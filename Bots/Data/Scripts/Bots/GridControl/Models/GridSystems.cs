using System.Collections.Generic;
using Bots.Common.BaseClasses;
using Bots.Common.Interfaces;
using Bots.GridControl.Interfaces;
using Sandbox.ModAPI;
using ILog = Bots.GridControl.Interfaces.ILog;

namespace Bots.GridControl.Models
{
	public class GridSystems : BaseLoggingClass, IUpdate, ILog
	{
		protected override string Id { get; } = "GridSystems";

		internal readonly ControllableGyros ControllableGyros;
		internal readonly ControllableThrusters ControllableThrusters;
		internal readonly ControllableLandingGear ControllableLandingGear;
		internal readonly DebugScreens DebugScreens = new DebugScreens();

		private readonly List<ILog> _closeUs = new List<ILog>();

		public GridSystems(IMyShipController thisController)
		{
			ControllableGyros = new ControllableGyros(thisController);
			ControllableGyros.OnWriteToLog += WriteToLog;
			ControllableThrusters = new ControllableThrusters(thisController);
			ControllableThrusters.OnWriteToLog += WriteToLog;
			ControllableLandingGear = new ControllableLandingGear(thisController);
			_closeUs.Add(ControllableGyros);
			_closeUs.Add(ControllableThrusters);
			_closeUs.Add(ControllableLandingGear);
			_closeUs.Add(DebugScreens);
		}

		public void Update(long tick)
		{
			
		}

		public override void Close()
		{
			base.Close();
			foreach (ILog closeThis in _closeUs)
			{
				closeThis.OnWriteToLog -= WriteToLog;
				closeThis.Close();
			}
		}
	}
}
