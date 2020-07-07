using System.Text;
using Bots.Common.BaseClasses;
using Bots.Common.Enums;
using Bots.GridControl.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Bots.GridControl.Models
{
	internal class ControllableGrid : BaseLoggingClass, IUpdate
	{
		protected override string Id { get; } = "ControllableGrid";

		private readonly IMyEntity _thisEntity;
		private readonly IMyCubeGrid _thisIGrid;
		private readonly MyCubeGrid _thisGrid;
		private readonly IMyShipController _thisController;
		private readonly FlightControl _flightControl;
		private readonly GridSystems _gridSystems;

		public ControllableGrid(IMyCubeGrid grid, IMyShipController controller)
		{
			_thisIGrid = grid;
			_thisEntity = grid;
			_thisGrid = (MyCubeGrid) grid;
			_thisController = controller;
			_thisGrid.OnFatBlockAdded += OnFatBlockAdded;
			_thisGrid.OnFatBlockRemoved += OnFatBlockRemoved;
			_thisGrid.OnClose += Close;

			_gridSystems = new GridSystems(_thisController);
			_gridSystems.OnWriteToLog += WriteToLog;

			_flightControl = new FlightControl(_thisGrid, _thisController, _gridSystems);
			_flightControl.OnWriteToLog += WriteToLog;
		}


		public void Close(IMyEntity entity)
		{
			base.Close();
			_gridSystems.OnWriteToLog -= WriteToLog;
			_gridSystems.Close();
			_flightControl.OnWriteToLog -= WriteToLog;
			_flightControl.Close();
			_thisGrid.OnClose -= Close;
			_thisGrid.OnFatBlockAdded -= OnFatBlockAdded;
			_thisGrid.OnFatBlockRemoved -= OnFatBlockRemoved;
		}
		
		public void SetupGrid()
		{
			foreach (MyCubeBlock block in _thisGrid.GetFatBlocks())
			{
				MyThrust myThrust = block as MyThrust;
				if (myThrust != null)
				{
					_gridSystems.ControllableThrusters.AddNewThruster(myThrust);
					WriteToLog("SetupGrid", $"Adding Thruster {myThrust.GridThrustDirection} {myThrust.WorldMatrix.Forward}", LogType.General);
					continue;
				}

				MyGyro myGyro = block as MyGyro;
				if (myGyro != null)
				{
					_gridSystems.ControllableGyros.Add(myGyro);
					WriteToLog("SetupGrid", $"Adding Gyro", LogType.General);
					continue;
				}

				IMyTextSurface surface = block as IMyTextSurface;
				if (surface != null) 
				{
					if (((IMyTerminalBlock) surface).CustomData.Contains("DebugL"))
					{
						_gridSystems.DebugScreens.AddLeftScreen(surface);
						_gridSystems.DebugScreens.WriteToLeft("Left Debug Screen Detected");
						WriteToLog("SetupGrid", $"Adding Left Debug Screen", LogType.General);
					}

					if (((IMyTerminalBlock) surface).CustomData.Contains("DebugR")) 
					{
						_gridSystems.DebugScreens.AddRightScreen(surface);
						_gridSystems.DebugScreens.WriteToRight("Right Debug Screen Detected");
						WriteToLog("SetupGrid", $"Adding Right Debug Screen", LogType.General);
					}
					continue;
				}

				IMyLandingGear gear = block as IMyLandingGear;
				if (gear == null) continue;
				_gridSystems.ControllableLandingGear.Add(gear);
			}
		}

		private void OnFatBlockRemoved(MyCubeBlock block)
		{
			if (block == _thisController) Close(block);
		}

		private void OnFatBlockAdded(MyCubeBlock block)
		{
			
		}

		private void RegisterNewBlock(IMyEntity entity)
		{

		}

		public void Update(long tick)
		{
			_flightControl.Update(tick);
		}
	}
}