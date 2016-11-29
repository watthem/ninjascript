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
using NinjaTrader.Core;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Indicators.Splinter;

#endregion


namespace NinjaTrader.NinjaScript
{
	public class Splinter : NinjaScriptBase
	{
		public Splinter(ISeries<double> input )
		{
			this.SetInput(input);
			SetState(State.Configure);
		}

		public sealed override void SetState(State state)
		{
			base.SetState(state);
		}

		internal void AddDataSeriesFuzzy(Indicator indicator, BarsPeriodType periodType, int value, string tradingHours)
		{
			FuzzyInstrument fuzzy = indicator.GetFuzzyInstrument(periodType, value, tradingHours);

			base.AddDataSeries(fuzzy.InstrumentName, fuzzy.Period, fuzzy.TradingHours);

			indicator.CopyTo(indicator);
		}

		public override string LogTypeName
		{
			get { throw new NotImplementedException(); }
		}
	}
}
namespace NinjaTrader.NinjaScript.Indicators.Splinter
{
	public enum TrueRangeMethod
	{
		PriorClose,
		CurrentOpen,
		PriorOpen
	}

	public enum BarsUpdating
	{
		Primary,
		Daily,
		Tick
	}

	public struct FuzzyInstrument
	{
		public string InstrumentName;
		public BarsPeriod Period;
		public string TradingHours;
	}

	public struct PriceZone
	{
		public string Name;
		public DateTime Time;
		public double High;
		public double Low;
	}

	public static class IndieHelper
	{
		public static string DefaultInstrumentName
		{
			get
			{
				try
				{
					return MasterInstrument.GetInstrumentByDate(Instrument.GetInstrument("ES"), Globals.Now, true, true, null).FullName;
				}
				catch (Exception e)
				{
					NinjaScript.Log("Error getting DefaultInstrumentName while calling GetInstrumentByDate: " + e, LogLevel.Error);
					return string.Empty;
				}
			}
		}
		public static FuzzyInstrument GetFuzzyInstrument(this Indicator indicator, BarsPeriodType periodType, int value, string tradingHours)
		{
			if (indicator == null) throw new ArgumentNullException("no indicator for fuzzy insturment (?)");

			FuzzyInstrument myFuzzyInstrument = new FuzzyInstrument
			{
				Period = new BarsPeriod {BarsPeriodType = periodType, Value = value},
				TradingHours = tradingHours
			};
			try
			{
				myFuzzyInstrument.InstrumentName = indicator.Instrument.FullName;
			}
			catch
			{
				myFuzzyInstrument.InstrumentName = DefaultInstrumentName;
#if DEBUG
				NinjaScript.Log(string.Format("{0} Had to use default data series when adding {1} ({2})", indicator.Name,
					myFuzzyInstrument.InstrumentName, myFuzzyInstrument.Period), LogLevel.Error);
#endif
			}
			return myFuzzyInstrument;
		}

	}


}

