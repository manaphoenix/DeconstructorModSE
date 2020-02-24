using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
	class DeconstructorTerminal
	{
		private static readonly List<IMyTerminalControl> CustomControls = new List<IMyTerminalControl>();
		private static readonly List<IMyTerminalAction> CustomActions = new List<IMyTerminalAction>();
		public static IMyTerminalControlListbox StaticGridList;

		public static DeconstructorMod IsModBlock(IMyTerminalBlock Block)
		{
			return (Block != null && Block.GameLogic != null) ? Block.GameLogic.GetAs<DeconstructorMod>() : null;
		}

		public static void InitControls()
		{
			MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
			MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
			

			var gridList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipGrinder>("Grids");
			StaticGridList = gridList;
			gridList.Multiselect = false;
			gridList.SupportsMultipleBlocks = false;
			gridList.VisibleRowsCount = 8;
			gridList.Title = MyStringId.GetOrCompute("Grindable Grids");
			gridList.ItemSelected = (block, selected) =>
			{
				var system = IsModBlock(block);
				if (system != null && system.Grids != null && system.Grids.Count > 0)
				{
					if (selected.Count > 0)
					{
						system.SelectedGrid = selected.First().UserData as IMyCubeGrid;
						system.GetGrindTime();
					}
					else
						system.SelectedGrid = null;
				}
			};
			gridList.ListContent = (block, items, selected) =>
			{
				var system = IsModBlock(block);
				if (system != null && system.Grids != null && system.Grids.Count > 0)
				{
					foreach (var item in system.Grids) 
					{
						var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(item.CustomName), MyStringId.NullOrEmpty, item);
						items.Add(BoxItem);
					}
				}
				
				if (system.SelectedGrid != null)
					selected.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(system.SelectedGrid.CustomName), MyStringId.NullOrEmpty, system.SelectedGrid));

			};
			gridList.Enabled = (block) =>
			{
				var system = IsModBlock(block);
				return system != null && !system.isGrinding;
			};
			CustomControls.Add(gridList);

			var efficiency = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipGrinder>("Efficiency");
			efficiency.Enabled = (block) =>
			{
				var system = IsModBlock(block);
				return system != null && !system.isGrinding;
			};
			efficiency.SetLimits(0, 99);
			efficiency.SupportsMultipleBlocks = false;
			efficiency.Title = MyStringId.GetOrCompute("Efficiency");
			efficiency.Tooltip = MyStringId.GetOrCompute("Reduces Deconstruction time, But increases Power Requirement");
			efficiency.Setter = (block, value) =>
			{
				var system = IsModBlock(block);
				if (system != null)
				{
					value = (float)Math.Floor(value);
					system.Efficiency = (int)value;
					if (system.SelectedGrid != null)
						system.GetGrindTime();
				}
			};
			efficiency.Getter = (block) =>
			{
				var system = IsModBlock(block);
				if (system != null)
				{
					return system.Efficiency;
				}
				return 0;
			};
			efficiency.Writer = (block, info) =>
			{
				var system = IsModBlock(block);
				if (system != null)
				{
					info.Clear();
					info.Append($"{system.Efficiency}");
				}
			};
			CustomControls.Add(efficiency);

			var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipGrinder>("StartDecon");
			button.SupportsMultipleBlocks = false;
			button.Title = MyStringId.GetOrCompute("Select");
			button.Action = (block) =>
			{
				var system = IsModBlock(block);
				if (system != null && system.SelectedGrid != null)
					system.DeconstructGrid();
				gridList.UpdateVisual();
				button.UpdateVisual();
				efficiency.UpdateVisual();
			};
			button.Enabled = (block) =>
			{
				var system = IsModBlock(block);
				return system != null && !system.isGrinding;
			};
			CustomControls.Add(button);

			//Actions
			var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipGrinder>("StartDeconAct");
			action.ValidForGroups = false;
			action.Enabled = button.Enabled;
			action.Name = new StringBuilder("Deconstruct");
			action.Writer = (block, result) =>
			{
				result.Append("Deconstructs Selected Grid");
			};
			action.Action = (block) =>
			{
				var system = IsModBlock(block);
				if (system != null && system.SelectedGrid != null)
					system.DeconstructGrid();
			};
			CustomActions.Add(action);

			//Properties
			//TODO?
		}

		private static void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
		{
			if (block.BlockDefinition.SubtypeName.Equals("LargeDeconstructor"))
			{
				foreach (var action in CustomActions)
				{
					actions.Add(action);
				}
			}
		}

		private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
		{
			if (block.BlockDefinition.SubtypeName.Equals("LargeDeconstructor"))
			{
				foreach (var control in CustomControls)
				{
					controls.Add(control);
				}
			}
		}
	}
}
