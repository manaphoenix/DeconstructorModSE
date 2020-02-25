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
	public class DeconstructorTerminal
	{
		
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

			var efficiency = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T> ("Efficiency");
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

		public static DeconstructorMod IsModBlock(IMyTerminalBlock block) => block?.GameLogic?.GetAs<DeconstructorMod>();

		static bool VisibilityCheck(IMyTerminalBlock block)
		{
			return IsModBlock(block) != null;
		}

		static bool EnabledCheck(IMyTerminalBlock block)
		{
			var system = IsModBlock(block);
			return system != null && !system.isGrinding;
		}

		static void Button_action(IMyTerminalBlock block)
		{
			var system = IsModBlock(block);
			if (system != null && system.SGrid != null)
			{
				DeconstructorSession.Instance.Net.SendToServer(new DeconstructorPacketData(system.Entity.EntityId, system.SGrid.EntityId, system.Efficiency));
			}
		}

		static void List_selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			var system = IsModBlock(block);
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
			var system = IsModBlock(block);
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
			var system = IsModBlock(block);
			if (system != null)
			{
				system.Efficiency = (int)(float)Math.Floor(value);
			}
		}

		static float Slider_getter(IMyTerminalBlock block)
		{
			var system = IsModBlock(block);
			if (system != null)
			{
				return system.Efficiency;
			}
			return 0;
		}

		static void Slider_writer(IMyTerminalBlock block, StringBuilder info)
		{
			var system = IsModBlock(block);
			if (system != null)
				info.Append($"{system.Efficiency}%");
		}
	}
}
