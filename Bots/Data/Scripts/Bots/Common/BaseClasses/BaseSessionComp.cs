using Bots.Common.Enums;
using Bots.Common.Interfaces;
using Bots.Common.Utilities.Tools.Logging;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;

namespace Bots.Common.BaseClasses
{
	public abstract class BaseSessionComp : MySessionComponentBase, ILog
	{
		protected abstract string CompName { get; }

		protected abstract CompType Type { get; }

		protected abstract MyUpdateOrder Schedule { get; }

		internal long TickCounter;

		private Log _generalLog;

		private bool _superEarlySetupComplete;
		private bool _earlySetupComplete;
		private bool _lateSetupComplete;

		private bool BlockUpdates()
		{
			switch (Type)
			{
				case CompType.Both:
					return false;
				case CompType.Client:
					return Settings.IsServer;
				case CompType.Server:
					return !Settings.IsServer;
				default:
					return false;
			}
		}

		/// <inheritdoc />
		public override void LoadData()
		{
			if (BlockUpdates())
			{
				MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate)); // sets the proper update schedule to the desired schedule
				return;
			};
			base.LoadData();
			if (!_superEarlySetupComplete) SuperEarlySetup();
		}

		public override MyObjectBuilder_SessionComponent GetObjectBuilder()
		{
			// Always return base.GetObjectBuilder(); after your code! 
			// Do all saving here, make sure to return the OB when done;
			return base.GetObjectBuilder();
		}

		public override void SaveData()
		{
			// this save happens after the game save, so it has limited uses really
			base.SaveData();
		}

		protected virtual void SuperEarlySetup()
		{
			_superEarlySetupComplete = true;
			_generalLog = new Log(CompName);
			WriteToLog("SuperEarlySetup", $"Waking up.  Is Server: {Settings.IsServer}", LogType.General);
		}

		public override void BeforeStart()
		{
			if (BlockUpdates()) return;
			base.BeforeStart();
		}

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			if (BlockUpdates()) return;
			base.Init(sessionComponent);
			if (!_earlySetupComplete) EarlySetup();
		}

		protected virtual void EarlySetup()
		{
			_earlySetupComplete = true;
		}

		public override void UpdateBeforeSimulation()
		{
			if (BlockUpdates()) return;
			base.UpdateBeforeSimulation();
			if (!_lateSetupComplete) LateSetup();
			RunBeforeSimUpdate();
		}

		private void RunBeforeSimUpdate()
		{
			TickCounter++;
			BeforeSimUpdate();
			if (TickCounter % 2 == 0) BeforeSimUpdate2Ticks();
			if (TickCounter % 10 == 0) BeforeSimUpdate5Ticks();
			if (TickCounter % 20 == 0) BeforeSimUpdate10Ticks();
			if (TickCounter % (Settings.TicksPerSecond / 2) == 0) BeforeSimUpdateHalfSecond();
			if (TickCounter % Settings.TicksPerSecond == 0) BeforeSimUpdate1Second();
			if (TickCounter % (Settings.TicksPerSecond * 30) == 0) BeforeSimUpdate30Seconds();
			if (TickCounter % (Settings.TicksPerMinute) == 0) BeforeSimUpdate1Minute();
		}
		
		protected virtual void BeforeSimUpdate() { }
		
		protected virtual void BeforeSimUpdate2Ticks() { }
		
		protected virtual void BeforeSimUpdate5Ticks() { }
		
		protected virtual void BeforeSimUpdate10Ticks() { }

		protected virtual void BeforeSimUpdateHalfSecond() { }

		protected virtual void BeforeSimUpdate1Second() { }

		protected virtual void BeforeSimUpdate30Seconds() { }

		protected virtual void BeforeSimUpdate1Minute() { }
		
		protected virtual void LateSetup()
		{
			_lateSetupComplete = true;
			if (UpdateOrder != Schedule)
				MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(Schedule)); // sets the proper update schedule to the desired schedule
			WriteToLog("LateSetup", $"Fully online.", LogType.General);
		}


		public override void UpdateAfterSimulation()
		{
			if (BlockUpdates()) return;
			base.UpdateAfterSimulation();
		}

		protected override void UnloadData()
		{
			Unload();
			base.UnloadData();
		}

		protected virtual void Unload()
		{
			if (BlockUpdates()) return;
			WriteToLog("Unload", $"Retired.", LogType.General);
			_generalLog?.Close();
		}

		public void WriteToLog(string caller, string message, LogType type, bool showOnHud = false, int duration = Settings.DefaultLocalMessageDisplayTime, string color = MyFontEnum.Green)
		{
			switch (type)
			{
				case LogType.Exception:
					WriteException(caller, message, showOnHud, duration, color);
					return;
				case LogType.General:
					WriteGeneral(caller, message, showOnHud, duration, color);
					return;
				default:
					return;
			}
		}

		private readonly object _writeLocker = new object();

		private void WriteException(string caller, string message, bool showOnHud, int duration, string color)
		{
			StaticLog.WriteToLog($"{CompName}: {caller}", $"Exception! {message}", LogType.Exception, showOnHud, duration, color);
		}

		private void WriteGeneral(string caller, string message, bool showOnHud, int duration, string color)
		{
			lock (_writeLocker)
			{
				_generalLog?.WriteToLog($"{CompName}: {caller}", message, showOnHud, duration, color);
			}
		}
	}
}