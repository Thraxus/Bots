using Bots.Common.BaseClasses;
using Bots.Common.Enums;
using Bots.GridControl.Models;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Bots.GridControl
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, priority: int.MinValue + 1)]
	public class GridControlCore : BaseSessionComp
	{
		protected override string CompName { get; } = "GridControlCore";
		protected override CompType Type { get; } = CompType.Server;
		protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.BeforeSimulation;
		
		private readonly ConcurrentCachingList<ControllableGrid> _grids = new ConcurrentCachingList<ControllableGrid>();

		protected override void SuperEarlySetup()
		{
			base.SuperEarlySetup();
			// Code below here
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
		}

		protected override void EarlySetup()
		{
			base.EarlySetup();
		}

		private void OnEntityAdd(IMyEntity entity)
		{
			IMyCubeGrid grid = entity as IMyCubeGrid;
			if (grid == null) return;
			RegisterNewGrid((MyCubeGrid)grid);
		}

		private void RegisterNewGrid(MyCubeGrid grid)
		{
			// Logic for picking proper grids goes here
			IMyShipController mainControl = null;
			foreach (IMyShipController controller in grid.GetFatBlocks<MyShipController>())
			{
				if(!controller.CustomData.Contains("DemoSetup")) continue;
				mainControl = controller;
			}
			if (mainControl == null) return;
			WriteToLog("RegisterNewGrid", $"Registering new grid: {mainControl.CubeGrid.DisplayName}", LogType.General);
			ControllableGrid controllableGrid = new ControllableGrid(grid, mainControl);
			controllableGrid.OnWriteToLog += WriteToLog;
			controllableGrid.OnClose += OnGridClose;
			controllableGrid.SetupGrid();
			_grids.Add(controllableGrid);
			_grids.ApplyAdditions();
		}

		private void DeRegisterGrid(ControllableGrid grid)
		{
			if (grid == null) return;
			grid.OnWriteToLog -= WriteToLog;
			grid.OnClose -= OnGridClose;
			grid.Close(null);
			_grids.Remove(grid);
		}

		private void OnGridClose(BaseClosableClass grid)
		{
			DeRegisterGrid(grid as ControllableGrid);
		}

		protected override void Unload()
		{
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			foreach (ControllableGrid grid in _grids)
				DeRegisterGrid(grid);
			_grids.ApplyRemovals();
			_grids.ClearList();
			// Unload code above base
			base.Unload();
		}

		protected override void BeforeSimUpdate()
		{
			base.BeforeSimUpdate();
			foreach (ControllableGrid grid in _grids)
			{
				grid.Update(TickCounter);
			}
		}
	}
}