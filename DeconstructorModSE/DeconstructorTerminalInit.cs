using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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

			api.Add("GetComponents", new Action<IMyTerminalBlock, List<VRage.Game.ModAPI.Ingame.MyInventoryItem>>(GetComponents));
			// more...

			DeconstructorSession.Instance.APIMethods = api.ToImmutable();

			var p = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, DeconstructorMod>("DeconstructorModAPI");
			p.Getter = (b) => DeconstructorSession.Instance.APIMethods;
			p.Setter = (b, v) => { };
			MyAPIGateway.TerminalControls.AddControl<DeconstructorMod>(p);

			DeconstructorSession.Instance.DeconButton = button;
			DeconstructorSession.Instance.EfficiencySlider = efficiency;
			DeconstructorSession.Instance.GridList = gridList;
			DeconstructorSession.Instance.TimerBox = TimerBox;
			DeconstructorSession.Instance.ComponentList = componentList;
		}

		public static DeconstructorMod GetBlock(IMyTerminalBlock block) => block?.GameLogic?.GetAs<DeconstructorMod>();

		private static void GetComponents(IMyTerminalBlock b, List<VRage.Game.ModAPI.Ingame.MyInventoryItem> items)
		{
			var system = GetBlock(b);
			if (system == null) return;
			if (items == null) return;

			for (var i = system.Settings.Items.Count - 1; i > -1; i--)
			{
				var item = system.Settings.Items[i];
				var InvItem = new VRage.Game.ModAPI.Ingame.MyInventoryItem(new VRage.Game.ModAPI.Ingame.MyItemType(item.TypeId, item.SubtypeId), item.ItemId, item.Amount);
				items.Add(InvItem);
			}
		}

		private static StringBuilder TextBoxGetter(IMyTerminalBlock b)
		{
			var system = GetBlock(b);
			if (system == null) return new StringBuilder();
			var Builder = new StringBuilder();
			if (system.Settings != null && system.Settings.Time > 0)
			{
				var time = TimeSpan.FromSeconds(system.Settings.Time);
				return Builder.Append($"{time:hh'h 'mm'm 'ss's '}");
			}
			else
			{
				return Builder.Append("N/A");
			}
		}

		private static bool VisibilityCheck(IMyTerminalBlock block)
		{
			return GetBlock(block) != null;
		}

		private static bool EnabledCheck(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && !system.Settings.IsGrinding;
		}

		private static void Button_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (system != null && system.SelectedGrid != null)
			{
				DeconstructorSession.Instance.CachedPacketServer.Send(system.Entity.EntityId, system.SelectedGrid.EntityId, system.Settings.Efficiency);
				DeconstructorSession.Instance.DeconButton.UpdateVisual();
				DeconstructorSession.Instance.EfficiencySlider.UpdateVisual();
				DeconstructorSession.Instance.GridList.UpdateVisual();
				DeconstructorSession.Instance.ComponentList.UpdateVisual();
			}
		}

		private static void List_selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system != null && system.Grids != null && system.Grids.Count > 0)
			{
				if (selected.Count > 0)
				{
					system.SelectedGrid = selected.First().UserData as IMyCubeGrid;
				}
				else
					system.SelectedGrid = null;
			}
		}

		private static void ComponentList_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system != null && system.Settings.Items != null && system.Settings.Items.Count > 0)
			{
				for (var i = system.Settings.Items.Count - 1; i > -1; i--)
				{
					var item = system.Settings.Items[i];
					var name = item.PhysicalContent.SubtypeName + ": " + item.Amount;
					var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name.ToString()), MyStringId.NullOrEmpty, null);
					items.Add(BoxItem);
				}
			}
		}

		private static void List_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
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

			if (system.SelectedGrid != null)
				selected.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(system.SelectedGrid.CustomName), MyStringId.NullOrEmpty, system.SelectedGrid));
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
			var system = GetBlock(block);
			if (system != null)
			{
				return system.Efficiency;
			}
			return 0;
		}

		private static void Slider_writer(IMyTerminalBlock block, StringBuilder info)
		{
			var system = GetBlock(block);
			if (system != null)
				info.Append($"{system.Efficiency}%");
		}
	}
}