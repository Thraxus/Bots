using Bots.Common;
using Bots.GridControl.Controllers.WhipsCode;

namespace Bots.GridControl.DataTypes
{
	public class GyroOverrides
	{
		private const double Kp = 2;
		private const double Ki = 0;
		private const double Kd = 0.1;

		// when i get to PIDs, they will be setup here
		private readonly Pid _pitchPid = new Pid(Kp, Ki, Kd, 0.1, 1.0 / Settings.TicksPerSecond);
		private readonly Pid _yawPid = new Pid(Kp, Ki, Kd, 0.1, 1.0 / Settings.TicksPerSecond);
		private readonly Pid _rollPid = new Pid(Kp, Ki, Kd, 0.1, 1.0 / Settings.TicksPerSecond);

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
			return _yawPid.Control(_yaw);
		}

		public double GetPitch()
		{
			return _pitchPid.Control(_pitch);
		}

		public double GetRoll()
		{
			return _rollPid.Control(_roll);
		}
	}
}
