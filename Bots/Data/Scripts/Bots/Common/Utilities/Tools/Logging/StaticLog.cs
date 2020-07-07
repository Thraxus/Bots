using Bots.Common.Enums;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace Bots.Common.Utilities.Tools.Logging
{
	[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate, priority: int.MinValue)]
	internal class StaticLog : MySessionComponentBase
	{
		private const string GeneralLogName = Settings.StaticGeneralLogName;
		private const string ExceptionLogName = Settings.ExceptionLogName;

		private static Log _generalLog;
		private static Log _exceptionLog;

		private static readonly object GeneralWriteLocker = new object();
		private static readonly object ExceptionWriteLocker = new object();
		
		/// <inheritdoc />
		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			base.Init(sessionComponent);
			_exceptionLog = new Log(ExceptionLogName);
			_generalLog = new Log(GeneralLogName);
			WriteToLog("StaticLogger", "Static log loaded.", LogType.General);
			WriteToLog("StaticLogger", "Exception log loaded.", LogType.Exception);
		}

		/// <inheritdoc />
		protected override void UnloadData()
		{
			WriteToLog("StaticLogger", "Static log closed.", LogType.General);
			WriteToLog("StaticLogger", "Exception log closed.", LogType.Exception);
			lock (ExceptionWriteLocker)
			{
				_exceptionLog?.Close();
			}
			lock (GeneralWriteLocker)
			{
				_generalLog?.Close();
			}
			base.UnloadData();
		}

		public static void WriteToLog(string caller, string message, LogType type, bool showOnHud = false, int duration = Settings.DefaultLocalMessageDisplayTime, string color = MyFontEnum.Green)
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

		private static void WriteException(string caller, string message, bool showOnHud, int duration, string color)
		{
			lock (ExceptionWriteLocker)
			{
				_exceptionLog?.WriteToLog(caller, message, showOnHud, duration, color);
				MyLog.Default.WriteLine($"{caller}: {message}");
			}
		}

		private static void WriteGeneral(string caller, string message, bool showOnHud, int duration, string color)
		{
			lock (GeneralWriteLocker)
			{
				_generalLog?.WriteToLog(caller, message);
			}
		}
	}
}
