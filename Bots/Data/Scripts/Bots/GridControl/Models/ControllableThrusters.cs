using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Bots.Common.BaseClasses;
using Bots.Common.Enums;
using Bots.GridControl.DataTypes;
using Bots.GridControl.DataTypes.Enums;
using Bots.GridControl.Interfaces;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRageMath;

namespace Bots.GridControl.Models
{
	public class ControllableThrusters : BaseLoggingClass, IUpdate, ILog
	{

		// TODO: Add a balanced option that distributes the force to all thrusters, not just one
		//			One just makes it easier to debug since i can still fly a ship manually, will likely use balanced for release
		protected override string Id { get; } = "ControllableThrusters";

		private readonly MyConcurrentDictionary<MovementDirection, ConcurrentCachingList<ControllableThruster>> _thrusters = new MyConcurrentDictionary<MovementDirection, ConcurrentCachingList<ControllableThruster>>();
		private readonly MyConcurrentDictionary<MovementDirection, float> _maxEffectiveThrust = new MyConcurrentDictionary<MovementDirection, float>()
		{
			{ MovementDirection.Forward, 0 },
			{ MovementDirection.Back, 0 },
			{ MovementDirection.Up, 0 },
			{ MovementDirection.Down, 0 },
			{ MovementDirection.Left, 0 },
			{ MovementDirection.Right, 0 },
		};
		private readonly MyConcurrentDictionary<MovementDirection, float> _currentlyUtilizedThrust = new MyConcurrentDictionary<MovementDirection, float>()
		{
			{ MovementDirection.Forward, 0 },
			{ MovementDirection.Back, 0 },
			{ MovementDirection.Up, 0 },
			{ MovementDirection.Down, 0 },
			{ MovementDirection.Left, 0 },
			{ MovementDirection.Right, 0 },
		};

		private readonly IMyShipController _thisIController;
		private readonly MyShipController _thisController;
		private readonly MyCubeGrid _thisCubeGrid;

		private readonly List<ControllableThruster> _passTwoThrusters = new List<ControllableThruster>();

		public event Action<MovementDirection> InsufficientThrustAvailable;

		public ThrusterSettings ThrusterSettings = new ThrusterSettings();

		private bool _customDampnersEnabled;

		private ShipMatrixTranslation _currentShipWorldMatrix;
		private MatrixD _originalShipWorldMatrix;


		private readonly double _largeShipMaxAngularSpeed = MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxAngularSpeed;
		private readonly double _largeShipMaxSpeed = MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed;
		private readonly double _smallShipMaxAngularSpeed = MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxAngularSpeed;
		private readonly double _smallShipMaxSpeed = MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;

		// Velocity is in m/s
		private Vector3D _desiredVelocity = Vector3D.Zero;

		// Dampners is in kN
		private Vector3D _dampners = Vector3D.Zero;


