using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class DeconstructorSession : MySessionComponentBase
    {
        public static DeconstructorSession Instance;
        private bool _init = false;
        public const int Dist = 150;

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            Instance = this;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

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
                if (!_init)
                {
                    if (MyAPIGateway.Session == null) return;
                    DeconstructorTerminal.InitControls();
                    _init = true;
                }
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
