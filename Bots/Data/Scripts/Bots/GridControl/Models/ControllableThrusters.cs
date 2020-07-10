using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Bots.Common.BaseClasses;
using Bots.Common.Enums;
using Bots.GridControl.DataTypes.Enums;
using Bots.GridControl.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRageMath;

namespace Bots.GridControl.Models
{
	public class ControllableThrusters : BaseLoggingClass, IUpdate, ILog
	{

		// TODO: Add a balanced option that distributes the force to all thrusters, not just one
		//			One just makes it easier to debug since i can still fly a ship manually, will likely use balanced for release
		protected override string Id { get; } = "ControllableThrusters";

		private readonly MyConcurrentDictionary<ThrustDirection, ConcurrentCachingList<ControllableThruster>> _thrusters = new MyConcurrentDictionary<ThrustDirection, ConcurrentCachingList<ControllableThruster>>();
		private readonly MyConcurrentDictionary<ThrustDirection, float> _maxEffectiveThrust = new MyConcurrentDictionary<ThrustDirection, float>()
		{
			{ ThrustDirection.Forward, 0 },
			{ ThrustDirection.Back, 0 },
			{ ThrustDirection.Up, 0 },
			{ ThrustDirection.Down, 0 },
			{ ThrustDirection.Left, 0 },
			{ ThrustDirection.Right, 0 },
		};
		private readonly MyConcurrentDictionary<ThrustDirection, float> _currentlyUtilizedThrust = new MyConcurrentDictionary<ThrustDirection, float>()
		{
			{ ThrustDirection.Forward, 0 },
			{ ThrustDirection.Back, 0 },
			{ ThrustDirection.Up, 0 },
			{ ThrustDirection.Down, 0 },
			{ ThrustDirection.Left, 0 },
			{ ThrustDirection.Right, 0 },
		};

		private readonly IMyShipController _thisIController;
		private readonly MyShipController _thisController;
		private readonly MyCubeGrid _thisCubeGrid;

		public event Action<ThrustDirection> InsufficientThrustAvailable;
		
