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
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Splinter
{
	public class Playhouse : Indicator
	{
		private NinjaTrader.NinjaScript.Splinter splinter;
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Enter the description for your new custom Indicator here.";
				Name = "Playhouse";
				Calculate = Calculate.OnBarClose;
				IsOverlay = false;
				DisplayInDataBox = true;
				DrawOnPricePanel = true;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = true;
				PaintPriceMarkers = true;
				ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive = true;
			}
			else if (State == State.Configure)
			{
				//splinter = new NinjaTrader.NinjaScript.Splinter(Input);
				//splinter.AddDataSeriesFuzzy(this, BarsPeriodType.Minute, 60, "CME US Index Futures RTH");

				this.AddDataSeriesFuzzy(BarsPeriodType.Minute, 60, "CME US Index Futures RTH");
				//AddDataSeries("ES 09-16", new BarsPeriod() { BarsPeriodType = BarsPeriodType.Minute, Value = 60 }, "CME US Index Futures RTH");
			}
			else if (State == State.Historical)
			{
			}
		}

		internal void AddDataSeriesFuzzy(BarsPeriodType periodType, int value, string tradingHours)
		{
			FuzzyInstrument fuzzy = this.GetFuzzyInstrument(periodType, value, tradingHours);
			AddDataSeries(fuzzy.InstrumentName, fuzzy.Period, fuzzy.TradingHours);
		}
		protected override void OnBarUpdate()
		{

			if (BarsInProgress == 1)
				Print(BarsArray[1].ToChartString());
			//Add your custom indicator logic here.
		}
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Splinter.Playhouse[] cachePlayhouse;
		public Splinter.Playhouse Playhouse()
		{
			return Playhouse(Input);
		}

		public Splinter.Playhouse Playhouse(ISeries<double> input)
		{
			if (cachePlayhouse != null)
				for (int idx = 0; idx < cachePlayhouse.Length; idx++)
					if (cachePlayhouse[idx] != null &&  cachePlayhouse[idx].EqualsInput(input))
						return cachePlayhouse[idx];
			return CacheIndicator<Splinter.Playhouse>(new Splinter.Playhouse(), input, ref cachePlayhouse);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Splinter.Playhouse Playhouse()
		{
			return indicator.Playhouse(Input);
		}

		public Indicators.Splinter.Playhouse Playhouse(ISeries<double> input )
		{
			return indicator.Playhouse(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Splinter.Playhouse Playhouse()
		{
			return indicator.Playhouse(Input);
		}

		public Indicators.Splinter.Playhouse Playhouse(ISeries<double> input )
		{
			return indicator.Playhouse(input);
		}
	}
}

#endregion
