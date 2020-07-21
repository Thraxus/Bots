using System;
using Bots.GridControl.DataTypes.Enums;
using VRage.GameServices;
using VRageMath;

namespace Bots.GridControl.DataTypes
{
	public class ThrusterSettings
	{
		public Vector3D Thrust = Vector3D.Zero;
		public Vector3D Dampners = Vector3D.Zero;

		public void SetThrust(Vector3D thrust)
		{
			Thrust = thrust;
		}

		public void SetDampners(Vector3D dampners)
		{
			Dampners = dampners;
		}

		//  X thrust right
		//  Y thrust up
		//	Z thrust in forward

		public bool BlockUpDampner;
		public bool BlockDownDampner;
		public bool BlockLeftDampner;
		public bool BlockRightDampner;
		public bool BlockForwardDampner;
		public bool BlockBackDampner;

		public double GetRightLeftThrust()
		{
			if (BlockRightDampner && Dampners.X > 0) return Thrust.X;
			if (BlockLeftDampner && Dampners.X < 0) return Thrust.X;
			return Thrust.X + Dampners.X;
		}

		public double GetUpDownThrust()
		{
			if (BlockUpDampner && Dampners.Y > 0) return Thrust.Y;
			if (BlockDownDampner && Dampners.Y < 0) return Thrust.Y;
			return Thrust.Y + Dampners.Y;
		}
	
		public double GetForwardBackThrust()
		{
			if (BlockForwardDampner && Thrust.Z > 0) return Thrust.Z;
			if (BlockBackDampner && Thrust.Z < 0) return Thrust.Z;
			return Thrust.Z + Dampners.Z;
		}
	}
}
