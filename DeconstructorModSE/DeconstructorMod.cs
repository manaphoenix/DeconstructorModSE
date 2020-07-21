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
            if (!DeconstructorSession.Instance._TerminalInit)
            {
                DeconstructorSession.Instance._TerminalInit = true;
                InitControls<IMyShipGrinder>();
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
                var time = TimeSpan.FromSeconds(Settings.Time);
                info.Append($"Timer: {time:hh'h 'mm'm 'ss's '}\n");
                info.Append($"Power Required: {sink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId)*1000}Kw\n");
            }
            if (Settings.Items.Count > 0)
                info.Append($"Items: {Settings.Items.Count}");
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
                    }
                    else
                    {
                        Settings.IsGrinding = false;
                        SetPower();
                    }
                } else
                {
                    if (Settings.IsGrinding)
                    {
                        NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
                    }
                }

                Grids = Utils.Grids.Where(x => !x.IsSameConstructAs(deconstructor.CubeGrid) && (x.GetPosition() - deconstructor.GetPosition()).Length() <= Range && (x.SmallOwners.Contains(deconstructor.OwnerId) || x.SmallOwners.Count == 0)).ToList();
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

        //Terminal
        static void InitControls<T>()
        {
            var gridList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Grids");
            gridList.Visible = VisibilityCheck;
            gridList.Enabled = EnabledCheck;
            gridList.Multiselect = false;
            gridList.SupportsMultipleBlocks = false;
            gridList.VisibleRowsCount = 8;
            gridList.Title = MyStringId.GetOrCompute("Grindable Grids");
            gridList.ItemSelected = List_selected;
            gridList.ListContent = List_content;
            MyAPIGateway.TerminalControls.AddControl<T>(gridList);

            var efficiency = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>("Efficiency");
            efficiency.Enabled = EnabledCheck;
            efficiency.Visible = VisibilityCheck;
            efficiency.SetLimits(0, 99);
            efficiency.SupportsMultipleBlocks = false;
            efficiency.Title = MyStringId.GetOrCompute("Efficiency");
            efficiency.Tooltip = MyStringId.GetOrCompute("Reduces Deconstruction time, But increases Power Requirement");
            efficiency.Setter = Slider_setter;
            efficiency.Getter = Slider_getter;
            efficiency.Writer = Slider_writer;
            MyAPIGateway.TerminalControls.AddControl<T>(efficiency);

            var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("StartDecon");
            button.Visible = VisibilityCheck;
            button.Enabled = EnabledCheck;
            button.SupportsMultipleBlocks = false;
            button.Title = MyStringId.GetOrCompute("Select");
            button.Action = Button_action;
            MyAPIGateway.TerminalControls.AddControl<T>(button);
        }

        public static DeconstructorMod GetBlock(IMyTerminalBlock block) => block?.GameLogic?.GetAs<DeconstructorMod>();

        static bool VisibilityCheck(IMyTerminalBlock block)
        {
            return GetBlock(block) != null;
        }

        static bool EnabledCheck(IMyTerminalBlock block)
        {
            var system = GetBlock(block);
            return system != null && !system.Settings.IsGrinding;
        }

        static void Button_action(IMyTerminalBlock block)
        {
            var system = GetBlock(block);
            if (system != null && system.SGrid != null)
            {
                DeconstructorSession.Instance.CachedPacketServer.Send(system.deconstructor.EntityId, system.SGrid.EntityId, system.Settings.Efficiency);
            }
        }

        static void List_selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var system = GetBlock(block);
            if (system != null && system.Grids != null && system.Grids.Count > 0)
            {
                if (selected.Count > 0)
                {
                    system.SGrid = selected.First().UserData as IMyCubeGrid;
                }
                else
                    system.SGrid = null;
            }
        }

        static void List_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            var system = GetBlock(block);
            if (system != null && system.Grids != null && system.Grids.Count > 0)
            {
                foreach (var item in system.Grids)
                {
                    var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(item.CustomName), MyStringId.NullOrEmpty, item);
                    items.Add(BoxItem);
                }
            }

            if (system.SGrid != null)
                selected.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(system.SGrid.CustomName), MyStringId.NullOrEmpty, system.SGrid));

        }

        static void Slider_setter(IMyTerminalBlock block, float value)
        {
            var system = GetBlock(block);
            if (system != null)
            {
                system.Efficiency = (float)Math.Floor(value);
            }
        }

        static float Slider_getter(IMyTerminalBlock block)
        {
            var system = GetBlock(block);
            if (system != null)
            {
                return system.Efficiency;
            }
            return 0;
        }

        static void Slider_writer(IMyTerminalBlock block, StringBuilder info)
        {
            var system = GetBlock(block);
            if (system != null)
                info.Append($"{system.Efficiency}%");
        }

    }
}
