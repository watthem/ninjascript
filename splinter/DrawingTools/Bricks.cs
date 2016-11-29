// 
// Copyright (C) 2014, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{


	public class Bricks : DrawingTool
	{
		[CLSCompliant(false)]
		protected SharpDX.Direct2D1.PathGeometry	arrowPathGeometry;
		private const double						cursorSensitivity		= 15;
		private	ChartAnchor							editingAnchor;
		private	ChartAnchor							lastMouseMoveDataPoint	= null;

		public override IEnumerable<ChartAnchor> Anchors { get { return new[] { StartAnchor, EndAnchor }; } }

		[Display(Order = 1)]
		public ChartAnchor StartAnchor { get; set; }
		[Display(Order = 2)]
		public ChartAnchor	EndAnchor		{ get; set; }

		[Display(ResourceType=typeof(Custom.Resource), GroupName = "NinjaScriptGeneral", Name="NinjaScriptDrawingToolLine", Order = 99)]
		public Stroke		Stroke			{ get; set; }

		public override bool SupportsAlerts { get { return true; } }
		
		public override void OnCalculateMinMax()
		{
			MinValue = double.MaxValue;
			MaxValue = double.MinValue;

			if (!this.IsVisible)
				return;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			try 
			{
				if (Stroke != null)
					Stroke.Dispose();
			}
			catch { }
			finally
			{
				Stroke = null;
			}
		}

		public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, Point point)
		{
			switch (DrawingState)
			{
				case DrawingState.Building:	return Cursors.Pen;
				case DrawingState.Moving:	return IsLocked ? Cursors.No : Cursors.SizeAll;
				case DrawingState.Editing:
					if (IsLocked)
						return Cursors.No;
					return editingAnchor == StartAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE;
				default:
					// draw move cursor if cursor is near line path anywhere
					Point startPoint = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);

					ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
					if (closest != null)
					{
						if (IsLocked)
							return Cursors.Arrow;
						return closest == StartAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE;
					}

					Point	endPoint		= EndAnchor.GetPoint(chartControl, chartPanel, chartScale);
					Point	minPoint		= startPoint;
					Point	maxPoint		= endPoint;

					
					maxPoint	= GetExtendedPoint(startPoint, endPoint);
					
					Vector	totalVector	= maxPoint - minPoint;
					return MathHelper.IsPointAlongVector(point, minPoint, totalVector, cursorSensitivity) ?
						IsLocked ? Cursors.Arrow : Cursors.SizeAll : null;
			}
		}

		public override IEnumerable<AlertConditionItem> GetAlertConditionItems()
		{
			yield return new AlertConditionItem 
			{
				Name = Custom.Resource.NinjaScriptDrawingToolLine,
				ShouldOnlyDisplayName = true,
			};
		}
		
		public override sealed Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
		{
			ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
			Point startPoint = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point endPoint = EndAnchor.GetPoint(chartControl, chartPanel, chartScale);

			int totalWidth = chartPanel.W + chartPanel.X;
			int totalHeight = chartPanel.Y + chartPanel.H;

			//Vector strokeAdj = new Vector(Stroke.Width / 2, Stroke.Width / 2);
			Point midPoint = startPoint + ((endPoint - startPoint) / 2);
			return new[]{ startPoint, midPoint, endPoint };
		}

		public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
		{
			if (DrawingState == DrawingState.Building)
				return true;

			DateTime	minTime = Core.Globals.MaxDate;
			DateTime	maxTime = Core.Globals.MinDate;

			Point		minPoint;
			Point		maxPoint;

			// here we'll get extended point and see if they're on scale
			ChartPanel panel = chartControl.ChartPanels[PanelIndex];
			Point startPoint	= StartAnchor.GetPoint(chartControl, panel, chartScale);
			Point endPoint		= EndAnchor.GetPoint(chartControl, panel, chartScale);

			minPoint = startPoint;
			maxPoint = GetExtendedPoint(chartControl, panel, chartScale, StartAnchor, EndAnchor);

			foreach (Point pt in new[] { minPoint, maxPoint })
			{
				DateTime time = chartControl.GetTimeByX((int) pt.X);
				if (time > maxTime)
					maxTime = time;
				if (time < minTime)
					minTime = time;
			}
			
			if ((minTime >= firstTimeOnChart && minTime <= maxTime) || (maxTime >= firstTimeOnChart && maxTime <= lastTimeOnChart))
				return true;

			return true;
		}

		public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (lastMouseMoveDataPoint == null)
			{
				lastMouseMoveDataPoint = new ChartAnchor();
				dataPoint.CopyDataValues(lastMouseMoveDataPoint);
			}
			switch (DrawingState)
			{
				case DrawingState.Building:
					if (StartAnchor.IsEditing)
					{
						dataPoint.CopyDataValues(StartAnchor);
						
						StartAnchor.IsEditing = false;

						// these lines only need one anchor, so stop editing end anchor too
//						if (LineType == ChartLineType.HorizontalLine || LineType == ChartLineType.VerticalLine)
//							EndAnchor.IsEditing = false;

						// give end anchor something to start with so we dont try to render it with bad values right away
						dataPoint.CopyDataValues(EndAnchor);
					}
					else if (EndAnchor.IsEditing)
					{
						dataPoint.CopyDataValues(EndAnchor);
						
						EndAnchor.Price = StartAnchor.Price;
						EndAnchor.IsEditing = false;
					}
					
					// is initial building done (both anchors set)
					if (!StartAnchor.IsEditing && !EndAnchor.IsEditing)
					{
						DrawingState = DrawingState.Normal;
						IsSelected = false;
					}
					break;
				case DrawingState.Normal:
					Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
					// see if they clicked near a point to edit, if so start editing
					
					editingAnchor = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);

					if (editingAnchor != null)
					{
						editingAnchor.IsEditing = true;
						DrawingState = DrawingState.Editing;
						
					}
					else
					{
						if (GetCursor(chartControl, chartPanel, chartScale, point) != null)
						{
							DrawingState = DrawingState.Moving;
							dataPoint.CopyDataValues(lastMouseMoveDataPoint);
						}
						else
						{
							// user whiffed.
							IsSelected	= false;
						}
					}
					break;
			}
		}

		public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (IsLocked && DrawingState != DrawingState.Building)
				return;

			if (DrawingState == DrawingState.Building)
			{
				// start anchor will not be editing here because we start building as soon as user clicks, which
				// plops down a start anchor right away
				if (EndAnchor.IsEditing)
				{
					dataPoint.CopyDataValues(EndAnchor);
					
					StartAnchor.Price = EndAnchor.Price;
					
				}
			}
			else if (DrawingState == DrawingState.Editing && editingAnchor != null)
			{
				
				editingAnchor.Price = dataPoint.Price;			
				
				StartAnchor.Price = dataPoint.Price;
				EndAnchor.Price = dataPoint.Price;
				
				if(editingAnchor.Time == StartAnchor.Time)
					StartAnchor.Time = dataPoint.Time;
				
				else EndAnchor.Time = dataPoint.Time;
				
			}
			else if (DrawingState == DrawingState.Moving)
			{
				foreach (ChartAnchor anchor in Anchors)
				{			
					anchor.MoveAnchor(lastMouseMoveDataPoint, dataPoint, chartControl, chartPanel, chartScale, this);//lastMouseMovePoint.Value, point, chartControl, chartScale);
				}
				
				dataPoint.CopyDataValues(lastMouseMoveDataPoint);
			}
		}

		public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			// simply end whatever moving
			if (DrawingState == DrawingState.Moving || DrawingState == DrawingState.Editing)
				DrawingState = DrawingState.Normal;
			if (editingAnchor != null)
				editingAnchor.IsEditing = false;
			editingAnchor = null;
			foreach (ChartAnchor anchor in Anchors)
				anchor.ClearMoveDelta();
			lastMouseMoveDataPoint = null;
		}

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			Stroke.RenderTarget = RenderTarget;

			// first of all, turn on anti-aliasing to smooth out our line
			RenderTarget.AntialiasMode	= SharpDX.Direct2D1.AntialiasMode.PerPrimitive;

			ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];
			
			Point startPoint = StartAnchor.GetPoint(chartControl, panel, chartScale);

			// align to full pixel to avoid unneeded aliasing
			double strokePixAdj =	Stroke.Width % 2 == 0 ? 0.5d : 0d;
			Vector pixelAdjustVec = new Vector(strokePixAdj, strokePixAdj);
			
			Point					endPoint			= EndAnchor.GetPoint(chartControl, panel, chartScale);

			// convert our start / end pixel points to directx 2d vectors
			Point					startPointAdjusted	= startPoint + pixelAdjustVec;
			Point					endPointAdjusted	= endPoint + pixelAdjustVec;
			SharpDX.Vector2			startVec			= startPointAdjusted.ToVector2();
			SharpDX.Vector2			endVec				= endPointAdjusted.ToVector2();
			SharpDX.Direct2D1.Brush	tmpBrush			= IsInHitTest ? chartControl.SelectionBrush : Stroke.BrushDX;

			// if a plain ol' line, then we're all done
			// if we're an arrow line, make sure to draw the actual line. for extended lines, only a single
			// line to extended points is drawn below, to avoid unneeded multiple DrawLine calls
			RenderTarget.DrawLine(startVec, endVec,	tmpBrush, Stroke.Width, Stroke.StrokeStyle);
			// we have a line type with extensions (ray / extended line) or additional drawing needed
			// create a line vector to easily calculate total length
			Vector lineVector = endPoint - startPoint;
			lineVector.Normalize();

			Point minPoint = startPointAdjusted;
			Point maxPoint = GetExtendedPoint(chartControl, panel, chartScale, StartAnchor, EndAnchor);//GetExtendedPoint(startPoint, endPoint); //
			RenderTarget.DrawLine(endPointAdjusted.ToVector2(), maxPoint.ToVector2(), tmpBrush, Stroke.Width, Stroke.StrokeStyle);
		
		}
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name						= "Bricks";
//				LineType					= ChartLineType.Line;
				DrawingState				= DrawingState.Building;
				StartAnchor					= new ChartAnchor { IsEditing = true, DrawingTool = this };
				EndAnchor					= new ChartAnchor { IsEditing = true, DrawingTool = this };
				StartAnchor.DisplayName		= Custom.Resource.NinjaScriptDrawingToolAnchorStart;
				EndAnchor.DisplayName		= Custom.Resource.NinjaScriptDrawingToolAnchorEnd;
				// a normal line with both end points has two anchors
				StartAnchor.IsBrowsable		= true;
				EndAnchor.IsBrowsable		= true;
				Stroke						= new Stroke(Brushes.MediumOrchid, 2f);
			}
			else if (State == State.Terminated)
			{
				// release any device resources
				Dispose();
			}
		}
	}
}