using System.Collections.Generic;
using Bots.Common.Models;
using Bots.GridControl.DataTypes.Enums;

namespace Bots.GridControl.Models
{
	public class PidController
	{
		// https://en.wikipedia.org/wiki/PID_controller
		// https://www.habrador.com/tutorials/pid-controller/1-car-follow-path/
		// https://www.habrador.com/tutorials/pid-controller/3-stabilize-quadcopter/
		// https://www.habrador.com/tutorials/math/3-turn-left-or-right/
		// https://blog.habrador.com/2015/11/explaining-hybrid-star-pathfinding.html
		// http://robotsforroboticists.com/pid-control/

		public double Kp = 2;	// Proportional Gain
		public double Ki = 0;	// Integral Gain
		public double Kd = 0.01; // Derivative Gain

		private long _stepLength = 5; // Ticks between recalculations
		private long _totalSteps; // How many times has this run

		private readonly FastQueue<double> _pastErrorQueue = new FastQueue<double>(3);
		private double _previousIntegral = 0;

		private double _bias = 0;
		
		// Limits if desired (stops gains from exceeding some number)
		private double _limitMax;
		private double _limitMin;

		// Dead band if desired (when it gets here, just assume 0)
		private double _deadMax;
		private double _deadMin;

		// defaults, should be set proper though
		private const double GyroClampMax = 1f;
		private const double GyroClampMin = -1f;

		// defaults, should be set proper though
		private const double ThrusterClampMax = float.MaxValue;
		private const double ThrusterClampMin = float.MinValue;

		private double _max;
		private double _min;
		private double _lastError = 0;

		private readonly PidType _type;

		public PidController()
		{
			
		}

		public PidController(PidType type)
		{
			_type = type;
			_max = type == PidType.Gyro ? GyroClampMax : ThrusterClampMax;
			_min = type == PidType.Gyro ? GyroClampMin : ThrusterClampMin;
		}

		public void Reset()
		{
			_stepLength = 0;
			_totalSteps = 0;
			_pastErrorQueue.Clear();
		}

		public void SetStepLength(long length)
		{
			_stepLength = length;
		}

		public void SetPidKs(double kp, double ki, double kd)
		{
			Kp = kp;
			Ki = ki;
			Kd = kd;
		}

		public void SetLimits(double max, double min)
		{
			_max = max;
			_min = min;
		}

		private double Output(double error, bool useDeadBand = false, bool enforceLimit = false)
		{
			double pastErrors = AverageSumPastErrors();
			
			// I
			double integral = _previousIntegral + (error * _stepLength);

			// D
			//double derivative = (error - pastErrors) / _stepLength;
			double derivative = (error - _lastError) / _stepLength;

			// P
			double output = Kp * error + (Ki * integral) + (Kd * derivative) + _bias;

			_pastErrorQueue.Enqueue(error);
			_previousIntegral = integral;
			_totalSteps++;
			_lastError = error;

			//if (useDeadBand && enforceLimit) return IsBetween(output, _deadMax, _deadMin) ? 0 : Clamp(output, _limitMax, _limitMin);
			//if (useDeadBand) return IsBetween(output, _deadMax, _deadMin) ? 0 : output;
			//return enforceLimit ? Clamp(output, _limitMax, _limitMin) : output;
			return output;
		}
		
		private double AverageSumPastErrors()
		{
			if (_pastErrorQueue.Count == 0) return 0;
			double sum = 0;
			for (int i = 0; i < _pastErrorQueue.Count -1; i++)
			{
				double num = _pastErrorQueue.Dequeue();
				sum += num * _stepLength;
				_pastErrorQueue.Enqueue(num);
			}
			return sum / _pastErrorQueue.Count;
		}
		
		private double Clamp(double value)
		{
			return value > _max ? _max : value < _min ? _min : value;
		}

		private static double Clamp(double value, double max, double min)
		{
			return value > max ? max : value < min ? min : value;
		}

		private static bool IsBetween(double value, double max, double min)
		{
			return value > min && value < max;
		}
	}
}