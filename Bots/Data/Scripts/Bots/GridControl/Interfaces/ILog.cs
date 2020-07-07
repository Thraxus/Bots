using System;
using Bots.Common.Enums;
using Bots.Common.Interfaces;

namespace Bots.GridControl.Interfaces
{
	public interface ILog : IClose
	{
		event Action<string, string, LogType, bool, int, string> OnWriteToLog;
	}
}
