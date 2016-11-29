#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX.DirectWrite;
#endregion
//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.Splinter
{
	public class Leo : Indicator
	{
		private bool hasBeenConfigured;
		private Brush redBrush;
		private Brush greenBrush;
		private Brush blueBrush;
		private Brush openBrush;
		private Brush grayBrush;
		private const float labelSize = 8f;
		private double highPrice;
		private double lowPrice;
		private double maxVolume;
		private double openPrice;
		private double trueRangeHigh;
		private double trueRangeLow;
		private double trueRangeMax;
		private double trueRangeMin;
		/*
				private int areaWidth = 0;
		*/
		internal int initialBarMarginRight = 8;
		private SessionIterator sessionIterator;
		private GreenRanger greenRanger;
		private Renet   renet;
		private BarsUpdating barsUpdating;
		private SolidColorBrush blackBrush;
		private bool shouldColorTrueRange;

		
		[NinjaScriptProperty]
		[Display(Name = @"True range method", GroupName = @"Data Series", Order = 0)]
		public NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod RangeMethod
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = @"True range look-back", GroupName = @"Data Series", Description = @"True range used to detect zones of profile's highs and lows")]
		[Range(0.0001, double.MaxValue)]
		public int RangePeriod
		{ get; set; }

		[Display(Name = @"Opacity (%)", GroupName = @"Visual"), Range(0.0001, double.MaxValue)]
		public double Opacity { get; set; }

		[Display(Name = @"Render current zone overlay", GroupName = @"Visual")]
		public bool IsRenderPriceZones { get; set; }

		[XmlIgnore(), Browsable(false)]
		public List<double> MoneyPits { get; set; }

		[XmlIgnore(), Browsable(false)]
		public Dictionary<double, double> VolumeDictionary { get; set; }

		#region virtuals
		protected override void OnStateChange()
		{
			switch (State)
			{
				case State.SetDefaults:
					{
						Name = "Leo";
						Calculate = Calculate.OnEachTick;
						IsOverlay = true;
						ScaleJustification = ScaleJustification.Right;
						RangePeriod = 14;
						Opacity = .35;
						IsRenderPriceZones = false;
						RangeMethod = TrueRangeMethod.PriorOpen;
						break;
					}
				case State.Configure:
					{
						try
						{
							AddDataSeries(Instrument.FullName, new BarsPeriod
							{
								BarsPeriodType = BarsPeriodType.Minute,
								Value = 1440
							}, "CME US Index Futures RTH");
						}
						catch
						{
							AddDataSeries("ES ##-##", new BarsPeriod
							{
								BarsPeriodType = BarsPeriodType.Minute,
								Value = 1440
							}, "CME US Index Futures RTH");

						}
						finally
						{
							
						}
						VolumeDictionary = new Dictionary<double, double>();
						MoneyPits = new List<double>();
						SetupBrushes();
						hasBeenConfigured = true;
						break;
					}
				case State.Historical:
					{
						sessionIterator = new SessionIterator(BarsArray[1]);
						greenRanger = GreenRanger(RangeMethod, RangePeriod);
						renet = Renet();
						// give us enough room to plot the volume profile without running into the bars
						if (ChartControl == null)
						{
							Log("ChartControl was null during historical", LogLevel.Error);
							return;
						}
						ChartControl.Dispatcher.InvokeAsync(() =>
						{
							// store incoming BarMarginRight to reset when we exit the chart
							ChartControl.Properties.BarMarginRight = initialBarMarginRight;
							ChartControl.Properties.BarMarginRight = 300; // set to custom value to make room for volume profile
						});
						break;
					}
				case State.Terminated:
					{
						// terminated is called throughout the application lifetime
						// sometimes in instances which are not related to the types usage
						if (!hasBeenConfigured || ChartControl == null)
							return;
						// set the bar margin back when we're done
						ChartControl.Dispatcher.InvokeAsync(() => { ChartControl.Properties.BarMarginRight = initialBarMarginRight; });
						DestroyBrushes();
						VolumeDictionary.Clear();
						MoneyPits.Clear();
						break;
					}
				case State.Undefined:
					{ break; }
				case State.Active:
					{ break; }
				case State.DataLoaded:
					{ break; }
				case State.Transition:
					{ break; }
				case State.Realtime:
					{ break; }
				case State.Finalized:
					{ break; }
				default:
					{
						throw new Exception(string.Format("Unhandled State={0}", State));
					}
			}
		}
		protected override void OnBarUpdate()
		{
			if(CurrentBars[1] < RangePeriod)
			{
				shouldColorTrueRange = false;
			}
			else
				shouldColorTrueRange = true;
		   // just makes on bar stuff easier to read
		   barsUpdating = (BarsUpdating)BarsInProgress;
			switch (barsUpdating)
			{
				case BarsUpdating.Primary:
					{
						maxVolume = GetMaxVolume(trueRangeMax, trueRangeMin);
						// recacualte trading hours/session template data before accessing below
						if (Bars.IsFirstBarOfSession) // cached for performance optimizations 
						{
							renet.RecalculateSession(Time[0]);
						}
						// primary bars are in session
						if (renet.IsRegularTradingHours(Time[0]))
						{
							// used to color the volume profile
							openPrice = greenRanger.DayOpen[0];
							trueRangeMax = greenRanger.RangeMax[0];
							trueRangeMin = greenRanger.RangeMin[0];
							trueRangeLow = greenRanger.RangeLow[0];
							trueRangeHigh = greenRanger.RangeHigh[0];
							double tmpPit = GetMoneyPit(Close[0]);
							if (tmpPit < double.MaxValue)
								MoneyPits.Add(tmpPit);
						}
						break;
					}
				case BarsUpdating.Tick:
					{
						// add tick volume at price for volume 
						AddToVolumeDictionary(Time[0], Close[0], Volume[0]);
						break;
					}
				case BarsUpdating.Daily:
					return;
				default:
					throw new Exception(string.Format("Unhandled BarsInProgress={0} ({1})", BarsInProgress,
						BarsArray[BarsInProgress].ToChartString()));
			}
		}
		
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (IsInHitTest)
				return;
			
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			RenderVolumeZones(chartControl, chartScale); // draw volume profile
			RenderLevelText(chartControl, chartScale); // draw text on key levels
			if (IsRenderPriceZones)
				RenderPriceZones(chartControl, chartScale);  // draw price zones on previous trading days		
			//double currentPrice = Close.GetValueAt(CurrentBar);
			//double nearestLevel = MoneyPits.Aggregate((x,y) => Math.Abs(x - currentPrice) < Math.Abs(y - currentPrice) ? x : y);
			//RenderPriceLevels(chartScale, nearestLevel, Brushes.Green);
			//foreach(double price in MoneyPits)
			//	RenderPriceLevels(chartScale, price, VolumeZoneBrush(price));
			base.OnRender(chartControl, chartScale);
		}
		#endregion
		#region helpers
		private void RenderPriceLevels(ChartScale chartScale, double price, Brush brush)
		{
			SharpDX.Vector2 lineStart = new Point(ChartPanel.W, chartScale.GetYByValue(price)).ToVector2();
			SharpDX.Vector2 lineEnd = new Point(0, lineStart.Y).ToVector2();
			RenderTarget.DrawLine(lineStart, lineEnd, brush.ToDxBrush(RenderTarget));
		}
		// time is just used for error handling, could create better class/object to track time with ticks
		private void AddToVolumeDictionary(DateTime tickTime, double tickClose, double tickVolume)
		{
			try
			{
				if (VolumeDictionary.ContainsKey(tickClose))
					VolumeDictionary[tickClose] += tickVolume;
				else
					VolumeDictionary.Add(tickClose, tickVolume);
			}
			catch
			{
				Log(string.Format("Unable to add to volume dictonary: t={0} c={1} v={2}", tickTime, tickClose, tickVolume),
					LogLevel.Error);
			}
		}
		public double GetVolumeRatio(double level)
		{
			if (VolumeDictionary == null) return 0;
			double volumeSum = VolumeDictionary.Sum(vol => vol.Value);
			if (VolumeDictionary.ContainsKey(level))
				return VolumeDictionary[level] / volumeSum;
			return 0; // fall back / no value
		}
		private string FormatLevelVolumeAt(double price)
		{
			return string.Format("{0} ({1})", Instrument.MasterInstrument.FormatPrice(price),
				GetVolumeRatio(price).ToString("P2", Core.Globals.GeneralOptions.CurrentCulture));
		}
		private void RenderLevelText(ChartControl chartControl, ChartScale chartScale)
		{
			using (TextFormat textFormat = chartControl.Properties.LabelFont.ToDirectWriteTextFormat())
			{
				// draw every ray price
				foreach (dynamic drawTool in DrawObjects)
				{
					double rayPrice;
					Brush rayBrush;
					// drawing tools sometimes fail, get these safley
					if (!TryGetRayValues(drawTool, out rayPrice, out rayBrush)) continue;
					RenderPriceText(chartScale, textFormat, rayBrush, rayPrice);
				}
			}
		}
		// getting drawing tools can sometimes fail, do this safely
		private bool TryGetRayValues(dynamic drawTool, out double price, out Brush tmpBrush)
		{
			try
			{
				// only draw if type is Ray and has actually been selected by user
				// otherwise too many prices drawn which hide the profile
				if (!drawTool.GetType().ToString().Contains("DrawingTools.Ray") || !drawTool.IsSelected)
				{
					price = 0;
					tmpBrush = null;
					return false;
				}
				drawTool.Stroke.Brush = Brushes.CornflowerBlue;
				// hard coded to default blue, might need to change to allow for custom colors?
				price = drawTool.StartAnchor.Price;
				// only render if ray does not match any previous level values
				// e.g., do not render ray.Price over openPrice
				if (price.ApproxCompare(openPrice) == 0)
				{
					drawTool.Stroke.Brush = Brushes.DarkOrange;
					tmpBrush = drawTool.Stroke.Brush;
				}
				else if (price.ApproxCompare(highPrice) == 0)
				{
					drawTool.Stroke.Brush = Brushes.DarkSeaGreen;
					tmpBrush = drawTool.Stroke.Brush;
				}
				else if (price.ApproxCompare(lowPrice) == 0)
				{
					drawTool.Stroke.Brush = Brushes.IndianRed;
					tmpBrush = drawTool.Stroke.Brush;
				}
				else tmpBrush = Brushes.White;
				return true;
			}
			catch (Exception e)
			{
				Log(string.Format("Unable to get Ray values: {0}", e), LogLevel.Error);
				price = 0;
				tmpBrush = null;
				return false;
			}
		}
		private void RenderPriceText(ChartScale chartScale, TextFormat textFormat, Brush brush, double price)
		{
			// only render what is in viewable range
			if (price >= chartScale.MaxValue || price < chartScale.MinValue) return;
			using (
				TextLayout textLayout = new TextLayout(Core.Globals.DirectWriteFactory, FormatLevelVolumeAt(price), textFormat,
					ChartPanel.W, labelSize))
			{
				SharpDX.Vector2 vector2 = GetTextVectorRight(chartScale, price, textLayout);
				RenderTarget.DrawTextLayout(vector2, textLayout, brush.ToDxBrush(RenderTarget));
			}
		}
		private double GetMaxVolume(double max, double min)
		{
			double tmpVolume = double.MinValue;
			if (VolumeDictionary == null) return tmpVolume;
			foreach (KeyValuePair<double, double> level in VolumeDictionary)
			{
				double price = level.Key;
				if (price > max || price < min) // optimize calculations
					continue;
				tmpVolume = level.Value;
			}
			return maxVolume = Math.Max(tmpVolume, maxVolume);
		}
		private double GetMoneyPit(double price)
		{
			//checks if the volume is less than one tick above
			// and also less than one tick below
			double nextlevelUp = price + TickSize;
			double nextLevelDown = price - TickSize;
			double testVolumeUp;
			double testVolumeDown;
			if (!VolumeDictionary.TryGetValue(nextlevelUp, out testVolumeUp) ||
				!VolumeDictionary.TryGetValue(nextLevelDown, out testVolumeDown))
				return double.MaxValue; // default value
										// valid money pit
			try
			{
				if (VolumeDictionary[price] < testVolumeUp && VolumeDictionary[price] < testVolumeDown)
				{
					return price;
				}
			}
			catch
			{
				// ok, whatever
			}
			// otherwise remove if it exists (level has cleared)
			if (MoneyPits.Contains(price))
				MoneyPits.Remove(price);
			// no match, return default value
			return double.MaxValue;
		}
		private void RenderVolumeZones(ChartControl chartControl, ChartScale chartScale)
		{
			foreach (KeyValuePair<double, double> level in VolumeDictionary)
			{
				double price = level.Key;
				int volume = (int) level.Value;
				if(price > chartScale.MaxValue + TickSize || price < chartScale.MinValue - (TickSize * 1))
					continue;
				int priceLower = chartScale.GetYByValue(price);
				int priceUpper = chartScale.GetYByValue(price + TickSize);
				int barHeight = Math.Max(1, Math.Abs(priceUpper - priceLower) - 1);
				maxVolume = Math.Max(volume, maxVolume);
				if (maxVolume < 1) // don't divide by zero
					continue;
				int maxWidth = ((int) maxVolume/300);
				int barWidth = volume/maxWidth;
				int barX = (chartControl.CanvasRight - barWidth);
				int barY = (priceLower - barHeight);
				SharpDX.RectangleF rect = new SharpDX.RectangleF(barX, barY, barWidth, barHeight);
				RenderTarget.FillRectangle(rect, VolumeZoneBrush(price).ToDxBrush(RenderTarget));
			}
		}
		private SharpDX.Vector2 GetTextVectorRight(ChartScale chartScale, double price, TextLayout textLayout)
		{
			return
				new Point(ChartPanel.W - textLayout.Metrics.Width, chartScale.GetYByValue(price) - textLayout.Metrics.Height)
					.ToVector2();
		}
		private void SetupBrushes()
		{
			openBrush	= new SolidColorBrush(Colors.DarkKhaki) { Opacity = Opacity };
			grayBrush	= new SolidColorBrush(Color.FromRgb(240, 240, 243)) { Opacity = Opacity };
			redBrush	= new SolidColorBrush(Colors.IndianRed) { Opacity = Opacity };
			greenBrush	= new SolidColorBrush(Colors.DarkSeaGreen) { Opacity = Opacity };
			blueBrush	= new SolidColorBrush(Color.FromRgb(143, 143, 188)) { Opacity = Opacity };
			blackBrush = new SolidColorBrush(Colors.Black) { Opacity = Opacity };
			openBrush.Freeze();
			grayBrush.Freeze();
			redBrush.Freeze();
			greenBrush.Freeze();
			blueBrush.Freeze();
			blackBrush.Freeze();
		}
		private void DestroyBrushes()
		{
			openBrush = null;
			grayBrush = null;
			redBrush = null;
			greenBrush = null;
			blueBrush = null;
			blackBrush = null;
		}
		private Brush VolumeZoneBrush(double price)
		{			
			if (Math.Abs(openPrice - price) < TickSize)
				return openBrush;
			if(!shouldColorTrueRange)
				return grayBrush;
			if (price >= trueRangeMax || price < trueRangeMin)
				return blackBrush;
			if (price >= trueRangeHigh)
				return greenBrush;
			if (price < trueRangeLow)
				return redBrush;
			return blueBrush;
		}
		#endregion
		#region not implemented
		// MH:  5-25 Disabled for now, can add option
		// ReSharper disable once UnusedMember.Local
		private void RenderPriceZones(ChartControl chartControl, ChartScale chartScale)
		{
			// add 10 bars of padding so that the drawn region extends past the visual area of the chart
			// but handle possible out of range exception by checking to see we even have enough bars for the padding to begin with
			int startX = chartControl.GetXByBarIndex(ChartBars,
				ChartBars.FromIndex >= 10 ? ChartBars.FromIndex - 10 : ChartBars.FromIndex);
			int sessionBeginX = chartControl.GetXByTime(sessionIterator.ActualSessionBegin);
			if (startX > sessionBeginX)
				return;
			float highPriceY = chartScale.GetYByValue(trueRangeHigh);
			float highLowDifference = chartScale.GetYByValue(trueRangeLow) - chartScale.GetYByValue(trueRangeHigh);
			SharpDX.RectangleF midZone = new SharpDX.RectangleF(sessionBeginX, highPriceY, startX - sessionBeginX,
				highLowDifference);
			SharpDX.RectangleF lowZone = new SharpDX.RectangleF(sessionBeginX, highPriceY + midZone.Height,
				startX - sessionBeginX, highLowDifference);
			SharpDX.RectangleF highZone = new SharpDX.RectangleF(sessionBeginX, highPriceY - midZone.Height,
				startX - sessionBeginX, highLowDifference);
			RenderTarget.FillRectangle(lowZone, redBrush.ToDxBrush(RenderTarget));
			RenderTarget.FillRectangle(highZone, greenBrush.ToDxBrush(RenderTarget));
			RenderTarget.FillRectangle(midZone, blueBrush.ToDxBrush(RenderTarget));
		}
		// not implemented correctly
		// ReSharper disable once UnusedMember.Local
		private float ScaleFontSize(int minFontSize, int maxFontSize)
		{
			float fontSize = minFontSize;
			for (int i = minFontSize; i < maxFontSize; i++)
			{
				fontSize = i;
			}
			return fontSize;
		}
		#endregion
	}
}
// ReSharper disable All

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Splinter.Leo[] cacheLeo;
		public Splinter.Leo Leo(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return Leo(Input, rangeMethod, rangePeriod);
		}

		public Splinter.Leo Leo(ISeries<double> input, NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			if (cacheLeo != null)
				for (int idx = 0; idx < cacheLeo.Length; idx++)
					if (cacheLeo[idx] != null && cacheLeo[idx].RangeMethod == rangeMethod && cacheLeo[idx].RangePeriod == rangePeriod && cacheLeo[idx].EqualsInput(input))
						return cacheLeo[idx];
			return CacheIndicator<Splinter.Leo>(new Splinter.Leo(){ RangeMethod = rangeMethod, RangePeriod = rangePeriod }, input, ref cacheLeo);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Splinter.Leo Leo(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.Leo(Input, rangeMethod, rangePeriod);
		}

		public Indicators.Splinter.Leo Leo(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.Leo(input, rangeMethod, rangePeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Splinter.Leo Leo(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.Leo(Input, rangeMethod, rangePeriod);
		}

		public Indicators.Splinter.Leo Leo(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.Leo(input, rangeMethod, rangePeriod);
		}
	}
}

#endregion
