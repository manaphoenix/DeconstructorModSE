using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
	public static class DeconstructorTerminalInit
	{
		public static bool _TerminalInit = false;

		public static void InitControls<T>()
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

			var TimerBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, T>("Timer");
			TimerBox.Visible = VisibilityCheck;
			TimerBox.Enabled = x => false;
			TimerBox.SupportsMultipleBlocks = false;
			TimerBox.Getter = TextBoxGetter;
			TimerBox.Title = MyStringId.GetOrCompute("Grind Time");
			MyAPIGateway.TerminalControls.AddControl<T>(TimerBox);

			var efficiency = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>("Efficiency");
			efficiency.Enabled = EnabledCheck;
			efficiency.Visible = VisibilityCheck;
			efficiency.SetLimits(0, 99);
			efficiency.SupportsMultipleBlocks = false;
			efficiency.Title = MyStringId.GetOrCompute("Efficiency");
			efficiency.Tooltip = MyStringId.GetOrCompute("Reduces deconstruction time, but increases power required");
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

			var componentList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Components");
			componentList.Visible = VisibilityCheck;
			componentList.Multiselect = false;
			componentList.SupportsMultipleBlocks = false;
			componentList.VisibleRowsCount = 8;
			componentList.Title = MyStringId.GetOrCompute("Components");
			componentList.ListContent = ComponentList_content;
			MyAPIGateway.TerminalControls.AddControl<T>(componentList);

			var api = ImmutableDictionary.CreateBuilder<string, Delegate>();

			api.Add("GetComponents", new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, List<VRage.Game.ModAPI.Ingame.MyInventoryItem>>(GetComponents));
			api.Add("CheckGrid", new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, string, StringBuilder>(CheckGrid));
			// more...

			DeconstructorSession.Instance.APIMethods = api.ToImmutable();

			var p = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, T>("DeconstructorModAPI");
			p.Getter = (b) => DeconstructorSession.Instance.APIMethods;
			p.Setter = (b, v) => { };
			MyAPIGateway.TerminalControls.AddControl<T>(p);

			DeconstructorSession.Instance.DeconButton = button;
			DeconstructorSession.Instance.EfficiencySlider = efficiency;
			DeconstructorSession.Instance.GridList = gridList;
			DeconstructorSession.Instance.TimerBox = TimerBox;
			DeconstructorSession.Instance.ComponentList = componentList;
		}

		public static DeconstructorMod GetBlock(IMyTerminalBlock block) => block?.GameLogic?.GetAs<DeconstructorMod>();

		private static void CheckGrid(Sandbox.ModAPI.Ingame.IMyTerminalBlock deconstructor, string gridName, StringBuilder output)
		{
			var system = GetBlock((IMyTerminalBlock)deconstructor);
			if (system == null) output.AppendLine("block does not exist... how did you get this?");

			var grid = Utils.Grids.FirstOrDefault(x => x.DisplayName == gridName);
			if (grid == null) output.AppendLine("Grid does not exist!");

			if (grid.IsSameConstructAs(deconstructor.CubeGrid) || grid.Physics == null)
				output.AppendLine("Grid cannot be deconstructed because it is attached to the same grid as the block or does not exist!");

			if ((grid.GetPosition() - deconstructor.GetPosition()).Length() > 500)
				output.AppendLine("Grid is too far away");

			var cubGrid = grid as MyCubeGrid;
			if (cubGrid == null || cubGrid.GetBiggestGridInGroup() != cubGrid)
				output.AppendLine("Grid is not a valid cube grid or not the biggest grid in its group");

			var bigOwners = grid.BigOwners;
			var gridOwner = bigOwners.Count > 0 ? bigOwners[0] : long.MaxValue;
			var relationship = gridOwner != long.MaxValue ? MyIDModule.GetRelationPlayerBlock(deconstructor.OwnerId, gridOwner, MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;

			if (relationship == MyRelationsBetweenPlayerAndBlock.Enemies || (gridOwner != deconstructor.OwnerId && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare))
				output.AppendLine("Grid is owned by an enemy or not owned by you");

			if (((MyCubeGrid)grid).GetFatBlocks().Any(block => !Utils.SearchBlocks(block, (IMyTerminalBlock)deconstructor)))
				output.AppendLine("One or more of the important blocks is not owned by the grid owner");

			output.AppendLine("Grid is valid");
		}

		private static void GetComponents(Sandbox.ModAPI.Ingame.IMyTerminalBlock b, List<VRage.Game.ModAPI.Ingame.MyInventoryItem> items)
		{
			var system = GetBlock((IMyTerminalBlock)b);
			if (system == null || system.Settings.Items == null || items == null) return;

			foreach (var item in system.Settings.Items)
			{
				items.Add(new VRage.Game.ModAPI.Ingame.MyInventoryItem(item.PhysicalContent.GetId(), item.ItemId, item.Amount));
			}
		}

		private static StringBuilder TextBoxGetter(IMyTerminalBlock b)
		{
			var system = GetBlock(b);
			if (system == null || system.Settings == null || system.Settings.Time <= 0)
				return new StringBuilder("N/A");
			var time = TimeSpan.FromSeconds(system.Settings.Time);
			return new StringBuilder().Append($"{time:hh'h 'mm'm 'ss's '}");
		}

		private static bool VisibilityCheck(IMyTerminalBlock block)
		{
			return GetBlock(block) is object;
		}

		private static bool EnabledCheck(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system?.Settings != null && !system.Settings.IsGrinding;
		}

		private static void Button_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (system?.SelectedGrid != null)
			{
				DeconstructorSession.Instance.CachedPacketServer.Send(system.Entity.EntityId, system.SelectedGrid.EntityId, system.Settings.Efficiency);
				UpdateVisuals();
			}
		}

		private static void UpdateVisuals()
		{
			DeconstructorSession.Instance.DeconButton.UpdateVisual();
			DeconstructorSession.Instance.EfficiencySlider.UpdateVisual();
			DeconstructorSession.Instance.GridList.UpdateVisual();
			DeconstructorSession.Instance.ComponentList.UpdateVisual();
		}

		private static void List_selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system != null)
			{
				system.SelectedGrid = selected.Count > 0 ? selected.First().UserData as IMyCubeGrid : null;
			}
		}

		private static void ComponentList_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system?.Settings.Items != null)
			{
				foreach (var item in system.Settings.Items)
				{
					var name = $"{item.PhysicalContent.SubtypeName}: {item.Amount}";
					items.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name), MyStringId.NullOrEmpty, null));
				}
			}
		}

		private static void List_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system?.Grids != null)
			{
				foreach (var item in system.Grids)
				{
					AddGridItem(items, item, system);
				}

				if (system.SelectedGrid != null)
				{
					AddGridItem(selected, system.SelectedGrid, system);
				}
			}
		}

		private static void AddGridItem(List<MyTerminalControlListBoxItem> list, IMyCubeGrid grid, DeconstructorMod system)
		{
			var distanceFromDeconstructor = Math.Round((grid.GetPosition() - system.Entity.GetPosition()).Length(), 2);
			var listBoxString = MyStringId.GetOrCompute($"{grid.CustomName} - {distanceFromDeconstructor}m");
			var gridPCU = ((MyCubeGrid)grid).BlocksPCU;

			list.Add(new MyTerminalControlListBoxItem(listBoxString, MyStringId.GetOrCompute($"{distanceFromDeconstructor}m | {gridPCU}PCU"), grid));
		}

		private static void Slider_setter(IMyTerminalBlock block, float value)
		{
			var system = GetBlock(block);
			if (system != null)
			{
				system.Efficiency = (float)Math.Floor(value);
			}
		}

		private static float Slider_getter(IMyTerminalBlock block)
		{
			return GetBlock(block)?.Efficiency ?? 0;
		}

		private static void Slider_writer(IMyTerminalBlock block, StringBuilder info)
		{
			var system = GetBlock(block);
			info.Append(system?.Efficiency.ToString() + "%");
		}
	}
}