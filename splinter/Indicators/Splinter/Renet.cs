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
using System.Globalization;
#endregion
//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.Splinter
{
	public class Renet : Indicator
	{
		private bool 				isHigherTimeFrame;
		private SessionIterator 	sessionIterator;
		private Brush 				first30MinuteBrush;
		private TimeSpan 			endOfDay;
		protected override void OnStateChange()
		{
			switch(State)
			{
				case State.SetDefaults:
				{
					Name = "Renet";
					Description = "Time master";
					Calculate = Calculate.OnEachTick;
					IsOverlay = true;
					AddPlot(Brushes.Black, "Globex Close");
					ZOrder = int.MinValue;
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
					break;
				}
				case State.Historical:
				{
					sessionIterator = new SessionIterator(BarsArray[1]);
					if (ChartControl != null)
					{
						// set the shade for first 30 minutes by cloning a visible version of the user configured background brush
						first30MinuteBrush = (Utilities.CalculateVisibleColor(ChartControl.Properties.ChartBackground) as Brush).Clone();
						first30MinuteBrush.Opacity = .10f;
						first30MinuteBrush.Freeze();
					}
					// used variously to prevent some actions on bigger time frames
					BarsPeriod inputPeriod = BarsArray[0].BarsPeriod;
					if (inputPeriod != null &&
					    (inputPeriod.BarsPeriodType == (BarsPeriodType.Day) ||
					     (inputPeriod.BarsPeriodType == BarsPeriodType.Minute && inputPeriod.Value >= 60)))
					{
						isHigherTimeFrame = true;
					}
					break;
				}
				case State.Terminated:
				{ break; }
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
			if(BarsInProgress == 1)
				return;
			// just using value series to plot the close price if out of hours
			// just a visual effect to try and hide out of session data...
			Value[0] = Close[0];
			// recacualte trading hours/session template data before accessing below
			if(Bars.IsFirstBarOfSession) // cached for performance optimizations 
			{
				// only need to get these values as primary bar is in new session
				RecalculateSession(Time[0]);
				endOfDay = sessionIterator.ActualSessionEnd.TimeOfDay;
			}
			// primary bars are in session
			if(IsRegularTradingHours(Time[0]))
			{
				if(IsFirstThirtyMinutes(Time[0]))
				{
					BackBrush = first30MinuteBrush;
				}
				Value.Reset(); // do not continue painting value series
			}
			else // out of session, paint all bars black
			{
				// do not paint on higher tickTime frames e.g., 60 minute, daily, etc.
				if(isHigherTimeFrame)
					return;
				// hide bars out of session, just use values series to paint black line on close
				BarBrush = Brushes.Transparent;
				CandleOutlineBrush = Brushes.Transparent;
			}
		}
		public void RecalculateSession(DateTime time)
		{
			if (sessionIterator == null)
			{
				if (BarsArray != null) sessionIterator = new SessionIterator(BarsArray[1]);
			}		
			sessionIterator.GetNextSession(time, false);
		}
		public bool IsRegularTradingHours(DateTime time)
		{
			if (sessionIterator == null)
			{
				RecalculateSession(time);
			}
			return time >= sessionIterator.ActualSessionBegin && time < sessionIterator.ActualSessionEnd;
		}
		public DateTime CurrentSessionEnd(DateTime time)
		{
			if (sessionIterator == null)
			{
				RecalculateSession(time);
			}
			return sessionIterator.ActualSessionEnd;
		}

		public bool IsFirstThirtyMinutes(DateTime time)
		{
			if (sessionIterator == null)
			{
				RecalculateSession(time);
			}

			return time <= sessionIterator.ActualSessionBegin.AddMinutes(30);
		}
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);
			for(int barIndex = ChartBars.FromIndex; barIndex < ChartBars.ToIndex; barIndex++)
			{
				if (!BarsArray[0].IsFirstBarOfSessionByIndex(barIndex) ||
				    chartControl.GetTimeBySlotIndex(barIndex).TimeOfDay >= endOfDay) continue;
				float textX = chartControl.GetXByBarIndex(ChartBars, barIndex);
				float textY = chartScale.GetYByValue(chartScale.MinValue);
				DayOfWeek dayOfBar = chartControl.GetTimeBySlotIndex(barIndex).DayOfWeek;
				DateTimeFormatInfo dateTimeFormatInfo = new CultureInfo("en-US").DateTimeFormat;
				string shortDayName = dateTimeFormatInfo.DayNames[(int) dayOfBar].Substring(0, 3);
				RenderText(textX, textY, shortDayName, ChartControl.Properties.ChartText);
			}
		}
		private void RenderText(float textX, float textY, string text, Brush brush)
		{
			if (ChartControl == null) return;
			using(SharpDX.DirectWrite.TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
			{
				using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, textFormat, ChartPanel.W, 8f))
				{
					SharpDX.Vector2 origin = new SharpDX.Vector2(textX, textY - (float) (textLayout.Metrics.Height * 2.5)); // bump above copyright
					RenderTarget.DrawTextLayout(origin, textLayout, brush.ToDxBrush(RenderTarget));
				}
			}
		}
	}
}
// ReSharper disable All

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Splinter.Renet[] cacheRenet;
		public Splinter.Renet Renet()
		{
			return Renet(Input);
		}

		public Splinter.Renet Renet(ISeries<double> input)
		{
			if (cacheRenet != null)
				for (int idx = 0; idx < cacheRenet.Length; idx++)
					if (cacheRenet[idx] != null &&  cacheRenet[idx].EqualsInput(input))
						return cacheRenet[idx];
			return CacheIndicator<Splinter.Renet>(new Splinter.Renet(), input, ref cacheRenet);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Splinter.Renet Renet()
		{
			return indicator.Renet(Input);
		}

		public Indicators.Splinter.Renet Renet(ISeries<double> input )
		{
			return indicator.Renet(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Splinter.Renet Renet()
		{
			return indicator.Renet(Input);
		}

		public Indicators.Splinter.Renet Renet(ISeries<double> input )
		{
			return indicator.Renet(input);
		}
	}
}

#endregion
