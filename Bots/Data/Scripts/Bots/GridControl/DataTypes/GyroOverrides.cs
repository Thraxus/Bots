using Bots.GridControl.Controllers.WhipsCode;

namespace Bots.GridControl.DataTypes
{
	public class GyroOverrides
	{
		// when i get to PIDs, they will be setup here
		private Pid _pitchPID;
		private Pid _yawPID;
		private Pid _rollPID;

		private double _yaw = 0;
		private double _pitch = 0;
		private double _roll = 0;

		public void SetYaw(double yaw)
		{
			_yaw = yaw;
		}

		public void SetPitch(double pitch)
		{
			_pitch = pitch;
		}

		public void SetRoll(double roll)
		{
			_roll = roll;
		}

		public double GetYaw()
		{
			return _yaw;
		}

		public double GetPitch()
		{
			return _pitch;
		}

		public double GetRoll()
		{
			return _roll;
		}
	}
}
