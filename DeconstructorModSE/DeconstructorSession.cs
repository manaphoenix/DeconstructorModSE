using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DeconstructorSession : MySessionComponentBase
    {
        public static DeconstructorSession Instance;
        public DeconstructorNetworking Net = new DeconstructorNetworking(57747);
        public bool _TerminalInit = false;
        public const int Dist = 150;

        public override void BeforeStart()
        {
            base.BeforeStart();
            Net.Register();
        }
        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            Instance = this;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

            Net?.Unregister();
            Net = null;
            Utils.Grids.Clear();

            Instance = null;
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

        public static void SendMessage(string sender, string message)
        {
            MyAPIGateway.Utilities.ShowMessage(sender, message);
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                //Nothing
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }
    }
}
