using System.Text;
using Bots.Common.BaseClasses;
using Bots.GridControl.Interfaces;
using Sandbox.ModAPI;
using VRage.Game.GUI.TextPanel;

namespace Bots.GridControl.Models
{
	public class DebugScreens : BaseLoggingClass, ILog
	{
		protected override string Id { get; } = "DebugScreens";

		private IMyTextSurface _leftSurface;
		private IMyTextSurface _rightSurface;

		public void AddLeftScreen(IMyTextSurface leftSurface)
		{
			if (_leftSurface != null) return;
			_leftSurface = leftSurface;
			_leftSurface.ContentType = ContentType.TEXT_AND_IMAGE;
		}

		public void AddRightScreen(IMyTextSurface rightSurface)
		{
			if (_rightSurface != null) return;
			_rightSurface = rightSurface;
			_rightSurface.ContentType = ContentType.TEXT_AND_IMAGE;
		}

		readonly StringBuilder _builderLeft = new StringBuilder();
		public void WriteToLeft(string value, bool append = false)
		{
			_builderLeft.Clear();
			_builderLeft.Append(value);
			_leftSurface?.WriteText(_builderLeft, append);
		}

		readonly StringBuilder _builderRight = new StringBuilder();
		public void WriteToRight(string value, bool append = false)
		{
			_builderRight.Clear();
			_builderRight.Append(value);
			_rightSurface?.WriteText(value, append);
		}
	}
}
