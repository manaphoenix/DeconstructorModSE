using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using System.Text;
using IMyShipGrinder = Sandbox.ModAPI.IMyShipGrinder;
using System.Linq;
using System;
using Sandbox.Game.EntityComponents;
using VRage.Utils;
using VRageMath;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Runtime.CompilerServices;
using VRage.Game;
using System.Threading;

namespace DeconstructorModSE
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipGrinder), false, "LargeDeconstructor")]
    public class DeconstructorMod : MyGameLogicComponent
    {
        //Settings
        public const float Efficiency_Min = 0;
        public const float Efficiency_Max = 99;
        public const float Range = 150;
        public const float Power = 0.002f; //in MW
        public const int SETTINGS_CHANGED_COUNTDOWN = (60 * 1) / 10; // div by 10 because it runs in update10
        public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
        public readonly DeconstructorBlockSettings Settings = new DeconstructorBlockSettings();
        IMyShipGrinder deconstructor;
       
        IMyInventory MyInventory;
        public List<IMyCubeGrid> Grids;
        public IMyCubeGrid SGrid;
        MyResourceSinkComponent sink;
        int syncCountdown;
        DeconstructorSession Mod => DeconstructorSession.Instance;

        public float Efficiency
        {
            get { return Settings.Efficiency; }
            set
            {
                Settings.Efficiency = MathHelper.Clamp(value, Efficiency_Min, Efficiency_Max);

                SettingsChanged();
                
            }
        }

        public void SyncServer(long grid)
        {
            IMyEntity entity;
            MyAPIGateway.Entities.TryGetEntityById(grid, out entity);

            if (entity != null && !entity.MarkedForClose)
            {
                var system = (IMyCubeGrid)entity;
                if (system != null)
                {
                    GetGrindTime(system);
                    DeconstructGrid(system);
                    Mod.CachedPacketClient.Send(deconstructor.EntityId, Settings.IsGrinding, Settings.Efficiency, Settings.Time);
                }
            }
        }

        public void SetPower(bool Working = false)
        {
            var removedTime = Settings.Time / (1 - (Efficiency / 100));
            var powerRequired = Power + (removedTime / 1000 / 60 / 2);
            if (Working) {
                sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
                sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
            } else
            {
                sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Power); // In MW
                sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Power); // In MW
            }
            sink.Update();
        }

        public void DeconstructGrid(IMyCubeGrid SelectedGrid)
        {
            if (SelectedGrid == null || Settings.Items.Count > 0) return;
            Settings.IsGrinding = true;
            Utils.DeconstructGrid(MyInventory, ref SelectedGrid, ref Settings.Items);
            SelectedGrid.Delete();
            SetPower(true);
        }

        public void GetGrindTime(IMyCubeGrid SelectedGrid)
        {
            Utils.GetGrindTime(this, ref SelectedGrid, ref Settings.Time);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME; // allow UpdateOnceBeforeFrame() to execute, remove if not needed
        }

        public override void UpdateOnceBeforeFrame()
        {
            // first update of the block, remove if not needed
            if (!DeconstructorTerminalInit._TerminalInit)
            {
                DeconstructorTerminalInit._TerminalInit = true;
                DeconstructorTerminalInit.InitControls<IMyShipGrinder>();
            }

            deconstructor = (IMyShipGrinder)Entity;
            if (deconstructor.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            MyInventory = deconstructor.GetInventory();
            sink = deconstructor.ResourceSink as MyResourceSinkComponent;

            Grids = new List<IMyCubeGrid>();
            deconstructor.AppendingCustomInfo += AddCustomInfo;

            LoadSettings();
            
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME; // allow UpdateAfterSimulation() and UpdateAfterSimulation100() to execute, remove if not needed

            SaveSettings();
        }

        private void AddCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            if (Settings.Time > 0)
            {
                info.Append($"Power Required: {Math.Round(sink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId)*1000,2)}Kw\n");
            }
        }

        // Saving
        bool LoadSettings()
        {
            if (deconstructor.Storage == null)
                return false;

            string rawData;
            if (!deconstructor.Storage.TryGetValue(SETTINGS_GUID, out rawData))
                return false;

            try
            {
                var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<DeconstructorBlockSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    Settings.Efficiency = loadedSettings.Efficiency;
                    Settings.IsGrinding = loadedSettings.IsGrinding;
                    Settings.Time = loadedSettings.Time;
                    Settings.Items = loadedSettings.Items;
                    return true;
                }
            }
            catch (Exception e)
            {
                DeconstructorLog.Error($"Error loading settings!\n{e}");
            }

            return false;
        }

        void SaveSettings()
        {
            if (deconstructor == null)
                return; // called too soon or after it was already closed, ignore

            if (Settings == null)
                throw new NullReferenceException($"Settings == null on entId={Entity?.EntityId}; modInstance={DeconstructorSession.Instance != null}");

            if (MyAPIGateway.Utilities == null)
                throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={DeconstructorSession.Instance != null}");

            if (deconstructor.Storage == null)
                deconstructor.Storage = new MyModStorageComponent();

            deconstructor.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
        }

        void SettingsChanged()
        {
            if (syncCountdown == 0)
                syncCountdown = SETTINGS_CHANGED_COUNTDOWN;
        }

        void SyncSettings()
        {
            if (syncCountdown > 0 && --syncCountdown <= 0)
            {
                SaveSettings();
            }
        }

        public override bool IsSerialized()
        {
            try
            {
                SaveSettings();
            }
            catch (Exception e)
            {
                DeconstructorLog.Error(e);
            }

            return base.IsSerialized();
        }

        public override void UpdateAfterSimulation100()
        {
            SyncSettings();
            if (deconstructor.IsFunctional && deconstructor.IsWorking && deconstructor.Enabled)
            {
                if (Utils.Grids == null || Grids == null) return;
                deconstructor.RefreshCustomInfo();

                if (Settings.Time <= 0)
                {
                    if (Settings.Items.Count > 0)
                    {
                        Utils.SpawnItems(MyInventory, ref Settings.Items);
                        DeconstructorSession.Instance.ComponentList.UpdateVisual();
                    }
                    else
                    {
                        Settings.IsGrinding = false;
                        DeconstructorSession.Instance.DeconButton.UpdateVisual();
                        DeconstructorSession.Instance.EfficiencySlider.UpdateVisual();
                        DeconstructorSession.Instance.GridList.UpdateVisual();
                        DeconstructorSession.Instance.ComponentList.UpdateVisual();
                        DeconstructorSession.Instance.TimerBox.UpdateVisual();
                        SetPower();
                    }
                } else
                {
                    if (Settings.IsGrinding)
                    {
                        NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
                    }
                }

                Grids.Clear();
                foreach (var grid in Utils.Grids)
                {
                    if (!grid.IsSameConstructAs(deconstructor.CubeGrid) 
                        && (grid.GetPosition() - deconstructor.GetPosition()).Length() <= Range 
                        && ((grid.SmallOwners.Contains(deconstructor.OwnerId) && grid.SmallOwners.Count == 1) || grid.SmallOwners.Count == 0)
                    )
                    {
                        if (grid.Physics != null)
                        {
                            Grids.Add(grid);
                        }
                    }
                }
            }
            else
                Grids.Clear();
        }

        public override void UpdateAfterSimulation()
        {
            if (deconstructor.IsFunctional && deconstructor.IsWorking && deconstructor.Enabled)
            {
                //60 ticks = 1 second
                Settings.Time -= 1.0f / 60.0f;
                if (Settings.Time > 0)
                {
                    Mod.TimerBox.UpdateVisual();
                }

                if (Settings.Time <= 0)
                {
                    Settings.Time = 0;
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
