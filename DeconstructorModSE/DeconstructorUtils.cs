using Sandbox.Definitions;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace DeconstructorModSE
{
	public static class Utils
	{
		public readonly static List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
		private static Dictionary<MyDefinitionId, MyPhysicalInventoryItem> TempItems = new Dictionary<MyDefinitionId, MyPhysicalInventoryItem>();
		//public readonly static Dictionary<long, IMyCubeGrid> Grids = new Dictionary<long, IMyCubeGrid>();

		public static void DeconstructGrid(IMyInventory inventory, ref IMyCubeGrid SelectedGrid, ref List<MyObjectBuilder_InventoryItem> Items)
		{
			Items.Clear();
			var Blocks = new List<IMySlimBlock>();
			SelectedGrid.GetBlocks(Blocks);

			// get subgrids
			var gridGroup = new List<IMyCubeGrid>();
			SelectedGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(gridGroup);
			foreach (var grid in gridGroup)
			{
				if (grid.EntityId == SelectedGrid.EntityId)
					continue;

				grid.GetBlocks(Blocks);
			}
			MyObjectBuilder_PhysicalObject physicalObjBuilder;
			MyPhysicalInventoryItem phys;
			MyObjectBuilder_CubeBlock Obj;
			var InvItems = new List<VRage.Game.ModAPI.Ingame.MyInventoryItem>();

			MyDefinitionId Id;

			foreach (var block in Blocks)
			{
				block.FullyDismount(inventory);
				Obj = block.GetObjectBuilder();
				if (Obj.ConstructionStockpile != null)
				{
					for (var i = 0; i < Obj.ConstructionStockpile.Items.Count(); i++)
					{
						phys = new MyPhysicalInventoryItem(Obj.ConstructionStockpile.Items[i].Amount, Obj.ConstructionStockpile.Items[i].PhysicalContent);
						Id = phys.Content.GetObjectId();
						if (!TempItems.ContainsKey(Id))
							TempItems.Add(Id, phys);
						else
							TempItems[Id] = new MyPhysicalInventoryItem(phys.Amount + TempItems[Id].Amount, phys.Content);
					}
				}
				if (block.FatBlock != null && block.FatBlock.HasInventory)
				{
					InvItems.Clear();
					block.FatBlock.GetInventory().GetItems(InvItems);

					for (var i = 0; i < InvItems.Count; i++)
					{
						physicalObjBuilder = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject((MyDefinitionId)InvItems[i].Type);
						phys = new MyPhysicalInventoryItem(InvItems[i].Amount, physicalObjBuilder);
						Id = phys.Content.GetObjectId();
						if (!TempItems.ContainsKey(Id))
							TempItems.Add(Id, phys);
						else
							TempItems[Id] = new MyPhysicalInventoryItem(phys.Amount + TempItems[Id].Amount, phys.Content);
					}
				}
			}

			foreach (var item in TempItems)
			{
				Items.Add(item.Value.GetObjectBuilder());
			}
			TempItems.Clear();
		}

		public static void GetGrindTime(DeconstructorMod MyBlock, ref IMyCubeGrid SelectedGrid, ref float totalTime, bool calcEff = true)
		{
			totalTime = 0;
			var Blocks = new List<IMySlimBlock>();
			SelectedGrid.GetBlocks(Blocks);
			var gridGroup = new List<IMyCubeGrid>();
			SelectedGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(gridGroup);
			foreach (var grid in gridGroup)
			{
				if (grid.EntityId == SelectedGrid.EntityId)
					continue;

				grid.GetBlocks(Blocks);
			}

			float grindRatio = 0;
			float integrity = 0;
			float grindTime;
			MyCubeBlockDefinition def;

			foreach (var block in Blocks)
			{
				if (block.BlockDefinition.Id != null)
				{
					def = MyDefinitionManager.Static.GetDefinition(block.BlockDefinition.Id) as MyCubeBlockDefinition;
					if (def != null)
					{
						grindRatio = def.DisassembleRatio;
						integrity = def.IntegrityPointsPerSec;
					}
				}

				grindTime = block.MaxIntegrity / integrity / DeconstructorSession.Instance.Session.WelderSpeedMultiplier / (1f / grindRatio) / DeconstructorSession.Instance.Session.GrinderSpeedMultiplier;
				totalTime += grindTime * block.BuildLevelRatio;
			}

			if (calcEff)
				totalTime *= (100.0f - MyBlock.Settings.Efficiency) / 100.0f;
			else
				totalTime *= 100.0f / 100.0f;
		}

		public static void SpawnItems(IMyInventory MyInventory, ref List<MyObjectBuilder_InventoryItem> Items)
		{
			MyFixedPoint amount;

			for (var i = Items.Count - 1; i >= 0; i--)
			{
				amount = GetMaxAmountPossible(MyInventory, Items[i]);
				if (amount > 0)
				{
					MyInventory.AddItems(amount, Items[i].PhysicalContent);
					if ((Items[i].Amount - amount) > 0)
					{
						Items[i].Amount -= amount;
					}
					else
					{
						Items.RemoveAtFast(i);
					}
				}
			}
		}

		public static MyFixedPoint GetMaxAmountPossible(IMyInventory inv, MyObjectBuilder_InventoryItem Item)
		{
			var def = MyDefinitionManager.Static.GetPhysicalItemDefinition(Item.PhysicalContent.GetId());
			var MaxAmount = (int)((inv.MaxVolume - inv.CurrentVolume).RawValue / (def.Volume * 1000000));

			return MaxAmount > Item.Amount ? Item.Amount : MaxAmount;
		}
	}
}