using Bots.Common.BaseClasses;
using Bots.Common.Enums;
using VRage.Game.Components;

namespace Bots
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, priority: int.MinValue + 1)]
	public class BotCore : BaseSessionComp
	{
		protected override string CompName { get; } = "BotCore";
		protected override CompType Type { get; } = CompType.Server;
		protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.NoUpdate;
	}
}
