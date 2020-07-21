using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.XPath;
using Bots.Common.BaseClasses;
using Bots.Common.Utilities.Statics;
using Bots.GridControl.DataTypes;
using Bots.GridControl.DataTypes.Enums;
using Bots.GridControl.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRageMath;
using IMyShipController = Sandbox.ModAPI.IMyShipController;
using Line = Bots.GridControl.DataTypes.Line;

namespace Bots.GridControl.Models
{
	internal class FlightControl : BaseLoggingClass, IUpdate
	{
		protected override string Id { get; } = "FlightControl";

		private readonly GridSystems _gridSystems;

		private readonly MyCubeGrid _thisCubeGrid;
		private readonly IMyShipController _thisIController;
		private readonly MyShipController _thisController;
		private readonly DebugDraw _draw = new DebugDraw();

		private readonly Line _thisDestination = new Line();

		public FlightControl(MyCubeGrid thisGrid, IMyShipController thisController, GridSystems gridSystems)
		{
			_thisCubeGrid = thisGrid;
			_thisIController = thisController;
			_thisController = (MyShipController) thisController;
			_gridSystems = gridSystems;

			_thisDestination.Color = new Vector4(255, 0, 0, 1);
			_thisDestination.To = _waypoints[0];
			_thisDestination.Thickness = 0.50f;
			AddDrawLine();
			MyAPIGateway.Utilities.MessageEntered += MessageEntered;
			_gridSystems.ControllableThrusters.OnWriteToLog += WriteToLog;
		}


		private void MessageEntered(string message, ref bool send)
		{
			if (message.ToLower().StartsWith("0"))
			{
				ResetSystems();
			}
			if (message.ToLower().StartsWith("1"))
			{
				TargetAcquired(_waypoints[0]);
			}
			if (message.ToLower().StartsWith("2"))
			{
				TargetAcquired(_waypoints[1]);
			}
			if (message.ToLower().StartsWith("3"))
			{
				TargetAcquired(_waypoints[2]);
			}
			if (message.ToLower().StartsWith("4"))
			{
				TargetAcquired(_waypoints[3]);
			}
			if (message.ToLower().StartsWith("5"))
			{
				TargetAcquired(_waypoints[4]);
			}
			if (message.ToLower().StartsWith("6"))
			{
				TargetAcquired(_waypoints[5]);
			}
			if (message.ToLower().StartsWith("d"))
			{
				_gridSystems.ControllableThrusters.EnableCustomDampners();
			}
			if (message.ToLower().StartsWith("p"))
			{
				_usePlayerPosition = true;
			}
			if (message.ToLower().StartsWith("t"))
			{
				_gridSystems.ControllableThrusters.SetThrust(MovementDirection.Forward, ThrustPower.Full);
			}
			if (message.ToLower().StartsWith("r"))
			{
				_botState = BotState.Patrolling;
				_engagementRange = 0;
			}
			if (!message.ToLower().StartsWith("x")) return;
			ResetSystems();
			
		}

		private void ResetSystems()
		{
			_gridSystems.ControllableGyros.Reset();
			_gridSystems.ControllableThrusters.ResetThrust();
			_gridSystems.ControllableThrusters.DisableCustomDampners();
			_targetPosition = Vector3D.Zero;
			_usePlayerPosition = false;
			_botState = BotState.Waiting;
			_engagementPattern = EngagementPattern.None;
			_engagementRange = 400;
		}

		private MovementDirection _currentThrustDirection = MovementDirection.Forward;
		private Vector3D _targetPosition = Vector3D.Zero;
		private bool _usePlayerPosition;

		private float GetBrakingSpeed()
		{
			_gridSystems.DebugScreens.WriteToLeft($"{_gridSystems.ControllableThrusters.GetMaxEffectiveThrustInDirection(_currentThrustDirection)}\n{_thisIController.CalculateShipMass().TotalMass}\n{_thisIController.CalculateShipMass().PhysicalMass}");
			return -(_gridSystems.ControllableThrusters.GetMaxEffectiveThrustInDirection(GetBrakingDirection()) / _thisIController.CalculateShipMass().PhysicalMass);
		}

		private Vector3D GetCurrentThrustVector()
		{
			switch (_currentThrustDirection)
			{
				case MovementDirection.Forward:
					return _thisIController.WorldMatrix.Forward;
				case MovementDirection.Back:
					return _thisIController.WorldMatrix.Backward;
				case MovementDirection.Right:
					return _thisIController.WorldMatrix.Right;
				case MovementDirection.Left:
					return _thisIController.WorldMatrix.Left;
				case MovementDirection.Up:
					return _thisIController.WorldMatrix.Up;
				case MovementDirection.Down:
					return _thisIController.WorldMatrix.Down;
				default:
					return Vector3D.Zero;
			}
		}