		public ControllableThrusters(IMyShipController controller)
		{
			_thisIController = controller;
			_thisController = (MyShipController) controller;
			_thisCubeGrid = _thisController.CubeGrid;
			_thisController.ControlThrusters = true;
			controller.GetNaturalGravity();
			_thrusters.Add(ThrustDirection.Forward, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(ThrustDirection.Back, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(ThrustDirection.Up, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(ThrustDirection.Down, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(ThrustDirection.Left, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(ThrustDirection.Right, new ConcurrentCachingList<ControllableThruster>());
		}

		public void AddNewThruster(MyThrust myThrust)
		{
			if (myThrust == null || _thisIController == null) return;
			ControllableThruster thruster = null;
			if (_thisIController.WorldMatrix.Forward * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, ThrustDirection.Forward);
			if (_thisIController.WorldMatrix.Backward * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, ThrustDirection.Back);
			if(_thisIController.WorldMatrix.Left * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, ThrustDirection.Left);
			if(_thisIController.WorldMatrix.Right * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, ThrustDirection.Right);
			if(_thisIController.WorldMatrix.Up * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, ThrustDirection.Up);
			if (_thisIController.WorldMatrix.Down * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, ThrustDirection.Down);
			if (thruster == null) return;
			thruster.OnClose += CloseThruster;
			_thrusters[thruster.ThrustDirection].Add(thruster);
			_thrusters[thruster.ThrustDirection].ApplyAdditions();
			RecalculateMaxEffectiveThrust();
		}

		private void CloseThruster(BaseClosableClass thruster)
		{
			thruster.OnClose -= CloseThruster;
			ControllableThruster closedThruster = (ControllableThruster) thruster;
			_thrusters[closedThruster.ThrustDirection].Remove(closedThruster);
			_thrusters[closedThruster.ThrustDirection].ApplyRemovals();
			RecalculateMaxEffectiveThrust();
		}

		public void RecalculateMaxEffectiveThrust()
		{
			if (IsClosed) return;
			ResetMaxEffectiveThrust();
			ResetUtilizedThrust();
			foreach (KeyValuePair<ThrustDirection, ConcurrentCachingList<ControllableThruster>> thrusterType in _thrusters)
			{
				foreach (ControllableThruster thruster in thrusterType.Value)
				{
					_maxEffectiveThrust[thruster.ThrustDirection] += thruster.MaxThrust();
					_currentlyUtilizedThrust[thruster.ThrustDirection] += thruster.CurrentThrust();
				}
			}
			RecalculateUtilizedThrust();
		}

		private bool _customDampnersEnabled;

		public void EnableCustomDampners()
		{
			if (_thisIController.EnabledDamping) _thisIController.SwitchDamping();
			_customDampnersEnabled = true;
			
		}

		public void DisableCustomDampners()
		{
			if (!_thisIController.EnabledDamping) _thisIController.SwitchDamping();
			_customDampnersEnabled = false;
		}

		private void CustomDampners()
		{
			if (!_customDampnersEnabled) return;
			double mass = _thisCubeGrid.Mass;// _thisController.CalculateShipMass().PhysicalMass;
			Vector3D gravity = _thisController.GetNaturalGravity();
			Vector3D gravityNormalized = Vector3D.Normalize(gravity);
			double requiredThrust = mass * gravity.Length();
			double forwardThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _thisController.WorldMatrix.Forward);
			double upThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _thisController.WorldMatrix.Up);
			double rightThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _thisController.WorldMatrix.Right);
			double arrestForwardMovement = mass * forwardThrust;
			double arrestUpMovement = mass * upThrust;
			double arrestRightMovement = mass * rightThrust;
			double forwardRatio = -Vector3D.Dot(Vector3D.Normalize(_thisController.WorldMatrix.Forward), gravityNormalized);
			double upRatio = -Vector3D.Dot(Vector3D.Normalize(_thisController.WorldMatrix.Up), gravityNormalized);
			double rightRatio = -Vector3D.Dot(Vector3D.Normalize(_thisController.WorldMatrix.Right), gravityNormalized);

			//SetDampnerThrust(ThrustDirection.Forward, (requiredThrust * forwardRatio) - arrestForwardMovement);
			//SetDampnerThrust(ThrustDirection.Up, (requiredThrust * upRatio) - arrestUpMovement);
			//SetDampnerThrust(ThrustDirection.Right, (requiredThrust * rightRatio) - arrestRightMovement);

			Vector3D thrust = new Vector3D(
				(requiredThrust * rightRatio) - arrestRightMovement,
				(requiredThrust * upRatio) - arrestUpMovement, 
				(requiredThrust * forwardRatio) - arrestForwardMovement);

			SetDampnerThrust(thrust);
			// -Z will be thrust in forward
			//  Y will be thrust up
			//  X will be thrust right
		}

		private void RecalculateUtilizedThrust()
		{
			ResetUtilizedThrust();
			foreach (KeyValuePair<ThrustDirection, ConcurrentCachingList<ControllableThruster>> thrust in _thrusters)
			{
				foreach (ControllableThruster thruster in thrust.Value)
				{
					_currentlyUtilizedThrust[thrust.Key] += thruster.CurrentThrust();
				}
			}
		}

		private void ResetMaxEffectiveThrust()
		{
			_maxEffectiveThrust[ThrustDirection.Forward] = 0;
			_maxEffectiveThrust[ThrustDirection.Back] = 0;
			_maxEffectiveThrust[ThrustDirection.Up] = 0;
			_maxEffectiveThrust[ThrustDirection.Down] = 0;
			_maxEffectiveThrust[ThrustDirection.Left] = 0;
			_maxEffectiveThrust[ThrustDirection.Right] = 0;
		}

		private void ResetUtilizedThrust()
		{
			_currentlyUtilizedThrust[ThrustDirection.Forward] = 0;
			_currentlyUtilizedThrust[ThrustDirection.Back] = 0;
			_currentlyUtilizedThrust[ThrustDirection.Up] = 0;
			_currentlyUtilizedThrust[ThrustDirection.Down] = 0;
			_currentlyUtilizedThrust[ThrustDirection.Left] = 0;
			_currentlyUtilizedThrust[ThrustDirection.Right] = 0;
		}
		
		public float GetMaxEffectiveThrustInDirection(ThrustDirection direction)
		{
			RecalculateMaxEffectiveThrust();
			return _maxEffectiveThrust[direction];
		}

		public void ResetThrust()
		{
			foreach (KeyValuePair<ThrustDirection, ConcurrentCachingList<ControllableThruster>> thrusters in _thrusters)
			{
				SetBalancedThrust(thrusters.Key, ThrustPower.None);
			}
		}

		public void SetThrust(ThrustDirection direction, ThrustPower power)
		{
			SetBalancedThrust(direction, power);
		}

		private void SetDampnerThrust(Vector3D vector)
		{
			WriteToLog("SetDampnerThrust", $"Setting Thrust {vector}", LogType.General);
			// -Z will be thrust in forward
			//  Y will be thrust up
			//  X will be thrust right

			if (vector.X > 0)
			{
				DampnerThrust(ThrustDirection.Right, vector.X);
				DampnerThrust(ThrustDirection.Left, 0);
			}
			else if (vector.X < 0)
			{
				DampnerThrust(ThrustDirection.Left, -vector.X);
				DampnerThrust(ThrustDirection.Right, 0);
			}
			else
			{
				DampnerThrust(ThrustDirection.Left, 0);
				DampnerThrust(ThrustDirection.Right, 0);
			}

			if (vector.Y > 0)
			{
				DampnerThrust(ThrustDirection.Up, vector.Y);
				DampnerThrust(ThrustDirection.Down, 0);
			}
			else if (vector.Y < 0)
			{
				DampnerThrust(ThrustDirection.Down, -vector.Y);
				DampnerThrust(ThrustDirection.Up, 0);
			}
			else
			{
				DampnerThrust(ThrustDirection.Up, 0);
				DampnerThrust(ThrustDirection.Down, 0);
			}

			if (vector.Z > 0)
			{
				DampnerThrust(ThrustDirection.Forward, vector.Z);
				DampnerThrust(ThrustDirection.Back, 0);
			}
			else if (vector.Z < 0)
			{
				DampnerThrust(ThrustDirection.Back, -vector.Z);
				DampnerThrust(ThrustDirection.Forward, 0);
			}
			else
			{
				DampnerThrust(ThrustDirection.Forward, 0);
				DampnerThrust(ThrustDirection.Back, 0);
			}
		}

		private void SetDampnerThrust(ThrustDirection direction, double value)
		{
			if (double.IsNaN(value)) return;
			if (Math.Sign(value) == -1)
			{
				value = Math.Abs(value);
				// If we're requesting negative thrust on this axis, then any 
				//	thrust on this axis in the requested direction needs to be nullified
				// Remember, this is a set, not an accumulated value
				DampnerThrust(direction, 0);
				switch (direction)
				{
					case ThrustDirection.Up:
						direction = ThrustDirection.Down;
						break;
					case ThrustDirection.Down:
						direction = ThrustDirection.Up;
						break;
					case ThrustDirection.Left:
						direction = ThrustDirection.Right;
						break;
					case ThrustDirection.Right:
						direction = ThrustDirection.Left;
						break;
					case ThrustDirection.Forward:
						direction = ThrustDirection.Back;
						break;
					case ThrustDirection.Back:
						direction = ThrustDirection.Forward;
						break;
					default:
						return; // something is broken if this ever happens, so... ignore it. 
				}
			}
			DampnerThrust(direction, value);
			WriteToLog("SetDampnerThrust", $"Setting Thrust {direction} {value}", LogType.General);
		}

		private void SetRollingThrust(ThrustDirection direction, double value)
		{
			if (double.IsNaN(value)) return;
			if (IsClosed) return;
			double tmpValue = value;
			_currentlyUtilizedThrust[direction] = 0;
			foreach (ControllableThruster thruster in _thrusters[direction])
			{
				if (Math.Abs(value) <= 0)
				{
					thruster.SetThrust(0);
					continue;
				}

				float availableThrust = thruster.MaxThrust() - thruster.CurrentThrust();
				if (availableThrust <= 0)
					continue;

				if (availableThrust > tmpValue)
				{
					thruster.SetThrust((float) (thruster.CurrentThrust() + tmpValue));
					tmpValue = 0;
				}
				else
				{
					thruster.SetThrust(thruster.MaxThrust());
					tmpValue -= thruster.MaxThrust();
				}
				if (tmpValue > 0) continue;
				thruster.SetThrust(0);
			}
			if (tmpValue > 0) 
			{
				InsufficientThrustAvailable?.Invoke(direction);
				_currentlyUtilizedThrust[direction] = (float) (value - tmpValue);
				return;
			}
			_currentlyUtilizedThrust[direction] = (float) value;
		}

		private readonly List<ControllableThruster> _passTwoThrusters = new List<ControllableThruster>();

		private void SetBalancedThrust(ThrustDirection direction, ThrustPower power)
		{
			if (IsClosed) return;
			foreach (ControllableThruster thruster in _thrusters[direction])
			{
				float thrusterPower = thruster.MaxThrust();
				switch (power)
				{
					case ThrustPower.None:
						thrusterPower = 0;
						break;
					case ThrustPower.Full:
						break;
					case ThrustPower.Half:
						thrusterPower *= 0.5f;
						break;
					case ThrustPower.Quarter:
						thrusterPower *= 0.25f;
						break;
					case ThrustPower.TenPercent:
						thrusterPower *= 0.1f;
						break;
					case ThrustPower.ThreeQuarters:
						thrusterPower *= 0.75f;
						break;
					default:
						thrusterPower = 0;
						break;
				}
				thruster.SetThrust(thrusterPower);
			}
		}

		private void DampnerThrust(ThrustDirection direction, double value)
		{
			if (IsClosed) return;
			WriteToLog("DampnerThrust",$"{direction} {value:F6}",LogType.General);
			_passTwoThrusters.Clear();
			//if (_maxEffectiveThrust[direction] - _currentlyUtilizedThrust[direction] > value) InsufficientThrustAvailable?.Invoke(direction);
			int thrusterCount = _thrusters[direction].Count;
			double tmpValue = value, val = value / thrusterCount;
			foreach (ControllableThruster thruster in _thrusters[direction])
			{
				if (thruster.MaxThrust() < val)
				{
					thruster.SetThrust(thruster.MaxThrust());
					tmpValue -= thruster.MaxThrust();
				}
				_passTwoThrusters.Add(thruster);
				thruster.SetThrust((float) val);
				tmpValue -= val;
			}
			//_currentlyUtilizedThrust[direction] = (float) value;
			if (tmpValue <= 0 || _passTwoThrusters.Count == 0) return;
			val = tmpValue / _passTwoThrusters.Count;
			WriteToLog("DampnerThrust-PassTwo", $"{direction} {val:F6} | {tmpValue:F6} | {_passTwoThrusters.Count}", LogType.General);
			foreach (ControllableThruster thruster in _passTwoThrusters)
			{
				thruster.SetThrust((float) (thruster.CurrentThrust() + val));
			}
			_passTwoThrusters.Clear();
		}

		public void Update(long tick)
		{
			if (tick % 10 != 0) return;
			CustomDampners();
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			foreach (var thrustCollection in _maxEffectiveThrust)
			{
				sb.Append($"{thrustCollection.Key} | {thrustCollection.Value}");
			}

			return sb.ToString();
		}
	}
}