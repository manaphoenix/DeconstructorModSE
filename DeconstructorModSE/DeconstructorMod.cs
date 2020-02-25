using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game;
using System.Text;
using IMyShipGrinder = Sandbox.ModAPI.IMyShipGrinder;
using VRage.Game.Entity;
using System.Linq;
using System;
using Sandbox.Game.EntityComponents;
using VRage.Utils;

namespace DeconstructorModSE
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipGrinder), false, "LargeDeconstructor")]
    public class DeconstructorMod : MyGameLogicComponent
    { 
        private IMyShipGrinder deconstructor;
        private IMyInventory MyInventory;
        public List<IMyCubeGrid> Grids;
        public int Efficiency = 0;
        public IMyCubeGrid SGrid;
        private Dictionary<MyDefinitionId, MyPhysicalInventoryItem> Items = new Dictionary<MyDefinitionId, MyPhysicalInventoryItem>();
        private float totalTime;
        public bool isGrinding;
        private MyResourceSinkComponent sink;
        private readonly float powerUse = 0.002f; //in MW

        public void SetPower(bool Working = false)
        {
            var removedTime = totalTime / (1 - (Efficiency / 100));
            var powerRequired = powerUse + (removedTime / 1000 / 60 / 2);
            if (Working) {
                sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
                sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
            } else
            {
                sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerUse); // In MW
                sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerUse); // In MW
            }
            sink.Update();
        }

        public void Save()
        {
            //TODO
        }

        public void Load()
        {
            //TODO
        }

        public void SyncClient(bool grind, int eff, float time)
        {
            isGrinding = grind;
            Efficiency = eff;
            totalTime = time;
        }

        public void SyncServer(long grid, int eff)
        {
            IMyEntity entity;
            MyAPIGateway.Entities.TryGetEntityById(grid, out entity);

            if (entity != null && !entity.MarkedForClose)
            {
                var system = (IMyCubeGrid)entity;
                if (system != null)
                {
                    Efficiency = eff;
                    GetGrindTime(system);
                    DeconstructGrid(system);
                }
            }
        }

        public void DeconstructGrid(IMyCubeGrid SelectedGrid)
        {
            if (SelectedGrid == null || Items.Count > 0) return;
            isGrinding = true;
            Utils.DeconstructGrid(MyInventory, ref SelectedGrid, ref Items);
            SelectedGrid.Delete();
            SetPower(true);
        }

        public void GetGrindTime(IMyCubeGrid SelectedGrid)
        {
            Utils.GetGrindTime(this, ref SelectedGrid, ref totalTime);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            MyLog.Default.WriteLineAndConsole($"Deconstructor Block Init");
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME; // allow UpdateOnceBeforeFrame() to execute, remove if not needed
        }

        public override void UpdateOnceBeforeFrame()
        {
            // first update of the block, remove if not needed
            deconstructor = (IMyShipGrinder)Entity;
            if (deconstructor.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            if (!DeconstructorSession.Instance._TerminalInit)
            {
                DeconstructorSession.Instance._TerminalInit = true;
                DeconstructorTerminal.InitControls<IMyShipGrinder>();
            }

            MyInventory = deconstructor.GetInventory();
            sink = deconstructor.ResourceSink as MyResourceSinkComponent;

            Grids = new List<IMyCubeGrid>();
            deconstructor.AppendingCustomInfo += AddCustomInfo;

            //TODO Load Data

            //
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME; // allow UpdateAfterSimulation() and UpdateAfterSimulation100() to execute, remove if not needed
        }

        private void AddCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            if (totalTime > 0)
            {
                var time = TimeSpan.FromSeconds(totalTime);
                info.Append($"Timer: {time.ToString("hh'h 'mm'm 'ss's '")}\n");
                info.Append($"Power Required: {sink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId)*1000}Kw\n");
            }
            if (Items.Count > 0)
                info.Append($"Items: {Items.Count}");
        }

        public override bool IsSerialized()
        {
            return base.IsSerialized();
            //TODO SaveHere
        }

        public override void UpdateAfterSimulation100()
        {
            if (deconstructor.IsFunctional && deconstructor.IsWorking && deconstructor.Enabled)
            {
                if (Utils.Grids == null || Grids == null) return;
                deconstructor.RefreshCustomInfo();

                DeconstructorSession.Instance.Net.RelayToClients(new DeconstructorPacketClient(deconstructor.EntityId, isGrinding, Efficiency, totalTime));
                if (totalTime <= 0)
                {
                    if (Items.Count > 0)
                    {
                        Utils.SpawnItems(MyInventory, ref Items);
                    }
                    else
                    {
                        isGrinding = false;
                        SetPower();
                    }
                } else
                {
                    if (isGrinding)
                    {
                        NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
                    }
                }

                Grids = Utils.Grids.Where(x => !x.IsSameConstructAs(deconstructor.CubeGrid) && (x.GetPosition() - deconstructor.GetPosition()).Length() <= DeconstructorSession.Dist && (x.SmallOwners.Contains(deconstructor.OwnerId) || x.SmallOwners.Count == 0)).ToList();
            }
            else
                Grids.Clear();
        }

        public override void UpdateAfterSimulation()
        {
            if (deconstructor.IsFunctional && deconstructor.IsWorking && deconstructor.Enabled)
            {
                //60 ticks = 1 second
                totalTime -= 1.0f / 60.0f;

                if (totalTime <= 0)
                {
                    totalTime = 0;
                    NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
                }
            }
        }

        public override void Close()
        {
            deconstructor.AppendingCustomInfo -= AddCustomInfo;
        }
    }
}
