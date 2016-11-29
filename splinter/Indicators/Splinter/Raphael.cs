//                _.---._
//            _.-(_o___o_)
//            )_.'_     _'.
//          _.-( (_`---'_) )-._
//        .'_.-'-._`"""`_.-'-._'.
//        /` |    __`"`__    | `\
//       |   | .'`  ^:^  `'. |   |
//       )'-.//      |      \\.-'(
//      /   //       |       \\   \
//      \   |=======.=.=======|   /
//       )`-|  ((AT)(R)      |-'(
//       \  \======/-\'\======/  /
//        \,=(    <_/;\_|    )=,/
//        /  -\      |      /-  \
//        | (`-'\    |    /'-`) |
//        \\_`\  '.__|__.'  /`_//
//          /     /     \     \
//         /    /`       `\    \
//        /_,="(           )"=,_\
//        )-_,="\         /"=,_-(
//         \    (         )    /
//          \    |       |    /
//           )._ |       | _.(
//       _.-'   '/       \'   '-._
//     (__,'  .'         '.  ', __)
//         '--`             `--'
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
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.Splinter
{
	public class Raphael : Indicator
	{
		public Series<double> atrFromPriorClose;
		public Series<double> atrFromOpen;
		public Series<double> atrFromPriorOpen;
		protected override void OnStateChange()
		{
			if (State != State.SetDefaults) return;
			Description = @"~(=o_o=)";
			Name = "Raphael";
			IsSuspendedWhileInactive = true;
			TrueRangePeriod = 14;
			RangeMethod = TrueRangeMethod.PriorOpen;
			AddPlot(Brushes.OrangeRed, "ATR");
		}
		protected override void OnBarUpdate()
		{
			double range = High[0]- Low[0];
			if (CurrentBar == 0)
			{
				atrFromPriorClose = new Series<double>(this);
				atrFromOpen = new Series<double>(this);
				atrFromPriorOpen = new Series<double>(this);
				Value[0] = range;
				return;
			}
			atrFromPriorClose[0] = CalculateATR(range, Close[1], atrFromPriorClose[1]);
			atrFromPriorOpen[0]  = CalculateATR(range, Open[1], atrFromPriorOpen[1]);
			atrFromOpen[0] = CalculateATR(range, Open[0], atrFromOpen[1]);
			switch (RangeMethod)
			{
				case NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod.PriorClose:
				{
					Value[0] = atrFromPriorClose[0];
					break;
				}
				case NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod.PriorOpen:
				{
					Value[0] = atrFromPriorOpen[0];
					break;
				}
				case NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod.CurrentOpen:
				{
					Value[0] = atrFromOpen[0];
					break;
				}
			}
		}
		public double CalculateATR(double range, double inputValue, double priorValue)
		{
			//https://en.wikipedia.org/wiki/Average_true_range
			//The true range is the largest of the:
			//Most recent period's high minus the most recent period's low
			//Absolute value of the most recent period's high minus the previous close
			//Absolute value of the most recent period's low minus the previous close
			double absoluteLowMinusClose = Math.Abs(Low[0] - inputValue);
			double absoluteHighMinusClose = Math.Abs(High[0] - inputValue);
			double maxAbsoluteHigh = Math.Max(range, absoluteHighMinusClose);
			double trueRange = Math.Max(absoluteLowMinusClose, maxAbsoluteHigh);
			return (priorValue * (Math.Min(CurrentBar + 1, TrueRangePeriod) - 1) + trueRange) /
			   Math.Min(CurrentBar + 1, TrueRangePeriod);
		}
		#region Properties
		[NinjaScriptProperty]
		[Display(Name = "Range Method", GroupName = "NinjaScriptParameters", Order = 0)]
		public NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod RangeMethod { get; set; }
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(Name = "True Range Period", GroupName = "NinjaScriptParameters", Order = 0)]
		public int TrueRangePeriod
		{ get; set; }
		#endregion
	}
}
// ReSharper disable All

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Splinter.Raphael[] cacheRaphael;
		public Splinter.Raphael Raphael(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int trueRangePeriod)
		{
			return Raphael(Input, rangeMethod, trueRangePeriod);
		}

		public Splinter.Raphael Raphael(ISeries<double> input, NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int trueRangePeriod)
		{
			if (cacheRaphael != null)
				for (int idx = 0; idx < cacheRaphael.Length; idx++)
					if (cacheRaphael[idx] != null && cacheRaphael[idx].RangeMethod == rangeMethod && cacheRaphael[idx].TrueRangePeriod == trueRangePeriod && cacheRaphael[idx].EqualsInput(input))
						return cacheRaphael[idx];
			return CacheIndicator<Splinter.Raphael>(new Splinter.Raphael(){ RangeMethod = rangeMethod, TrueRangePeriod = trueRangePeriod }, input, ref cacheRaphael);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Splinter.Raphael Raphael(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int trueRangePeriod)
		{
			return indicator.Raphael(Input, rangeMethod, trueRangePeriod);
		}

		public Indicators.Splinter.Raphael Raphael(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int trueRangePeriod)
		{
			return indicator.Raphael(input, rangeMethod, trueRangePeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Splinter.Raphael Raphael(NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int trueRangePeriod)
		{
			return indicator.Raphael(Input, rangeMethod, trueRangePeriod);
		}

		public Indicators.Splinter.Raphael Raphael(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Splinter.TrueRangeMethod rangeMethod, int trueRangePeriod)
		{
			return indicator.Raphael(input, rangeMethod, trueRangePeriod);
		}
	}
}

#endregion
