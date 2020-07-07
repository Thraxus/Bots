using System;
using Bots.Common.Interfaces;

namespace Bots.Common.BaseClasses
{
	public abstract class BaseClosableClass : IClose
	{
		public event Action<BaseClosableClass> OnClose;

		public bool IsClosed;

		public virtual void Close()
		{
			if (IsClosed) return;
			IsClosed = true;
			OnClose?.Invoke(this);
		}
	}
}