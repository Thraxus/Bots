using Bots.GridControl.DataTypes;
using Bots.GridControl.Interfaces;
using VRage.Collections;
using VRage.Game;

namespace Bots.GridControl.Models
{
	public class DebugDraw : IUpdate
	{
		// MyTransparentGeometry 
		// MySimpleObjectDraw
		private readonly ConcurrentCachingList<Line> _drawLines = new ConcurrentCachingList<Line>();
		
		public void Update(long tick)
		{
			DrawLines();
		}

		private void DrawLines()
		{
			foreach (Line line in _drawLines)
			{
				//MySimpleObjectDraw.DrawLine(line.From, line.To, line.Material, ref line.Color, line.Thickness);
				MyTransparentGeometry.AddLineBillboard(line.Material, line.Color, line.From, line.DirectionNormalized, line.Length, line.Thickness);
			}
		}

		public void DrawDot(PointBillboard point)
		{
			MyTransparentGeometry.AddPointBillboard(point.Material, point.Color, point.Point, point.Radius, point.Angle);
		}

		public void DrawLine(Line line)
		{
			//MySimpleObjectDraw.DrawLine(line.From, line.To, line.Material, ref line.Color, 1);
			MyTransparentGeometry.AddLineBillboard(line.Material, line.Color, line.From, line.DirectionNormalized, line.Length, line.Thickness);
		}

		public void AddLine(Line line)
		{
			_drawLines.Add(line);
			_drawLines.ApplyAdditions();
		}

		public void RemoveLine(Line line)
		{
			_drawLines.Remove(line);
			_drawLines.ApplyRemovals();
		}

		public void ClearLines()
		{
			_drawLines.ClearList();
			_drawLines.ApplyRemovals();
		}
	}
}
