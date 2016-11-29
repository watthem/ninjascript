#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;

using SharpDX;

#endregion
//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Splinter
{
	public class GreenRanger : Indicator
	{
		public struct PriceZone
		{
			public int Index { get; set; }
			public string Name { get; set; }
			public DateTime StartTime { get; set; }
			public DateTime EndTime { get; set; }
			public double ZoneHigh { get; set; }
			public double ZoneLow { get; set; }
			public double ZoneWeak { get; set; }
			public double ZoneStrong { get; set; }

			public double ZoneMiddle
			{
				get
				{
					return (ZoneHigh + ZoneLow) / 2;
				}
			}

			public override string ToString()
			{
				return string.Format("{0}: date={1} high={2} strong={3} middle={4} low={5} weak={6}", 
					this.Name, this.StartTime.Date, this.ZoneHigh, this.ZoneStrong, this.ZoneMiddle, this.ZoneLow, this.ZoneWeak);
			}
		}

		private double  sessionOpen;
		private double  normalAtr;
		private double  rangeSplit;
		private Renet   renet;
		private Raphael raphael;
		private PriceZone[] priceZoneArray = null;
		private Dictionary<DateTime, PriceZone> zonesList = new Dictionary<DateTime, PriceZone>();
		private DateTime openTime;


		public Series<PriceZone[]> priceZoneSeries;
		protected override void OnStateChange()
		{
			switch (State)
			{
				case State.SetDefaults:
				{
					Description = @"";
					Name = "Green Ranger";
					Calculate = Calculate.OnPriceChange;
					IsOverlay = true;
					DisplayInDataBox = true;
					DrawOnPricePanel = true;
					DrawHorizontalGridLines = true;
					DrawVerticalGridLines = true;
					PaintPriceMarkers = true;
					//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
					//See Help Guide for additional information.
					IsSuspendedWhileInactive = true;
					IsOverlay = true;
					IsAutoScale = true;
					AddPlot(new Stroke(Brushes.SeaGreen, 2), PlotStyle.Line, "Range Max");
					AddPlot(new Stroke(Brushes.DarkSeaGreen, 2), PlotStyle.Line, "Range High");
					AddPlot(new Stroke(Brushes.DarkKhaki, 2), PlotStyle.Line, "RTH Open");
					AddPlot(new Stroke(Brushes.IndianRed, 2), PlotStyle.Line, "Range Low");
					AddPlot(new Stroke(Brushes.Brown, 2), PlotStyle.Line, "Range Min");
					RangePeriod = 14;
					RangeMethod = TrueRangeMethod.PriorClose;
					ZOrder = int.MinValue + 1 * 69;
					break;
				}
				case State.Configure:
				{
					priceZoneArray = new PriceZone[5];
					priceZoneSeries = new Series<PriceZone[]>(this);
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
					break;
				}
				case State.Historical:
				{
					renet = Renet();
					raphael = Raphael(BarsArray[(int)BarsUpdating.Daily], RangeMethod, RangePeriod);

				
					break;
				}
			}
		}
		protected override void OnBarUpdate()
		{
			if (BarsInProgress == 1)
				return;
			if (Bars.IsFirstBarOfSession)
			{
				sessionOpen = Open[0];
				openTime = Time[0];
				if (CurrentBars[(int)BarsUpdating.Daily] < RangePeriod)
					return;

				normalAtr = this.ToTickSize(raphael[0]);
				rangeSplit = this.ToTickSize(normalAtr / 2);
				renet.RecalculateSession(Time[0]);
				foreach (BrushSeries brush in PlotBrushes)
				{
					brush[0] = Brushes.Transparent;
				}
			}

			if (renet.IsRegularTradingHours(Time[0]))
			{
				DayOpen[0] = sessionOpen;
				// draw rectangle for first 5 minutes maybe? Help try and catch lots of volume on tick chart
				// e.g., -> the wider the rectangle, the more activity in the first 5 minutes.


				if (normalAtr > 0 && rangeSplit > 0)
				{
					RangeMax[0] = DayOpen[0] + normalAtr;
					RangeHigh[0] = RangeMax[0] - rangeSplit;

					RangeMin[0] = DayOpen[0] - normalAtr;
					RangeLow[0] = RangeMin[0] + rangeSplit;
				}

				if (!Bars.IsFirstBarOfSession) return;

				double weakAtr = this.ToTickSize(raphael.atrFromOpen[0]);
				double strongAtr = this.ToTickSize(raphael.atrFromPriorOpen[0]);
				double weakSplit = this.ToTickSize(weakAtr / 2);
				double strongSplit = this.ToTickSize(strongAtr / 2);

				priceZoneArray[0] = new PriceZone
				{
					Name = "Max Zone",
					Index = 0,
					ZoneHigh = RangeMax[0],
					ZoneLow = RangeHigh[0],

					StartTime = Time[0],
					EndTime = renet.CurrentSessionEnd(Time[0]),

					ZoneStrong = DayOpen[0] + strongAtr,
					ZoneWeak = DayOpen[0] + weakAtr,
				};
				priceZoneArray[1] = new PriceZone
				{
					Name = "Bull Zone",
					Index = 1,
					ZoneHigh = RangeHigh[0],
					ZoneLow = DayOpen[0],
					StartTime = Time[0],
					EndTime = renet.CurrentSessionEnd(Time[0]),
					ZoneStrong = priceZoneArray[0].ZoneStrong - strongSplit,
					ZoneWeak = priceZoneArray[0].ZoneWeak - weakSplit,
				};
				// there is *probably* an open zone burried in here somewhere
				// look at Fri Jun 17 2016... tests open - 4 ticks twice
				// ... could there be a weak/strong open?
				priceZoneArray[2] = new PriceZone
				{
					Name = "Open Zone",
					Index = 2,
					ZoneHigh = DayOpen[0],
					ZoneLow = DayOpen[0],
					StartTime = Time[0],
					EndTime = renet.CurrentSessionEnd(Time[0]),
					ZoneStrong = DayOpen[0] + (4 * TickSize),
					ZoneWeak = DayOpen[0] - (4 * TickSize)
				};



				priceZoneArray[3] = new PriceZone
				{
					Name = "Bear Zone",
					Index = 3,
					ZoneHigh = DayOpen[0],
					ZoneLow = RangeLow[0],
					StartTime = Time[0],
					EndTime = renet.CurrentSessionEnd(Time[0]),
					ZoneWeak = (DayOpen[0] - weakAtr) + weakSplit,
					ZoneStrong = (DayOpen[0] - strongAtr) + strongSplit
				};

				priceZoneArray[4] = new PriceZone
				{
					Name = "Min Zone",
					Index = 4,
					ZoneHigh = RangeLow[0],
					ZoneLow = RangeMin[0],
					StartTime = Time[0],
					EndTime = renet.CurrentSessionEnd(Time[0]),
					ZoneWeak = DayOpen[0] - weakAtr,
					ZoneStrong = DayOpen[0] - strongAtr
				};

			


			}
			else
			{
				DayOpen.Reset();
				RangeMin.Reset();
				RangeMax.Reset();
				RangeLow.Reset();
				RangeHigh.Reset();
			}
			priceZoneSeries[0] = priceZoneArray;
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);


			for (int barIndex = ChartBars.FromIndex; barIndex <= ChartBars.ToIndex; barIndex++)
			{

				for (int plotIndex = 0; plotIndex < Values.Length; plotIndex++)
				{
					if (Values[plotIndex].IsValidDataPointAt(barIndex))
					{
						Series<double> plotSeries = Values[plotIndex];
						if (plotSeries == null) throw new ArgumentNullException(string.Format("plot series not found at {0}", plotIndex));
						int lastIndex = Math.Abs(barIndex - 1);

						//RenderPriceZoneAreas(chartControl, chartScale, barIndex, plotIndex);

						if (!plotSeries.IsValidDataPointAt(lastIndex) || plotSeries.IsValidDataPointAt(barIndex)) continue;
						double plotValue = plotSeries.GetValueAt(lastIndex);
						float textX = chartControl.GetXByBarIndex(ChartBars, barIndex);
						float textY = chartScale.GetYByValue(plotValue);
						string formatValue = Instrument.MasterInstrument.FormatPrice(plotValue, false);
						RenderText(textX, textY, formatValue, Plots[plotIndex].Brush);
					}
				}
			}
		}

		// TODO:  Disabled for now, need sure recntangels are drawn historically and for all price zones
		private void RenderPriceZoneAreas(ChartControl chartControl, ChartScale chartScale, int barIndex, int plotIndex)
		{
			SharpDX.RectangleF rectangleF = new SharpDX.RectangleF();

			DateTime tm = ChartBars.GetTimeByBarIdx(chartControl, barIndex).Date;

			PriceZone zone = zonesList[tm];


			rectangleF.Top = chartScale.GetYByValue(zone.ZoneStrong);
			rectangleF.Bottom = chartScale.GetYByValue(zone.ZoneWeak);
			rectangleF.Left = chartControl.GetXByTime(zone.StartTime);
			rectangleF.Right = ChartControl.GetXByTime(zone.EndTime);

			Brush tmpBrush =  Plots[plotIndex].Brush.Clone();

			tmpBrush.Opacity = .05;
			tmpBrush.Freeze();
			RenderTarget.FillRectangle(rectangleF, tmpBrush.ToDxBrush(RenderTarget));
		}		
		private void RenderText(float textX, float textY, string text, Brush brush)
		{
			if (ChartControl == null) return;
			using (SharpDX.DirectWrite.TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
			{
				using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, textFormat, ChartPanel.W, 8f))
				{
					SharpDX.Vector2 origin = new SharpDX.Vector2(textX,  textY - (float) (textLayout.Metrics.Height) / 2); // bump above copyright
					RenderTarget.DrawTextLayout(origin, textLayout, brush.ToDxBrush(RenderTarget));
				}
			}
		}
		[NinjaScriptProperty]
		[Display(Name = @"True range method", GroupName = @"Data Series", Order = 0)]
		public NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod RangeMethod
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = @"True range look-back", GroupName = @"Data Series", Description = @"True range used to detect zone of profile's highs and lows")]
		[Range(0.0001, double.MaxValue)]
		public int RangePeriod
		{ get; set; }

		[Browsable(false)]
		[XmlIgnore]
		[Display(Order = 1)]
		public Series<double> RangeMax
		{
			get { return Values[0]; }
		}
		[Browsable(false)]
		[XmlIgnore]
		[Display(Order = 2)]
		public Series<double> RangeHigh
		{
			get { return Values[1]; }
		}
		[Browsable(false)]
		[XmlIgnore]
		[Display(Order = 3)]
		public Series<double> DayOpen
		{
			get { return Values[2]; }
		}
		[Browsable(false)]
		[XmlIgnore]
		[Display(Order = 4)]
		public Series<double> RangeLow
		{
			get { return Values[3]; }
		}
		[Browsable(false)]
		[XmlIgnore]
		[Display(Order = 5)]
		public Series<double> RangeMin
		{
			get { return Values[4]; }
		}
	}
}
// ReSharper disable All

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Splinter.GreenRanger[] cacheGreenRanger;
		public Splinter.GreenRanger GreenRanger(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return GreenRanger(Input, rangeMethod, rangePeriod);
		}

		public Splinter.GreenRanger GreenRanger(ISeries<double> input, NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			if (cacheGreenRanger != null)
				for (int idx = 0; idx < cacheGreenRanger.Length; idx++)
					if (cacheGreenRanger[idx] != null && cacheGreenRanger[idx].RangeMethod == rangeMethod && cacheGreenRanger[idx].RangePeriod == rangePeriod && cacheGreenRanger[idx].EqualsInput(input))
						return cacheGreenRanger[idx];
			return CacheIndicator<Splinter.GreenRanger>(new Splinter.GreenRanger(){ RangeMethod = rangeMethod, RangePeriod = rangePeriod }, input, ref cacheGreenRanger);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Splinter.GreenRanger GreenRanger(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.GreenRanger(Input, rangeMethod, rangePeriod);
		}

		public Indicators.Splinter.GreenRanger GreenRanger(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.GreenRanger(input, rangeMethod, rangePeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Splinter.GreenRanger GreenRanger(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.GreenRanger(Input, rangeMethod, rangePeriod);
		}

		public Indicators.Splinter.GreenRanger GreenRanger(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int rangePeriod)
		{
			return indicator.GreenRanger(input, rangeMethod, rangePeriod);
		}
	}
}

#endregion
