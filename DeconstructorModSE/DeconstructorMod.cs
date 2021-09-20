using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyShipGrinder = Sandbox.ModAPI.IMyShipGrinder;

namespace DeconstructorModSE
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipGrinder), false, "LargeDeconstructor")]
	public class DeconstructorMod : MyGameLogicComponent
	{
		//Settings
		public const float Efficiency_Min = 0;

		public const float Efficiency_Max = 99;
		public const float Range = 500;
		public const float Power = 0.002f; //in MW
		public const int SETTINGS_CHANGED_COUNTDOWN = 10; // div by 10 because it runs in update10
		public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
		public readonly DeconstructorBlockSettings Settings = new DeconstructorBlockSettings();
		private IMyShipGrinder deconstructor;

		private IMyInventory MyInventory;
		public List<IMyCubeGrid> Grids;
		private IMyCubeGrid _SGrid;

		private readonly HashSet<MyStringHash> ImportantSubTypes = new HashSet<MyStringHash>(MyStringHash.Comparer)
		{
			MyStringHash.GetOrCompute("CockpitOpen"),
			MyStringHash.GetOrCompute("DBSmallBlockFighterCockpit"),
			MyStringHash.GetOrCompute("LargeBlockCockpit"),
			MyStringHash.GetOrCompute("LargeBlockCockpitIndustrial"),
			MyStringHash.GetOrCompute("LargeBlockCockpitSeat"),
			MyStringHash.GetOrCompute("OpenCockpitLarge"),
			MyStringHash.GetOrCompute("OpenCockpitSmall"),
			MyStringHash.GetOrCompute("SmallBlockCockpit"),
			MyStringHash.GetOrCompute("SmallBlockCockpitIndustrial"),
			MyStringHash.GetOrCompute("RoverCockpit"),
			MyStringHash.GetOrCompute("BuggyCockpit"),
			MyStringHash.GetOrCompute("LargeBlockBed"),
			MyStringHash.GetOrCompute("SmallBlockRemoteControl"),
			MyStringHash.GetOrCompute("LargeBlockRemoteControl"),
			MyStringHash.GetOrCompute("LargeMedicalRoom"),
			MyStringHash.GetOrCompute("LargeBlockCryoChamber"),
			MyStringHash.GetOrCompute("SmallBlockCryoChamber"),
			MyStringHash.GetOrCompute("SurvivalKitLarge"),
			MyStringHash.GetOrCompute("SurvivalKit")
		};

		public IMyCubeGrid SelectedGrid
		{
			get { return _SGrid; }
			set
			{
				if (_SGrid != value)
				{
					_SGrid = value;
					GetGrindTime(value);
					DeconstructorSession.Instance.TimerBox.UpdateVisual();
				}
			}
		}

		private MyResourceSinkComponent sink;
		private int syncCountdown;
		private DeconstructorSession Mod => DeconstructorSession.Instance;

		public float Efficiency
		{
			get { return Settings.Efficiency; }
			set
			{
				var val = MathHelper.Clamp(value, Efficiency_Min, Efficiency_Max);
				if (Settings.Efficiency != val)
				{
					Settings.Efficiency = val;
					SettingsChanged();
					if (_SGrid != null)
					{
						GetGrindTime(_SGrid);
						DeconstructorSession.Instance.TimerBox.UpdateVisual();
					}
				}
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

		public float GetPowerRequired()
		{
			if (Settings.IsGrinding) return sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
			if (_SGrid == null) return Power;

			float time = 0;
			Utils.GetGrindTime(this, ref _SGrid, ref time, false);
			var removedTime = time / (1 - (Efficiency / 100));
			return Power + (removedTime / 1000 / 60 / 2);
		}

		public void SetPower(bool Working = false)
		{
			var powerRequired = GetPowerRequired();
			if (Working)
			{
				sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
				sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
			}
			else
			{
				sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Power); // In MW
				sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Power); // In MW
			}
			sink.Update();
		}

		public void DeconstructGrid(IMyCubeGrid SelectedGrid)
		{
			if (SelectedGrid == null || Settings.Items.Count > 0) return;
			SetPower(true);
			Settings.IsGrinding = true;
			Utils.DeconstructGrid(MyInventory, ref SelectedGrid, ref Settings.Items);
			SelectedGrid.Delete();
		}

		public void GetGrindTime(IMyCubeGrid SelectedGrid, bool calcEff = true)
		{
			Utils.GetGrindTime(this, ref SelectedGrid, ref Settings.Time, calcEff);
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

			/*
            var t = new MyObjectBuilder_GasTankDefinition();
            t.Capacity = 10000;
            if (t.StoredGasId.IsNull())
            {
                t.StoredGasId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Oxygen");
            }
            */

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
				info.Append($"Power Required: {Math.Round(GetPowerRequired() * 1000, 2)}Kw\n");
			}
		}

		// Saving
		private bool LoadSettings()
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

		private void SaveSettings()
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

		private void SettingsChanged()
		{
			if (syncCountdown == 0)
				syncCountdown = SETTINGS_CHANGED_COUNTDOWN;
		}

		private void SyncSettings()
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

				if (Settings.Time <= 0 && Settings.IsGrinding)
				{
					if (Settings.Items.Count > 0)
					{
						Utils.SpawnItems(MyInventory, ref Settings.Items);
						DeconstructorSession.Instance.ComponentList.UpdateVisual();
					}
					else
					{
						Settings.IsGrinding = false;
						SetPower();
					}
				}
				else
				{
					if (Settings.IsGrinding)
					{
						NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
					}
				}

				Grids.Clear();
				foreach (var grid in Utils.Grids)
				{
					if (grid.IsSameConstructAs(deconstructor.CubeGrid)) continue;
					if ((grid.GetPosition() - deconstructor.GetPosition()).Length() > Range) continue;
					if (grid.Physics == null) continue;

					foreach (var block in ((MyCubeGrid)grid).GetFatBlocks())
					{
						if (!SearchBlocks(block)) continue;

						Grids.Add(grid);
					}
				}
			}
			else
				Grids.Clear();
		}

		private bool SearchBlocks(MyCubeBlock block)
		{
			if (block == null) return false;
			if (ImportantSubTypes.Contains(MyStringHash.GetOrCompute(block.BlockDefinition.Id.SubtypeName)))
			{
				return block.OwnerId == deconstructor.OwnerId || block.OwnerId == 0;
			}

			return false;
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
					DeconstructorSession.Instance.TimerBox.UpdateVisual();
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