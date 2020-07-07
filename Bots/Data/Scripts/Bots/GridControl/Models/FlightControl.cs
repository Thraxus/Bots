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
			//List<IMyGps> gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
		}


		private void MessageEntered(string message, ref bool send)
		{
			if (message.ToLower().StartsWith("0"))
			{
				_gridSystems.DebugScreens.WriteToLeft(_waypoints[0].ToString());
				_gridSystems.DebugScreens.WriteToRight(_thisController.GetNaturalGravity().ToString());
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[0]);
			}
			if (message.ToLower().StartsWith("1"))
			{
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[1]);
				_targetPosition = _waypoints[1];
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000);
				_thisDestination.To = _waypoints[1];
			}
			if (message.ToLower().StartsWith("2"))
			{
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[2]);
				_targetPosition = _waypoints[2];
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000);
				_thisDestination.To = _waypoints[2];
			}
			if (message.ToLower().StartsWith("3"))
			{
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[3]);
				_targetPosition = _waypoints[3];
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000);
				_thisDestination.To = _waypoints[3];
			}
			if (message.ToLower().StartsWith("4"))
			{
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[4]);
				_targetPosition = _waypoints[4];
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000);
				_thisDestination.To = _waypoints[4];
			}
			if (message.ToLower().StartsWith("5"))
			{
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[5]);
			}
			if (message.ToLower().StartsWith("6"))
			{
				_gridSystems.ControllableGyros.SetTargetHeading(_waypoints[6]);
			}
			if (message.ToLower().StartsWith("p"))
			{
				_gridSystems.ControllableGyros.UsePlayerPosition();
			}
			if (message.ToLower().StartsWith("t"))
			{
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Forward, 500000);
			}

			if (!message.ToLower().StartsWith("x")) return;
			_gridSystems.ControllableGyros.Reset();
			_gridSystems.ControllableThrusters.ResetThrust();
			_targetPosition = Vector3D.Zero;
		}

		private ThrustDirection _currentThrustDirection = ThrustDirection.Forward;
		private Vector3D _targetPosition = Vector3D.Zero;
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

		private double _engagementRange = 500f;

		public void Update(long tick)
		{
			_thisDestination.From = _thisController.GetPosition() + (_thisController.WorldMatrix.Up * 1);
			_thisDestination.Set();
			_draw.DrawLine(_thisDestination);
			_draw.Update(tick);
			_gridSystems.ControllableGyros.Update(tick);

			double distance = Vector3D.Distance(_targetPosition, _thisController.GetPosition());
			if (distance < Displacement(_engagementRange))
			{
				_gridSystems.ControllableThrusters.ResetThrust();
				_gridSystems.ControllableThrusters.SetThrust(ThrustDirection.Left, 50000);
			}


			

			double time = _thisController.GetShipVelocities().LinearVelocity.Length() / Math.Abs(GetBrakingSpeed());

			_rightScreen.Clear();
			_rightScreen.AppendLine($"Distance: {distance}");
			_rightScreen.AppendLine($"Displacement: {Displacement()}");
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