using Sandbox.Definitions;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace DeconstructorModSE
{
    public static class Utils
	{
        public readonly static List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        //public readonly static Dictionary<long, IMyCubeGrid> Grids = new Dictionary<long, IMyCubeGrid>();

        public static void DeconstructGrid(IMyInventory inventory, ref IMyCubeGrid SelectedGrid, ref Dictionary<MyDefinitionId, MyPhysicalInventoryItem> Items)
        {
            var Blocks = new List<IMySlimBlock>();
            SelectedGrid.GetBlocks(Blocks);
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
                        if (!Items.ContainsKey(Id))
                            Items.Add(Id, phys);
                        else
                            Items[Id] = new MyPhysicalInventoryItem(phys.Amount + Items[Id].Amount, phys.Content);
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
                        if (!Items.ContainsKey(Id))
                            Items.Add(Id, phys);
                        else
                            Items[Id] = new MyPhysicalInventoryItem(phys.Amount + Items[Id].Amount, phys.Content);
                    }
                }
            }

            DeconstructorSession.SendMessage("Item Counter", $"Items: {Items.Count}");
        }

        public static void GetGrindTime(DeconstructorMod MyBlock, ref IMyCubeGrid SelectedGrid, ref float totalTime)
        {
            totalTime = 0;
            var Blocks = new List<IMySlimBlock>();
            SelectedGrid.GetBlocks(Blocks);

            float grindRatio = 0;
            float integrity = 0;
            float grindTime = 0;
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

            totalTime *= (100.0f - MyBlock.Efficiency) / 100.0f;
        }

        public static void SpawnItems(IMyInventory MyInventory, ref Dictionary<MyDefinitionId, MyPhysicalInventoryItem> Items)
        {
            var TempList = new Dictionary<MyDefinitionId, MyPhysicalInventoryItem>();
            MyFixedPoint amount;
            foreach (var item in Items)
            {
                amount = GetMaxAmountPossible(MyInventory, item);
                if (amount > 0)
                {
                    MyInventory.AddItems(amount, item.Value.Content);
                    if ((item.Value.Amount - amount) > 0)
                    {
                        TempList.Add(item.Key, new MyPhysicalInventoryItem(item.Value.Amount - amount, item.Value.Content));
                    }
                }
                else
                {
                    TempList.Add(item.Key,item.Value);
                }
            }
            Items = TempList;
            
        }

        public static MyFixedPoint GetMaxAmountPossible(IMyInventory inv, KeyValuePair<MyDefinitionId, MyPhysicalInventoryItem> Item)
        {
            var def = MyDefinitionManager.Static.GetPhysicalItemDefinition(Item.Value.Content.GetId());
            var MaxAmount = (int)((inv.MaxVolume - inv.CurrentVolume).RawValue / (def.Volume*1000000));

            return MaxAmount > Item.Value.Amount ? Item.Value.Amount : MaxAmount;
        }
    }
}
