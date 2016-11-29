#region Using declarations
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NinjaTrader.Adapter;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.NinjaScript.OptimizationFitnesses;

#endregion
//This namespace holds SuperDOM Columns in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.SuperDomColumns
{
	public class DomSplit : SuperDomColumn
	{

		private double halfPenWidth;
		private FontFamily fontFamily;
		private FontStyle fontStyle;
		private FontWeight fontWeight;
		private long maxVolume;
		private bool resetAsk;
		private bool resetBid;
		private bool heightUpdateNeeded;
		private long lastAskVolume;
		private long lastBidVolume;
		private Pen gridPen;
		private Typeface typeFace;
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = @"Minutes", GroupName = @"Parameters", Order = 1)]
		public int Minutes { get; set; }

		[Display(Name = @"Starting Time", GroupName = @"Parameters", Order = 2)]
		[Gui.PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		public DateTime StartTime { get; set; }

		[XmlIgnore]
		[Display(Name = @"BackBrush", GroupName = @"Parameters", Order = 3)]
		public Brush BackColor { get; set; }
		[Browsable(false)]
		public string BackColorSerialize
		{
			get { return Serialize.BrushToString(BackColor); }
			set { BackColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = @"BidBrush", GroupName = @"Parameters", Order = 4)]
		public Brush BidColor { get; set; }
		[Browsable(false)]
		public string BarColorSerialize
		{
			get { return Serialize.BrushToString(BidColor); }
			set { BidColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = @"BidBrush", GroupName = @"Parameters", Order = 4)]
		public Brush AskColor { get; set; }
		[Browsable(false)]
		public string AskColorSerialize
		{
			get { return Serialize.BrushToString(AskColor); }
			set { AskColor = Serialize.StringToBrush(value); }
		}

		[Display(Name = @"DisplayText", GroupName = @"PropertyCategoryVisual", Order = 5)]
		public bool DisplayText { get; set; }

		[XmlIgnore]
		[Display(Name = @"ForeColor", GroupName = @"PropertyCategoryVisual", Order = 6)]
		public Brush ForeColor { get; set; }

		[Browsable(false)]
		public string ForeColorSerialize
		{
			get { return Serialize.BrushToString(ForeColor); }
			set { ForeColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Browsable(false)]
		public ConcurrentDictionary<double, long> LastVolumes { get; set; }

		protected override void OnStateChange()
		{
			switch (State)
			{
				case State.SetDefaults:
				{
					Name = "DomSplit";
					DefaultWidth = 160;
					PreviousWidth = -1;
					Minutes = 3;
					StartTime = DateTime.Parse("8:30 AM");
					BackColor = Brushes.Transparent;
					BidColor = Brushes.SeaGreen;
					AskColor = Brushes.DarkRed;
					ForeColor = Brushes.Gray;
					break;
				}
				case State.Configure:
				{
					asksAdded = new BadAssPrices();
					bidsAdded = new BadAssPrices();
					lastVolumes = new Dictionary<double, long>();
					executedAtAsks = new Dictionary<double, long>();
					executedAtBids = new Dictionary<double, long>();
					break;
				}
				case State.Active:
				{
					break;
				}
				case State.Terminated:
				{
					break;
				}
			}
		}

		protected override void OnMarketData(MarketDataEventArgs e)
		{
			OnPropertyChanged();
			if (!IsGoldenPeriod(e.Time)) return;

			switch (e.MarketDataType)
			{
				case MarketDataType.Last:
				{
					if (lastVolumes.ContainsKey(e.Price))
						lastVolumes[e.Price] += e.Volume;
					else
						lastVolumes.Add(e.Price, e.Volume);

					if (Math.Abs(e.Price - e.Ask) < e.Instrument.MasterInstrument.TickSize)
					{
						resetAsk = true;
						if (executedAtAsks.ContainsKey(e.Price))
							executedAtAsks[e.Price] += e.Volume;
						else
							executedAtAsks.Add(e.Price, e.Volume);
					}
					else if (Math.Abs(e.Price - e.Bid) < e.Instrument.MasterInstrument.TickSize)
					{
						resetBid = true;
						if (executedAtBids.ContainsKey(e.Price))
							executedAtBids[e.Price] += e.Volume;
						else
							executedAtBids.Add(e.Price, e.Volume);
					}
					break;
				}
				case MarketDataType.Ask:
				{
					if (resetAsk)
					{
						lastAskVolume = e.Volume;
						resetAsk = false;
					}

					if (e.Volume > lastAskVolume)
					{
						asksAdded[e.Price] += e.Volume - lastAskVolume;
						lastAskVolume = e.Volume;
					}
					break;
				}
				case MarketDataType.Bid:
				{
					if (resetBid)
					{
						lastBidVolume = e.Volume;
						resetBid = false;
					}

					if (e.Volume > lastBidVolume)
					{
						bidsAdded[e.Price] += e.Volume - lastBidVolume;
						lastBidVolume = e.Volume;
					}
					break;
				}
			}
		}

		private bool IsGoldenPeriod(DateTime time)
		{
			return time.TimeOfDay >= StartTime.TimeOfDay && time.TimeOfDay <= StartTime.AddMinutes(Minutes).TimeOfDay;
			// magic number magic mike picked
		}

		private BadAssPrices asksAdded, bidsAdded;
		private Dictionary<double, long> lastVolumes, executedAtAsks, executedAtBids;		

		protected override void OnRender(DrawingContext dc, double renderWidth)
		{
			// This may be true if the UI for a column hasn't been loaded yet (e.g., restoring multiple tabs from workspace won't load each tab until it's clicked by the user)
			if (gridPen == null)
			{
				if (UiWrapper != null && PresentationSource.FromVisual(UiWrapper) != null)
				{
					double dpiFactor = 1/PresentationSource.FromVisual(UiWrapper).CompositionTarget.TransformToDevice.M11;
					gridPen = new Pen(Application.Current.TryFindResource("BorderThinBrush") as Brush, 1 * dpiFactor);
					halfPenWidth = gridPen.Thickness * 0.5;
				}
			}
			if (fontFamily != SuperDom.Font.Family
				|| (SuperDom.Font.Italic && fontStyle != FontStyles.Italic)
				|| (!SuperDom.Font.Italic && fontStyle == FontStyles.Italic)
				|| (SuperDom.Font.Bold && fontWeight != FontWeights.Bold)
				|| (!SuperDom.Font.Bold && fontWeight == FontWeights.Bold))
			{
				// Only update this if something has changed
				fontFamily = SuperDom.Font.Family;
				fontStyle = SuperDom.Font.Italic ? FontStyles.Italic : FontStyles.Normal;
				fontWeight = SuperDom.Font.Bold ? FontWeights.Bold : FontWeights.Normal;
				typeFace = new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal);
				heightUpdateNeeded = true;
			}
			if (gridPen == null) return;
			double verticalOffset = -gridPen.Thickness;
			lock (SuperDom.Rows)
				foreach (PriceRow row in SuperDom.Rows)
				{
					if (renderWidth - halfPenWidth >= 0)
					{
						double halfWidth = renderWidth / 2 - halfPenWidth;
						//bids
						Rect bidRect = new Rect(-halfPenWidth, verticalOffset, halfWidth, SuperDom.ActualRowHeight);
						{
							// Create a guidelines set
							GuidelineSet guidelines = new GuidelineSet();
							guidelines.GuidelinesX.Add(bidRect.Left + halfPenWidth);
							guidelines.GuidelinesX.Add(bidRect.Right + halfPenWidth);
							guidelines.GuidelinesY.Add(bidRect.Top + halfPenWidth);
							guidelines.GuidelinesY.Add(bidRect.Bottom + halfPenWidth);

							dc.PushGuidelineSet(guidelines);
							dc.DrawRectangle(BackColor, null, bidRect);
							dc.DrawLine(gridPen, new Point(-gridPen.Thickness, bidRect.Bottom),
								new Point(renderWidth - halfPenWidth, bidRect.Bottom));
							dc.DrawLine(gridPen, new Point(bidRect.Right, verticalOffset), new Point(bidRect.Right, bidRect.Bottom));
						}
						//asks
						Rect askRect = new Rect(halfWidth, verticalOffset, halfWidth, SuperDom.ActualRowHeight);
						{
							// Create a guidelines set
							GuidelineSet guidelines = new GuidelineSet();
							guidelines.GuidelinesX.Add(askRect.Left + halfPenWidth);
							guidelines.GuidelinesX.Add(askRect.Right + halfPenWidth);
							guidelines.GuidelinesY.Add(askRect.Top + halfPenWidth);
							guidelines.GuidelinesY.Add(askRect.Bottom + halfPenWidth);

							dc.PushGuidelineSet(guidelines);
							dc.DrawRectangle(BackColor, null, askRect);
							dc.DrawLine(gridPen, new Point(-gridPen.Thickness, askRect.Bottom),
								new Point(renderWidth - halfPenWidth, askRect.Bottom));
							dc.DrawLine(gridPen, new Point(askRect.Right, verticalOffset), new Point(askRect.Right, askRect.Bottom));
						}

						if (!SuperDom.IsConnected || SuperDom.IsReloading || State != State.Active)
							verticalOffset += SuperDom.ActualRowHeight;
						else
						{
							// Draw proportional volume bar
							if (asksAdded[row.Price] != 0)
							{
								long rowVolume = asksAdded[row.Price];
								maxVolume = Math.Max(rowVolume, maxVolume);
								double totalWidth = halfWidth*(rowVolume)/maxVolume;
								if (totalWidth - gridPen.Thickness >= 0)
								{
									dc.DrawRectangle(Brushes.Red, null,
										new Rect(halfWidth, verticalOffset + halfPenWidth, totalWidth, askRect.Height - gridPen.Thickness));
								}
							}

							if (executedAtAsks.ContainsKey(row.Price) && executedAtAsks[row.Price] != 0)
							{
								long rowVolume = executedAtAsks[row.Price];
								maxVolume = Math.Max(rowVolume, maxVolume);
								double totalWidth = halfWidth*(rowVolume)/maxVolume;
								if (totalWidth - gridPen.Thickness >= 0)
								{
									dc.DrawRectangle(Brushes.DarkRed, null,
										new Rect(halfWidth, verticalOffset + halfPenWidth, totalWidth, askRect.Height - gridPen.Thickness));
								}
							}

							if (bidsAdded[row.Price] != 0)
							{
								long rowVolume = bidsAdded[row.Price];
								maxVolume = Math.Max(rowVolume, maxVolume);
								double totalWidth = halfWidth*(rowVolume)/maxVolume;
								if (totalWidth - gridPen.Thickness >= 0)
								{
									dc.DrawRectangle(Brushes.Green, null,
										new Rect(halfWidth - totalWidth, verticalOffset + halfPenWidth, totalWidth, bidRect.Height - gridPen.Thickness));
								}
							}

							if (executedAtBids.ContainsKey(row.Price) && executedAtBids[row.Price] != 0)
							{
								long rowVolume = executedAtBids[row.Price];
								maxVolume = Math.Max(rowVolume, maxVolume);
								double totalWidth = halfWidth*(rowVolume)/maxVolume;
								if (totalWidth - gridPen.Thickness >= 0)
								{
									dc.DrawRectangle(Brushes.DarkGreen, null,
										new Rect(halfWidth - totalWidth, verticalOffset + halfPenWidth, totalWidth, bidRect.Height - gridPen.Thickness));
								}
							}


							else
							{
								verticalOffset += SuperDom.ActualRowHeight;
								continue;
							}

							verticalOffset += SuperDom.ActualRowHeight;
						}
						dc.Pop();
					}
				}
		}		

		internal class BadAssPrices
		{
			private readonly IDictionary<double, long> volumeDictionary = new Dictionary<double, long>();

			public long this[double price]
			{
				// returns value if exists
				get
				{
					return volumeDictionary.ContainsKey(price) ? volumeDictionary[price] : 0;
				}

				// updates if exists, adds if doesn't exist
				set
				{
					if (volumeDictionary.ContainsKey(price))
					{
						volumeDictionary[price] = value;
					}
					else volumeDictionary.Add(price, value);
				}
			}

			public override string ToString()
			{
				return  string.Join(";", volumeDictionary.Select(x => x.Key + "=" + x.Value));
			}
		}	
	}
}