using Bots.Common.BaseClasses;
using Bots.Common.Utilities.Statics;
using Bots.GridControl.DataTypes.Enums;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.ModAPI;

namespace Bots.GridControl.Models
{
	public class ControllableThruster : BaseClosableClass
	{
		private readonly IMyThrust _thisIThruster;
		private readonly MyThrust _thisThruster;
		private readonly MyThrustDefinition _thisDefinition;
		public readonly ThrustDirection ThrustDirection;
		public float AdjustedMaxThrust;


		public ControllableThruster(IMyThrust thisThruster, ThrustDirection thisDirection)
		{
			_thisIThruster = thisThruster;
			ThrustDirection = thisDirection;
			_thisThruster = (MyThrust) thisThruster;
			_thisDefinition = _thisThruster.BlockDefinition;
			_thisThruster.OnClose += Close;
		}

		private void Close(IMyEntity thruster)
		{
			base.Close();
			_thisThruster.OnClose -= Close;
		}


		public float MaxPower()
		{
			return _thisThruster.MaxPowerConsumption;
		}

		public float CalculateAdjustedMaxPower(bool inAtmosphere)
		{
			return ThrusterCalculations.AdjustedMaxPower(_thisThruster, inAtmosphere);
		}

		public float MaxThrust()
		{
			if (!_thisIThruster.IsWorking) return 0;
			return _thisIThruster.MaxEffectiveThrust;
		}
		
		public float CurrentThrust()
		{
			if (!_thisIThruster.IsWorking) return 0;
			return _thisIThruster.ThrustOverride;
		}

		public void SetThrust(float value)
		{
			if (value < 0) return;
			_thisIThruster.ThrustOverride = value;
		}
	}
}