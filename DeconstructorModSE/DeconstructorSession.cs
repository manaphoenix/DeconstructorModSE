using DeconstructorModSE.Sync;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DeconstructorModSE
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class DeconstructorSession : MySessionComponentBase
	{
		public static DeconstructorSession Instance;
		public Networking Net = new Networking(57747);
		public PacketServer CachedPacketServer;
		public PacketClient CachedPacketClient;
		public IMyTerminalControlListbox GridList { get; set; }
		public IMyTerminalControlTextbox TimerBox { get; set; }
		public IMyTerminalControlSlider EfficiencySlider { get; set; }
		public IMyTerminalControlButton DeconButton { get; set; }
		public IMyTerminalControlListbox ComponentList { get; set; }

		public override void LoadData()
		{
			MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
			Instance = this;
			Net.Register();
			CachedPacketServer = new PacketServer();
			CachedPacketClient = new PacketClient();
		}

		protected override void UnloadData()
		{
			MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

			Instance = null;

			Net?.Unregister();
			Net = null;

			Utils.Grids.Clear();
		}

		private void EntityAdded(IMyEntity ent)
		{
			var grid = ent as IMyCubeGrid;

			if (grid != null)
			{
				Utils.Grids.Add(grid);
				grid.OnMarkForClose += GridMarkedForClose;
			}
		}

		private void GridMarkedForClose(IMyEntity ent)
		{
			Utils.Grids.Remove(ent as IMyCubeGrid);
		}
	}
}