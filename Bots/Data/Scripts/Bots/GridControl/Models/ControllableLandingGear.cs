using Bots.Common.BaseClasses;
using Bots.GridControl.Interfaces;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Collections;
using VRage.ModAPI;

namespace Bots.GridControl.Models
{
	internal class ControllableLandingGear : BaseLoggingClass, ILog
	{
		protected override string Id { get; } = "ControllableLandingGear";

		private readonly ConcurrentCachingList<IMyLandingGear> _landingGears = new ConcurrentCachingList<IMyLandingGear>();
		private readonly IMyShipController _thisController;

		public ControllableLandingGear(IMyShipController thisController)
		{
			_thisController = thisController;
		}

		public void Add(IMyLandingGear gear)
		{
			gear.OnClose += Close;
			_landingGears.Add(gear);
			_landingGears.ApplyAdditions();
		}

		private void Close(IMyEntity gear)
		{
			((IMyLandingGear)gear).OnClose -= Close;
			_landingGears.Remove((IMyLandingGear) gear);
			_landingGears.ApplyRemovals();
		}

		public override void Close()
		{
			base.Close();
			_landingGears.ClearList();
		}
	}
}