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
using VRageMath;
using IMyShipController = Sandbox.ModAPI.IMyShipController;
using Line = Bots.GridControl.DataTypes.Line;

namespace Bots.GridControl.Models
{
	internal class FlightControl : BaseLoggingClass, IUpdate
	{
		protected override string Id { get; } = "FlightControl";

		private readonly GridSystems _gridSystems;

		private readonly MyCubeGrid _thisGrid;
		private readonly IMyShipController _thisController;
		private readonly DebugDraw _draw = new DebugDraw();

		private readonly Line _thisDestination = new Line();

		public FlightControl(MyCubeGrid thisGrid, IMyShipController thisController, GridSystems gridSystems)
		{
			_thisGrid = thisGrid;
			_thisController = thisController;
			_gridSystems = gridSystems;

			_thisDestination.Color = new Vector4(255, 0, 0, 1);
			_thisDestination.To = _waypoints[0];
			_thisDestination.Thickness = 0.50f;
			AddDrawLine();
			MyAPIGateway.Utilities.MessageEntered += MessageEntered;
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
			if (message.ToLower().StartsWith("p"))
			{
				_usePlayerPosition = true;
			}
			if (message.ToLower().StartsWith("t"))
			{
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000);
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
			_targetPosition = Vector3D.Zero;
			_usePlayerPosition = false;
			_botState = BotState.Waiting;
			_engagementPattern = EngagementPattern.None;
			_engagementRange = 400;
		}

		private ThrustDirection _currentThrustDirection = ThrustDirection.Forward;
		private Vector3D _targetPosition = Vector3D.Zero;
		private bool _usePlayerPosition;

		private float GetBrakingSpeed()
		{
			_gridSystems.DebugScreens.WriteToLeft($"{_gridSystems.ControllableThrusters.GetMaxEffectiveThrustInDirection(_currentThrustDirection)}\n{_thisController.CalculateShipMass().TotalMass}\n{_thisController.CalculateShipMass().PhysicalMass}");
			return -(_gridSystems.ControllableThrusters.GetMaxEffectiveThrustInDirection(GetBrakingDirection()) / _thisController.CalculateShipMass().PhysicalMass);
		}

		private Vector3D GetCurrentThrustVector()
		{
			switch (_currentThrustDirection)
			{
				case ThrustDirection.Forward:
					return _thisController.WorldMatrix.Forward;
				case ThrustDirection.Back:
					return _thisController.WorldMatrix.Backward;
				case ThrustDirection.Right:
					return _thisController.WorldMatrix.Right;
				case ThrustDirection.Left:
					return _thisController.WorldMatrix.Left;
				case ThrustDirection.Up:
					return _thisController.WorldMatrix.Up;
				case ThrustDirection.Down:
					return _thisController.WorldMatrix.Down;
				default:
					return Vector3D.Zero;
			}
		}

		private ThrustDirection GetBrakingDirection()
		{
			switch (_currentThrustDirection)
			{
				case ThrustDirection.Forward:
					return ThrustDirection.Back;
				case ThrustDirection.Back:
					return ThrustDirection.Forward;
				case ThrustDirection.Right:
					return ThrustDirection.Left;
				case ThrustDirection.Left:
					return ThrustDirection.Right;
				case ThrustDirection.Up:
					return ThrustDirection.Down;
				case ThrustDirection.Down:
					return ThrustDirection.Up;
				default:
					return ThrustDirection.Forward;
			}
		}

		private Vector3D GetStoppingPoint()
		{
			return _thisController.GetPosition() + (_thisController.GetShipVelocities().LinearVelocity.Normalize() * (_thisController.GetShipVelocities().LinearVelocity.Length() / GetBrakingSpeed()));
		}

		private void ThrustControl()
		{
			_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 50000f);
		}

		private readonly PointBillboard _stoppingPoint = new PointBillboard();
		private BotState _botState = BotState.Waiting;
		private EngagementPattern _engagementPattern = EngagementPattern.None;
		private double _engagementRange = 400;
		private double _engagementBuffer => _engagementRange * 0.1;

		private bool _forwardThrusting;
		private bool _circling;

		private PatrolRoute _patrolRoute = new PatrolRoute();

		private void TargetAcquired(Vector3D target)
		{
			_targetPosition = target;
			_botState = BotState.Intercepting;
		}

		public void Update(long tick)
		{
			_rightScreen.Clear();
			double distance = 0;
			double displacement = 0;
			_thisDestination.From = _thisController.GetPosition() + (_thisController.WorldMatrix.Up * 1);
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

				distance = Vector3D.Distance(_targetPosition, _thisController.GetPosition() + GetCurrentThrustVector() * _engagementRange);
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
							_gridSystems.ControllableThrusters.SetThrust(GetBrakingDirection(), ThrustPower.None);
						}
						else if (distance < _engagementBuffer)
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.None);
							_gridSystems.ControllableThrusters.SetThrust(GetBrakingDirection(), ThrustPower.Full);
						}
						else 
						{
							_gridSystems.ControllableThrusters.SetThrust(_currentThrustDirection, ThrustPower.None);
							_gridSystems.ControllableThrusters.SetThrust(GetBrakingDirection(), ThrustPower.None);
						}

						if (_engagementPattern == EngagementPattern.None)
						{
							_engagementPattern = EngagementPattern.Circling;
							_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Left, ThrustPower.TenPercent);
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

			


			

			double time = _thisController.GetShipVelocities().LinearVelocity.Length() / Math.Abs(GetBrakingSpeed());
			
			_rightScreen.AppendLine($"Distance: {distance}");
			_rightScreen.AppendLine($"Displacement: {displacement}");
			_rightScreen.AppendLine($"Bot State: {_botState}");
			_rightScreen.AppendLine($"Current Thrust Direction: {_currentThrustDirection}");
			_rightScreen.AppendLine($"Target Position: {_targetPosition}");
			_rightScreen.AppendLine($"Linear Velocity: {_thisController.GetShipVelocities().LinearVelocity.Length()}");
			_rightScreen.AppendLine($"Braking Speed: {GetBrakingSpeed()}");
			_rightScreen.AppendLine($"Time: {time}");
			_rightScreen.AppendLine($"Time^2: {time * time}");
			_rightScreen.AppendLine($"\n");
			_rightScreen.AppendLine($"Braking Calculations:");
			_rightScreen.AppendLine($"Physical Mass: {_thisController.CalculateShipMass().PhysicalMass}");
			_rightScreen.AppendLine($"Total Mass: {_thisController.CalculateShipMass().TotalMass}");
			_rightScreen.AppendLine($"Base Mass: {_thisController.CalculateShipMass().BaseMass}");
			_rightScreen.AppendLine($"Max Available Thrust: {_gridSystems.ControllableThrusters.GetMaxEffectiveThrustInDirection(GetBrakingDirection())}");
			
			_gridSystems.DebugScreens.WriteToRight(_rightScreen.ToString());
		}

		private readonly StringBuilder _rightScreen = new StringBuilder();

		private double GravityEffect()
		{
			Vector3D naturalGravity = _thisController.GetNaturalGravity();
			return Vector3D.Dot(Vector3D.Normalize(naturalGravity), GetCurrentThrustVector()) * naturalGravity.Length();
		}

		private double Displacement(double safeStop = 0)
		{
			double
				linearVelocity = _thisController.GetShipVelocities().LinearVelocity.Length(),
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
		}
	}
}