		private MovementDirection GetBrakingDirection()
		{
			switch (_currentThrustDirection)
			{
				case MovementDirection.Forward:
					return MovementDirection.Back;
				case MovementDirection.Back:
					return MovementDirection.Forward;
				case MovementDirection.Right:
					return MovementDirection.Left;
				case MovementDirection.Left:
					return MovementDirection.Right;
				case MovementDirection.Up:
					return MovementDirection.Down;
				case MovementDirection.Down:
					return MovementDirection.Up;
				default:
					return MovementDirection.Forward;
			}
		}

		private Vector3D GetStoppingPoint()
		{
			return _thisIController.GetPosition() + (_thisIController.GetShipVelocities().LinearVelocity.Normalize() * (_thisIController.GetShipVelocities().LinearVelocity.Length() / GetBrakingSpeed()));
		}
		
		private readonly PointBillboard _stoppingPoint = new PointBillboard();
		private BotState _botState = BotState.Waiting;
		private EngagementPattern _engagementPattern = EngagementPattern.None;
		private double _engagementRange = 400;
		private double _engagementBuffer => _engagementRange * 0.1;

		private bool _forwardThrusting;
		private bool _circling;

		private readonly PatrolRoute _patrolRoute = new PatrolRoute();

		private void TargetAcquired(Vector3D target)
		{
			_targetPosition = target;
			_botState = BotState.Intercepting;
		}

		public void Update(long tick)
		{
			//MySimpleObjectDraw.draw 
			//MyTransparentGeometry.
			//MyTransparentGeometry.AddBillboardOriented()
			_gridSystems.ControllableThrusters.Update(tick);
			_rightScreen.Clear();
			double distance = 0;
			double displacement = 0;
			_thisDestination.From = _thisIController.GetPosition() + (_thisIController.WorldMatrix.Up * 1);
			_thisDestination.Set();
			_draw.DrawLine(_thisDestination);
			_draw.Update(tick);
			_gridSystems.ControllableGyros.Update(tick);

			if (_botState != BotState.Waiting || _usePlayerPosition)
			{
				if (_botState == BotState.Patrolling && _targetPosition == Vector3D.Zero) _targetPosition = _patrolRoute.GetNextNode();
				displacement = Displacement();
				_gridSystems.ControllableGyros.SetTargetHeading(_usePlayerPosition ? MyAPIGateway.Session.Player.GetPosition() : _targetPosition);
				_thisDestination.To = _usePlayerPosition ? MyAPIGateway.Session.Player.GetPosition() : _targetPosition;

				distance = Vector3D.Distance(_targetPosition, _thisIController.GetPosition() + GetCurrentThrustVector() * _engagementRange);
				switch (_botState)
				{
					case BotState.None:
						break;
					case BotState.Chasing:
						break;
					case BotState.Engaging:
						if (distance > _engagementBuffer)
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.Full);
						}
						else if (distance < _engagementBuffer)
						{
							_gridSystems.ControllableThrusters.SetThrust(GetBrakingDirection(), ThrustPower.Full);
						}
						else 
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.None);
						}

