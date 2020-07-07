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

		public event Action<ThrustDirection> InsufficientThrustAvailable;
		
		public ControllableThrusters(IMyShipController controller)
		{
			_thisIController = controller;
			_thisController = (MyShipController) controller;
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
				SetBalancedThrust(thrusters.Key, 0);
			}
		}

		public void SetThrust(ThrustDirection direction, float value)
		{
			if (Math.Sign(value) < 0)
			{
				value = Math.Abs(value);
				// If we're requesting negative thrust on this axis, then any 
				//	thrust on this axis in the requested direction needs to be nullified
				// Remember, this is a set, not an accumulated value
				SetRollingThrust(direction, 0);
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
			SetBalancedThrust(direction, value);
			WriteToLog("SetThrust", $"Setting Thrust {direction} {value}", LogType.General);
		}

		private void SetRollingThrust(ThrustDirection direction, float value)
		{
			if (IsClosed) return;
			float tmpValue = value;
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
					thruster.SetThrust(thruster.CurrentThrust() + tmpValue);
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
				_currentlyUtilizedThrust[direction] = value - tmpValue;
				return;
			}
			_currentlyUtilizedThrust[direction] = value;
		}

		private readonly List<ControllableThruster> _passTwoThrusters = new List<ControllableThruster>();

		private void SetBalancedThrust(ThrustDirection direction, float value)
		{
			if (IsClosed) return;
			_passTwoThrusters.Clear();
			if (_maxEffectiveThrust[direction] - _currentlyUtilizedThrust[direction] > value) InsufficientThrustAvailable?.Invoke(direction);
			int thrusterCount = _thrusters[direction].Count;
			float tmpValue = value, val = value / thrusterCount;
			foreach (ControllableThruster thruster in _thrusters[direction])
			{
				if (thruster.MaxThrust() < val)
				{
					thruster.SetThrust(thruster.MaxThrust());
					tmpValue -= thruster.MaxThrust();
					continue;
				}
				_passTwoThrusters.Add(thruster);
				thruster.SetThrust(val);
				tmpValue -= val;
			}
			_currentlyUtilizedThrust[direction] = value;
			if (tmpValue <= 0 || _passTwoThrusters.Count == 0) return;
			val = tmpValue / _passTwoThrusters.Count;
			WriteToLog("SetBalancedThrust", $"{val} | {tmpValue} | {_passTwoThrusters.Count}",LogType.General);
			foreach (ControllableThruster thruster in _passTwoThrusters)
			{
				thruster.SetThrust(thruster.CurrentThrust() + val);
			}
			_passTwoThrusters.Clear();
		}

		public void Update(long tick)
		{
			
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