		public ControllableThrusters(IMyShipController controller)
		{
			_thisIController = controller;
			_thisController = (MyShipController) controller;
			_thisCubeGrid = _thisController.CubeGrid;
			_thisController.ControlThrusters = true;
			SetShipWorldMatrix(ShipMatrixTranslation.Original);
			_originalShipWorldMatrix = _thisController.WorldMatrix;
			_thrusters.Add(MovementDirection.Forward, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(MovementDirection.Back, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(MovementDirection.Up, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(MovementDirection.Down, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(MovementDirection.Left, new ConcurrentCachingList<ControllableThruster>());
			_thrusters.Add(MovementDirection.Right, new ConcurrentCachingList<ControllableThruster>());
		}

		public void AddNewThruster(MyThrust myThrust)
		{
			if (myThrust == null || _thisIController == null) return;
			ControllableThruster thruster = null;
			if (_originalShipWorldMatrix.Forward * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, MovementDirection.Forward);
			if (_originalShipWorldMatrix.Backward * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, MovementDirection.Back);
			if(_originalShipWorldMatrix.Left * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, MovementDirection.Left);
			if(_originalShipWorldMatrix.Right * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, MovementDirection.Right);
			if(_originalShipWorldMatrix.Up * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, MovementDirection.Up);
			if (_originalShipWorldMatrix.Down * -1 == myThrust.WorldMatrix.Forward)
				thruster = new ControllableThruster(myThrust, MovementDirection.Down);
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
			foreach (KeyValuePair<MovementDirection, ConcurrentCachingList<ControllableThruster>> thrusterType in _thrusters)
			{
				foreach (ControllableThruster thruster in thrusterType.Value)
				{
					_maxEffectiveThrust[thruster.ThrustDirection] += thruster.MaxThrust();
					_currentlyUtilizedThrust[thruster.ThrustDirection] += thruster.CurrentThrust();
				}
			}
			RecalculateUtilizedThrust();
		}
		
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
			double mass = _thisCubeGrid.Mass;
			Vector3D gravity = _thisController.GetNaturalGravity();
			Vector3D gravityNormalized = Vector3D.Normalize(gravity);
			double requiredThrust = mass * gravity.Length();
			//double forwardThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _thisController.WorldMatrix.Forward);
			//double upThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _thisController.WorldMatrix.Up);
			//double rightThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _thisController.WorldMatrix.Right);
			double forwardThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _shipWorldMatrixD.Forward);
			double upThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _shipWorldMatrixD.Up);
			double rightThrust = Vector3D.Dot(_thisIController.GetShipVelocities().LinearVelocity, _shipWorldMatrixD.Right);
			double arrestForwardMovement = mass * forwardThrust;
			double arrestUpMovement = mass * upThrust;
			double arrestRightMovement = mass * rightThrust;
			//double forwardRatio = -Vector3D.Dot(Vector3D.Normalize(_thisController.WorldMatrix.Forward), gravityNormalized);
			//double upRatio = -Vector3D.Dot(Vector3D.Normalize(_thisController.WorldMatrix.Up), gravityNormalized);
			//double rightRatio = -Vector3D.Dot(Vector3D.Normalize(_thisController.WorldMatrix.Right), gravityNormalized);
			double forwardRatio = -Vector3D.Dot(Vector3D.Normalize(_shipWorldMatrixD.Forward), gravityNormalized);
			double upRatio = -Vector3D.Dot(Vector3D.Normalize(_shipWorldMatrixD.Up), gravityNormalized);
			double rightRatio = -Vector3D.Dot(Vector3D.Normalize(_shipWorldMatrixD.Right), gravityNormalized);

			Vector3D thrust = new Vector3D(
				(requiredThrust * rightRatio) - arrestRightMovement,
				(requiredThrust * upRatio) - arrestUpMovement, 
				(requiredThrust * forwardRatio) - arrestForwardMovement);

			_dampners = thrust;

			//ThrusterSettings.SetDampners(thrust); _shipWorldMatrixD

			//SetDampnerThrust(thrust);
			// -Z will be thrust in forward
			//  Y will be thrust up
			//  X will be thrust right
		}

		private void RecalculateUtilizedThrust()
		{
			ResetUtilizedThrust();
			foreach (KeyValuePair<MovementDirection, ConcurrentCachingList<ControllableThruster>> thrust in _thrusters)
			{
				foreach (ControllableThruster thruster in thrust.Value)
				{
					_currentlyUtilizedThrust[thrust.Key] += thruster.CurrentThrust();
				}
			}
		}

		private void ResetMaxEffectiveThrust()
		{
			_maxEffectiveThrust[MovementDirection.Forward] = 0;
			_maxEffectiveThrust[MovementDirection.Back] = 0;
			_maxEffectiveThrust[MovementDirection.Up] = 0;
			_maxEffectiveThrust[MovementDirection.Down] = 0;
			_maxEffectiveThrust[MovementDirection.Left] = 0;
			_maxEffectiveThrust[MovementDirection.Right] = 0;
		}

		private void ResetUtilizedThrust()
		{
			_currentlyUtilizedThrust[MovementDirection.Forward] = 0;
			_currentlyUtilizedThrust[MovementDirection.Back] = 0;
			_currentlyUtilizedThrust[MovementDirection.Up] = 0;
			_currentlyUtilizedThrust[MovementDirection.Down] = 0;
			_currentlyUtilizedThrust[MovementDirection.Left] = 0;
			_currentlyUtilizedThrust[MovementDirection.Right] = 0;
		}
		
		public float GetMaxEffectiveThrustInDirection(MovementDirection direction)
		{
			RecalculateMaxEffectiveThrust();
			return _maxEffectiveThrust[direction];
		}

		public void ResetThrust()
		{
			foreach (KeyValuePair<MovementDirection, ConcurrentCachingList<ControllableThruster>> thrusters in _thrusters)
			{
				SetBalancedThrust(thrusters.Key, ThrustPower.None);
			}
		}
		
		public void SetVelocity(MovementDirection direction, double desiredVelocity, bool resetOtherSpeeds = false)
		{
			if (resetOtherSpeeds)
				_desiredVelocity = Vector3D.Zero;
			if (desiredVelocity >= 999)
				desiredVelocity = _thisCubeGrid.GridSizeEnum == MyCubeSize.Small ? _smallShipMaxSpeed : _largeShipMaxSpeed;
			switch (direction)
			{
				case MovementDirection.Up:
					_desiredVelocity.Y = desiredVelocity;
					break;
				case MovementDirection.Down:
					_desiredVelocity.Y = -desiredVelocity;
					break;
				case MovementDirection.Right:
					_desiredVelocity.X = desiredVelocity;
					break;
				case MovementDirection.Left:
					_desiredVelocity.X = -desiredVelocity;
					break;
				case MovementDirection.Forward:
					_desiredVelocity.Z = desiredVelocity;
					break;
				case MovementDirection.Back:
					_desiredVelocity.Z = -desiredVelocity;
					break;
				default: // I don't know what happened here, but it's broken! 
					_desiredVelocity = Vector3D.Zero;
					break;
			}

			//SetDampnerThrust(thrust);
			// -Z will be thrust in forward
			//  Y will be thrust up
			//  X will be thrust right
		}

		private void AngularVelocity()
		{
			// this is more of a placeholder.  
			// Angular Velocity = the change in theta over some amount of time
			// Angular Velocity = theta/time
			// theta is in degrees
			// time is in seconds (1/ticks per second)
		}

		private Vector3D _finalThrust = Vector3D.Zero;

		private Vector3D LinearVelocity => _thisIController.GetShipVelocities().LinearVelocity;

		private MatrixD _shipWorldMatrixD = new MatrixD();

		private void SetThrust()
		{
			if (IsClosed) return;
			
			if (Math.Abs(_desiredVelocity.X) <= 0)
			{
				if (_desiredVelocity.X > 0)
				{   // ship has a desired velocity right
					// ignore left dampners
				}
				else
				{   // ship has a desired velocity left
					// ignore right dampners
				}
			}

			if (Math.Abs(_desiredVelocity.Y) <= 0)
			{
				if (_desiredVelocity.Y > 0)
				{   // ship has a desired velocity up
					// ignore down dampners
				}
				else
				{   // ship has a desired velocity down
					// ignore up dampners
				}
			}

			if (Math.Abs(_desiredVelocity.Z) <= 0)
			{
				if (_desiredVelocity.Z > 0)
				{	// ship has a desired velocity forward
					// ignore backwards dampners
				}
				else
				{   // ship has a desired velocity backwards
					// ignore forward dampners
				}
			}

			double thrust = ThrusterSettings.GetRightLeftThrust();
			if (thrust > 0)
				SetThrust(MovementDirection.Right, thrust);
			else if (thrust < 0) SetThrust(MovementDirection.Left, Math.Abs(thrust));
			else
			{
				SetThrust(MovementDirection.Right, thrust);
				SetThrust(MovementDirection.Left, thrust);
			}
			
			thrust = ThrusterSettings.GetUpDownThrust();
			if (thrust > 0)
				SetThrust(MovementDirection.Up, thrust);
			else if (thrust < 0) SetThrust(MovementDirection.Down, Math.Abs(thrust));
			else
			{
				SetThrust(MovementDirection.Up, thrust);
				SetThrust(MovementDirection.Down, thrust);
			}
			
			thrust = ThrusterSettings.GetForwardBackThrust();
			if (thrust > 0)
				SetThrust(MovementDirection.Forward, thrust);
			else if (thrust < 0) SetThrust(MovementDirection.Back, Math.Abs(thrust));
			else
			{
				SetThrust(MovementDirection.Forward, thrust);
				SetThrust(MovementDirection.Back, thrust);
			}
			
		}

		public void SetThrust(MovementDirection direction, ThrustPower power)
		{
			SetBalancedThrust(direction, power);
		}

		//private void SetDampnerThrust(Vector3D vector)
		//{
		//	if (vector.X > 0)
		//	{
		//		DampnerThrust(ThrustDirection.Right, vector.X);
		//		DampnerThrust(ThrustDirection.Left, 0);
		//	}
		//	else if (vector.X < 0)
		//	{
		//		DampnerThrust(ThrustDirection.Right, 0);
		//		DampnerThrust(ThrustDirection.Left, -vector.X);
		//	}
		//	else
		//	{
		//		DampnerThrust(ThrustDirection.Right, 0);
		//		DampnerThrust(ThrustDirection.Left, 0);
		//	}

		//	if (vector.Y > 0)
		//	{
		//		DampnerThrust(ThrustDirection.Up, vector.Y);
		//		DampnerThrust(ThrustDirection.Down, 0);
		//	}
		//	else if (vector.Y < 0)
		//	{
		//		DampnerThrust(ThrustDirection.Up, 0);
		//		DampnerThrust(ThrustDirection.Down, -vector.Y);
		//	}
		//	else
		//	{
		//		DampnerThrust(ThrustDirection.Up, 0);
		//		DampnerThrust(ThrustDirection.Down, 0);
		//	}

		//	if (vector.Z > 0)
		//	{
		//		DampnerThrust(ThrustDirection.Forward, vector.Z);
		//		DampnerThrust(ThrustDirection.Back, 0);
		//	}
		//	else if (vector.Z < 0)
		//	{
		//		DampnerThrust(ThrustDirection.Forward, 0);
		//		DampnerThrust(ThrustDirection.Back, -vector.Z);
		//	}
		//	else
		//	{
		//		DampnerThrust(ThrustDirection.Forward, 0);
		//		DampnerThrust(ThrustDirection.Back, 0);
		//	}
		//}

		private void SetBalancedThrust(MovementDirection direction, float thrustPercent)
		{
			if (IsClosed) return;
			double thrustPower = GetMaxEffectiveThrustInDirection(direction) * thrustPercent;
			switch(direction)
			{
				case MovementDirection.Up:
					ThrusterSettings.SetThrust(new Vector3D(0,0,thrustPower));
					break;
				case MovementDirection.Down:
					ThrusterSettings.SetThrust(new Vector3D(0, 0, -thrustPower));
					break;
				case MovementDirection.Left:
					break;
				case MovementDirection.Right:
					break;
				case MovementDirection.Forward:
					break;
				case MovementDirection.Back:
					break;
				default:
					break;
			}

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

		private void SetThrust(MovementDirection direction, double value)
		{
			if (IsClosed) return;
			WriteToLog("DampnerThrust", $"{direction} {value:F6}", LogType.General);
			_passTwoThrusters.Clear();
			//if (_maxEffectiveThrust[direction] - _currentlyUtilizedThrust[direction] > value) InsufficientThrustAvailable?.Invoke(direction);
			ConcurrentCachingList<ControllableThruster> thrusters = GetThrusterList(direction);
			int thrusterCount = thrusters.Count;
			double 
				tmpValue = value, 
				val = value / thrusterCount;
			foreach (ControllableThruster thruster in thrusters)
			{
				if (thruster.MaxThrust() < val)
				{
					thruster.SetThrust(thruster.MaxThrust());
					tmpValue -= thruster.MaxThrust();
					continue;
				}
				_passTwoThrusters.Add(thruster);
				thruster.SetThrust((float)val);
				tmpValue -= val;
			}
			//_currentlyUtilizedThrust[direction] = (float) value;
			if (tmpValue <= 0 || _passTwoThrusters.Count == 0) return;
			val = tmpValue / _passTwoThrusters.Count;
			WriteToLog("DampnerThrust-PassTwo", $"{direction} {val:F6} | {tmpValue:F6} | {_passTwoThrusters.Count}", LogType.General);
			foreach (ControllableThruster thruster in _passTwoThrusters)
			{
				thruster.SetThrust((float)(thruster.CurrentThrust() + val));
			}
			_passTwoThrusters.Clear();
		}

		//private void DampnerThrust(ThrustDirection direction, double value)
		//{
		//	if (IsClosed) return;
		//	WriteToLog("DampnerThrust",$"{direction} {value:F6}",LogType.General);
		//	_passTwoThrusters.Clear();
		//	//if (_maxEffectiveThrust[direction] - _currentlyUtilizedThrust[direction] > value) InsufficientThrustAvailable?.Invoke(direction);
		//	int thrusterCount = _thrusters[direction].Count;
		//	double tmpValue = value, val = value / thrusterCount;
		//	foreach (ControllableThruster thruster in _thrusters[direction])
		//	{
		//		if (thruster.MaxThrust() < val)
		//		{
		//			thruster.SetThrust(thruster.MaxThrust());
		//			tmpValue -= thruster.MaxThrust();
		//		}
		//		_passTwoThrusters.Add(thruster);
		//		thruster.SetThrust((float) val);
		//		tmpValue -= val;
		//	}
		//	//_currentlyUtilizedThrust[direction] = (float) value;
		//	if (tmpValue <= 0 || _passTwoThrusters.Count == 0) return;
		//	val = tmpValue / _passTwoThrusters.Count;
		//	WriteToLog("DampnerThrust-PassTwo", $"{direction} {val:F6} | {tmpValue:F6} | {_passTwoThrusters.Count}", LogType.General);
		//	foreach (ControllableThruster thruster in _passTwoThrusters)
		//	{
		//		thruster.SetThrust((float) (thruster.CurrentThrust() + val));
		//	}
		//	_passTwoThrusters.Clear();
		//}

		public void Update(long tick)
		{
			if (tick % 5 != 0) return;
			CustomDampners();
			SetThrust();
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

		private ConcurrentCachingList<ControllableThruster> GetThrusterList(MovementDirection direction)
		{
			switch (_currentShipWorldMatrix)
			{
				case ShipMatrixTranslation.Original:
					return _thrusters[direction];
				case ShipMatrixTranslation.LeftForwardUpUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Right];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.LeftForwardDownUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Right];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.LeftForwardForwardUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Right];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.LeftForwardBackUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Right];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.RightForwardUpUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Left];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.RightForwardDownUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Left];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.RightForwardForwardUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Left];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.RightForwardBackUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Left];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.UpForwardForwardUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Down];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.UpForwardBackUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Down];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.UpForwardLeftUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Down];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.UpForwardRightUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Down];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.DownForwardForwardUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Up];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.DownForwardBackUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Up];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.DownForwardLeftUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Up];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.DownForwardRightUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Up];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.ForwardForwardUpUp:
					return _thrusters[direction];
				case ShipMatrixTranslation.ForwardForwardLeftUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Back];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.ForwardForwardRightUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Back];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.ForwardForwardDownUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Forward];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Back];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.BackForwardUpUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Forward];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.BackForwardLeftUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Forward];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.BackForwardRightUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Forward];
						default:
							return _thrusters[direction];
					}
				case ShipMatrixTranslation.BackForwardDownUp:
					switch (direction)
					{
						case MovementDirection.Up:
							return _thrusters[MovementDirection.Down];
						case MovementDirection.Down:
							return _thrusters[MovementDirection.Up];
						case MovementDirection.Left:
							return _thrusters[MovementDirection.Left];
						case MovementDirection.Right:
							return _thrusters[MovementDirection.Right];
						case MovementDirection.Forward:
							return _thrusters[MovementDirection.Back];
						case MovementDirection.Back:
							return _thrusters[MovementDirection.Forward];
						default:
							return _thrusters[direction];
					}
				default:
					return _thrusters[direction];
			}
		}

		private void SetShipWorldMatrix(ShipMatrixTranslation translation)
		{
			//_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Forward, _thisController.WorldMatrix.Up);
			_currentShipWorldMatrix = translation;
			switch (translation)
			{
				case ShipMatrixTranslation.Original:
					_shipWorldMatrixD = _thisController.WorldMatrix;
					break;
				case ShipMatrixTranslation.LeftForwardUpUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Left, _thisController.WorldMatrix.Up);
					break;
				case ShipMatrixTranslation.LeftForwardDownUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Left, _thisController.WorldMatrix.Down);
					break;
				case ShipMatrixTranslation.LeftForwardForwardUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Left, _thisController.WorldMatrix.Forward);
					break;
				case ShipMatrixTranslation.LeftForwardBackUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Left, _thisController.WorldMatrix.Backward);
					break;
				case ShipMatrixTranslation.RightForwardUpUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Right, _thisController.WorldMatrix.Up);
					break;
				case ShipMatrixTranslation.RightForwardDownUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Right, _thisController.WorldMatrix.Down);
					break;
				case ShipMatrixTranslation.RightForwardForwardUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Right, _thisController.WorldMatrix.Forward);
					break;
				case ShipMatrixTranslation.RightForwardBackUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Right, _thisController.WorldMatrix.Backward);
					break;
				case ShipMatrixTranslation.UpForwardForwardUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Up, _thisController.WorldMatrix.Forward);
					break;
				case ShipMatrixTranslation.UpForwardBackUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Up, _thisController.WorldMatrix.Backward);
					break;
				case ShipMatrixTranslation.UpForwardLeftUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Up, _thisController.WorldMatrix.Left);
					break;
				case ShipMatrixTranslation.UpForwardRightUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Up, _thisController.WorldMatrix.Right);
					break;
				case ShipMatrixTranslation.DownForwardForwardUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Down, _thisController.WorldMatrix.Forward);
					break;
				case ShipMatrixTranslation.DownForwardBackUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Down, _thisController.WorldMatrix.Backward);
					break;
				case ShipMatrixTranslation.DownForwardLeftUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Down, _thisController.WorldMatrix.Left);
					break;
				case ShipMatrixTranslation.DownForwardRightUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Down, _thisController.WorldMatrix.Right);
					break;
				case ShipMatrixTranslation.ForwardForwardUpUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Forward, _thisController.WorldMatrix.Up);
					break;
				case ShipMatrixTranslation.ForwardForwardLeftUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Forward, _thisController.WorldMatrix.Left);
					break;
				case ShipMatrixTranslation.ForwardForwardRightUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Forward, _thisController.WorldMatrix.Right);
					break;
				case ShipMatrixTranslation.ForwardForwardDownUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Forward, _thisController.WorldMatrix.Down);
					break;
				case ShipMatrixTranslation.BackForwardUpUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Backward, _thisController.WorldMatrix.Up);
					break;
				case ShipMatrixTranslation.BackForwardLeftUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Backward, _thisController.WorldMatrix.Left);
					break;
				case ShipMatrixTranslation.BackForwardRightUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Backward, _thisController.WorldMatrix.Right);
					break;
				case ShipMatrixTranslation.BackForwardDownUp:
					_shipWorldMatrixD = MatrixD.CreateWorld(_thisController.WorldMatrix.Translation, _thisController.WorldMatrix.Backward, _thisController.WorldMatrix.Down);
					break;
				default:
					_shipWorldMatrixD = _thisController.WorldMatrix;
					break;
			}
		}
	}
}