using Bots.Common.BaseClasses;
using Bots.GridControl.Controllers.WhipsCode;
using Bots.GridControl.DataTypes;
using Bots.GridControl.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRageMath;

namespace Bots.GridControl.Models
{
	public class ControllableGyros : BaseLoggingClass, IUpdate, ILog
	{
		protected override string Id { get; } = "ControllableGyros";

		private readonly ConcurrentCachingList<MyGyro> _gyros = new ConcurrentCachingList<MyGyro>();
		private readonly GyroOverrides _gyroOverrides = new GyroOverrides();

		private readonly IMyShipController _thisController;

		public ControllableGyros(IMyShipController thisController)
		{
			_thisController = thisController;
		}

		public override void Close()
		{
			base.Close();
			_gyros.ClearList();
		}

		public void Add(MyGyro gyro)
		{
			gyro.OnClose += Close;
			_gyros.Add(gyro);
			_gyros.ApplyAdditions();
		}

		private void Close(MyEntity gyro)
		{
			gyro.OnClose -= Close;
			_gyros.Remove((MyGyro) gyro);
			_gyros.ApplyRemovals();
		}

		private long _updateSchedule = 1;

		public void Update(long tick)
		{
			//if (_updateSchedule % tick != 0) return;
			if (_targetHeading == Vector3D.Zero && !_usePlayerLocation) return;
			if (_usePlayerLocation)
			{
				RotationAngles.GetRotationAnglesWithRoll(_playerPosition - _thisController.GetPosition(), _thisController.GetPosition() - _thisController.GetNaturalGravity(), _thisController.WorldMatrix, _gyroOverrides);
			}

			else
			{
				RotationAngles.GetRotationAnglesWithRoll(_targetHeading - _thisController.GetPosition(), _thisController.GetPosition() - _thisController.GetNaturalGravity(), _thisController.WorldMatrix, _gyroOverrides);
			}
			
			ApplyGyroOverride();
		}

		private bool _usePlayerLocation;

		private Vector3D _playerPosition => MyAPIGateway.Session.Player.GetPosition();

		public void UsePlayerPosition()
		{
			_usePlayerLocation = true;
		}

		private Vector3D _targetHeading = Vector3D.Zero;

		public void Reset()
		{
			_targetHeading = Vector3D.Zero;
			_usePlayerLocation = false;

			foreach (IMyGyro gyro in _gyros)
			{
				gyro.Pitch = 0;
				gyro.Yaw = 0;
				gyro.Roll = 0;
				gyro.GyroOverride = false;
			}
		}

		public void SetTargetHeading(Vector3D location)
		{
			_targetHeading = location;
		}

		private MatrixD ControllerRotatedForward()
		{
			MatrixD newMatrix = new MatrixD
			{
				Forward = _thisController.WorldMatrix.Down,
				Backward = _thisController.WorldMatrix.Up,
				Up = _thisController.WorldMatrix.Forward,
				Down = _thisController.WorldMatrix.Backward,
				Left = _thisController.WorldMatrix.Left,
				Right = _thisController.WorldMatrix.Right,
			};
			return newMatrix;
		}

		private void ApplyGyroOverride()
		{
			Vector3D rotationVec = new Vector3D(-_gyroOverrides.GetPitch(), _gyroOverrides.GetYaw(), _gyroOverrides.GetRoll());
			MatrixD shipMatrix = _thisController.WorldMatrix;
			Vector3D relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);
			foreach (IMyGyro gyro in _gyros)
			{
				Vector3D transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyro.WorldMatrix));
				gyro.Pitch = (float)transformedRotationVec.X;
				gyro.Yaw = (float)transformedRotationVec.Y;
				gyro.Roll = (float)transformedRotationVec.Z;
				gyro.GyroOverride = true;
			}
		}
	}
}