						if (_engagementPattern == EngagementPattern.None)
						{
							_engagementPattern = EngagementPattern.Circling;
							_gridSystems.ControllableThrusters.SetThrust(MovementDirection.Left, ThrustPower.TenPercent);
						}
						break;
					case BotState.Evading:
						break;
					case BotState.Fleeing:
						break;
					case BotState.Intercepting:
						if (distance > displacement)
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.Full);
						}
						else if (distance < _engagementRange)
						{
							_botState = BotState.Engaging;
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.None);
						}
						break;
					case BotState.Patrolling:
						if (distance > displacement)
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.Full);
						}
						else if (distance < displacement)
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.None);
						}
						if (distance < 20)
						{
							_targetPosition = _patrolRoute.GetNextNode();
						}
						break;
					case BotState.Stalking:
						break;
					case BotState.Waiting:
						break;
					default:
						break;
				}
				//if (distance < displacement)
				//{
				//	//_gridSystems.ControllableThrusters.ResetThrust();
				//	_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 0);
				//	_forwardThrusting = false;
				//	if (!_circling)
				//	{
				//		_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Left, 50000, true);
				//		_circling = true;
				//	}
				//}
				//else
				//{
				//	_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000, true);
				//	_forwardThrusting = true;
				//}
			}


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

			_rightScreen.AppendLine($"Distance: {distance}");
			_rightScreen.AppendLine($"Displacement: {displacement}");
			_rightScreen.AppendLine($"Bot State: {_botState}");
			//_rightScreen.AppendLine($"Current Thrust Direction: {_currentThrustDirection}");
			//_rightScreen.AppendLine($"\n");
			_rightScreen.AppendLine($"::: Dampners :::");
			_rightScreen.AppendLine($"Total Mass: {_thisIController.CalculateShipMass().TotalMass}");
			_rightScreen.AppendLine($"Grid Mass: {mass}");
			_rightScreen.AppendLine($"Gravity Length: {gravity.Length()}");
			_rightScreen.AppendLine($"Required Thrust: {requiredThrust}");
			_rightScreen.AppendLine();
			_rightScreen.AppendLine($"Forward Thrust: {forwardThrust}");
			_rightScreen.AppendLine($"Up Thrust: {upThrust}");
			_rightScreen.AppendLine($"Right Thrust: {rightThrust}");
			_rightScreen.AppendLine();
			_rightScreen.AppendLine($"Forward Thrust Arrest: {arrestForwardMovement}");
			_rightScreen.AppendLine($"Up Thrust Arrest: {arrestUpMovement}");
			_rightScreen.AppendLine($"Right Thrust Arrest: {arrestRightMovement}");
			//_rightScreen.AppendLine($"Forward Ratio: {forwardRatio}");
			//_rightScreen.AppendLine($"Forward Thrust: {forwardRatio * requiredThrust}");
			//_rightScreen.AppendLine($"Up Ratio: {upRatio}");
			//_rightScreen.AppendLine($"Up Thrust: {upRatio * requiredThrust}");
			//_rightScreen.AppendLine($"Right Ratio: {rightRatio}");
			//_rightScreen.AppendLine($"Right Thrust: {rightRatio * requiredThrust}");


			//_rightScreen.AppendLine($"Target Position: {_targetPosition}");
			//_rightScreen.AppendLine($"Linear Velocity: {_thisController.GetShipVelocities().LinearVelocity.Length()}");
			//_rightScreen.AppendLine($"Braking Speed: {GetBrakingSpeed()}");
			//_rightScreen.AppendLine($"Time: {time}");
			//_rightScreen.AppendLine($"Time^2: {time * time}");
			//_rightScreen.AppendLine($"\n");
			//_rightScreen.AppendLine($"Braking Calculations:");
			//_rightScreen.AppendLine($"Physical Mass: {_thisController.CalculateShipMass().PhysicalMass}");
			//_rightScreen.AppendLine($"Total Mass: {_thisController.CalculateShipMass().TotalMass}");
			//_rightScreen.AppendLine($"Base Mass: {_thisController.CalculateShipMass().BaseMass}");
			//_rightScreen.AppendLine($"Max Available Thrust: {_gridSystems.ControllableThrusters.GetMaxEffectiveThrustInDirection(GetBrakingDirection())}");

			_gridSystems.DebugScreens.WriteToRight(_rightScreen.ToString());
		}

		private readonly StringBuilder _rightScreen = new StringBuilder();

		private double GravityEffect()
		{
			Vector3D naturalGravity = _thisIController.GetNaturalGravity();
			return Vector3D.Dot(Vector3D.Normalize(naturalGravity), GetCurrentThrustVector()) * naturalGravity.Length();
		}

		private double Displacement(double safeStop = 0)
		{
			double
				linearVelocity = _thisIController.GetShipVelocities().LinearVelocity.Length(),
				braking = GetBrakingSpeed() + GravityEffect(),
				time = linearVelocity/Math.Abs(braking),
				timeSquared = time * time;
			return (linearVelocity * time) + (0.5 * braking * timeSquared) + safeStop;
		}

		private void AddDrawLine()
		{
			for (int i = 0; i < _waypoints.Count - 1; i++)
			{
				Line line = new Line()
				{
					From = _waypoints[i],
					To = _waypoints[i + 1]
				};
				line.Set();
				_draw.AddLine(line);
			}
			Line line2 = new Line()
			{
				From = _waypoints[_waypoints.Count - 1],
				To = _waypoints[0]
			};
			line2.Set();
			_draw.AddLine(line2);
		}

		private readonly List<Vector3D> _waypoints = new List<Vector3D>
		{
			new Vector3D(-43229.021168609252, -9269.5177582804135, 43489.220552603845),
			new Vector3D(-42215.248862785862, -10106.238471936351, 44052.730770789916),
			new Vector3D(-41429.448022182289, -9966.6928187007179, 45368.193750827246),
			new Vector3D(-41763.776891319809, -8156.2750849056138, 45627.912503207961),
			new Vector3D(-42359.304601329386, -6601.9854178186824, 43311.280304341512),
			new Vector3D(-44799.281991765121, -7653.6194506676993, 42228.293961479911),
			new Vector3D(-43120.573476366189, -9248.4646031342054, 42960.09875162087)
		};

		public override void Close()
		{
			base.Close();
			_draw.ClearLines();
			MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
			_gridSystems.ControllableThrusters.OnWriteToLog -= WriteToLog;
		}
	}
}