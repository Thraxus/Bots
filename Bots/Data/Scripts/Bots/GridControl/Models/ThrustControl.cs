using Bots.Common;
using Bots.GridControl.Controllers.WhipsCode;
using Bots.GridControl.DataTypes.Enums;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;

namespace Bots.GridControl.Models
{
	public class ThrustControl
	{
		private readonly GridSystems _gridSystems;
		private readonly MyCubeGrid _thisCubeGrid;
		private readonly IMyShipController _thisIController;
		private readonly MyShipController _thisController;

		//	X = Right/Left
		//	Y = Up/Down
		//	Z = Forward/Up

		private readonly double _largeShipMaxAngularSpeed = MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxAngularSpeed;
		private readonly double _largeShipMaxSpeed = MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed;
		private readonly double _smallShipMaxAngularSpeed = MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxAngularSpeed;
		private readonly double _smallShipMaxSpeed = MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;

		private const double Kp = 2;
		private const double Ki = 0;
		private const double Kd = 0.1;

		private double _refreshRate = 1.0;

		private readonly Pid _forwardBackPid;
		private readonly Pid _leftRightPid;
		private readonly Pid _upDownPid;

		public Vector3D PatrolSpeed = new Vector3D();
		
		public ThrustControl(GridSystems gridSystems, IMyShipController thisIController)
		{
			_gridSystems = gridSystems;
			_thisIController = thisIController;
			_thisController = (MyShipController)thisIController;
			_thisCubeGrid = (MyCubeGrid) thisIController.CubeGrid;
			_upDownPid = new Pid(Kp, Ki, Kd, 0.1, _refreshRate / Settings.TicksPerSecond);
			_leftRightPid = new Pid(Kp, Ki, Kd, 0.1, _refreshRate / Settings.TicksPerSecond);
			_forwardBackPid = new Pid(Kp, Ki, Kd, 0.1, _refreshRate / Settings.TicksPerSecond);
		}
		// current velocity - desired velocity determines if there is a need to accelerate or decelerate 
		//	positive = accelerate, negative = decelerate
		// can do math to calculate required thrust to maintain speed - only required in gravity
		// no need for a large scale pid like the one for gyros
		// thrust = max to start, dial back as speed gets closer to required
		// set min value = to calculation above based on gravity to maintain a target velocity
		//	this will be added to the dampners automatically
		// need to disable dampners in the opposing direction 

		public void SetCruisingSpeed(MovementDirection direction, float desiredSpeed)
		{
			
		}


		
	}
}
