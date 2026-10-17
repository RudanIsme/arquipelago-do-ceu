using System;
using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

// O Inventário: um modal com a aba Inventário (equipamento) e a aba Cosméticos. Esc fecha.
public partial class InventoryScreen : OverlayScreen
{
	const int TargetHeight = 72;

	readonly InventoryHost host;
	readonly GearTab gearTab;
	readonly CosmeticsTab cosmeticsTab;
	readonly CharacterPortrait portrait = new();
	readonly ScrollContainer libraryScroll = new() { Name = "LibraryScroll", SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
	readonly VBoxContainer rightColumn = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
	readonly VBoxContainer leftColumn = new() { CustomMinimumSize = new Vector2(300, 0) };
	Button? gearButton;
	Button? cosmeticsButton;
	bool onGear = true;

	public InventoryScreen(InventoryHost host)
	{
		Name = "InventoryScreen";
		this.host = host;
		gearTab = new GearTab(host, portrait.Show);
		cosmeticsTab = new CosmeticsTab(host, portrait.Show);
		gearTab.Updated += RefreshTabs;
		cosmeticsTab.Updated += RefreshTabs;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Ready()
	{
		base._Ready();
		var shade = new ColorRect { Color = new Color(0.03f, 0.06f, 0.09f, 0.45f), MouseFilter = MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);
		AddChild(gearTab);
		AddChild(cosmeticsTab);
		AddChild(BuildWindow());
		ShowTab(gear: true);
		OpenGuideIfFirstTime();
	}


	Control BuildWindow()
	{
		var page = new VBoxContainer();
		page.AddThemeConstantOverride("separation", 8);
		page.AddChild(BuildHeader());
		page.AddChild(BuildBody());
		var window = BookStyle.Panel(page);
		window.Name = "InventoryWindow";
		window.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SetMargins(window, 16);
		return window;
	}

	static void SetMargins(Control control, float margin)
	{
		control.OffsetLeft = margin;
		control.OffsetTop = margin;
		control.OffsetRight = -margin;
		control.OffsetBottom = -margin;
	}

	Control BuildHeader()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var title = BookStyle.Text($"Inventário · {host.RoleName}", 26);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(title);
		gearButton = BookStyle.Button("", () => ShowTab(gear: true), "TabGear", TargetHeight);
		cosmeticsButton = BookStyle.Button("", () => ShowTab(gear: false), "TabCosmetics", TargetHeight);
		row.AddChild(gearButton);
		row.AddChild(cosmeticsButton);
		row.AddChild(BookStyle.Button("?", OpenGuide, "GuideButton", TargetHeight));
		row.AddChild(BookStyle.Button("Fechar", Close, "CloseInventory", TargetHeight));
		return row;
	}

	Control BuildBody()
	{
		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 16);
		leftColumn.AddThemeConstantOverride("separation", 8);
		leftColumn.AddChild(portrait);
		leftColumn.AddChild(gearTab.Slots);
		body.AddChild(leftColumn);
		rightColumn.AddThemeConstantOverride("separation", 8);
		rightColumn.AddChild(libraryScroll);
		rightColumn.AddChild(gearTab.Comparison);
		body.AddChild(rightColumn);
		return body;
	}

	void ShowTab(bool gear)
	{
		onGear = gear;
		foreach (var child in libraryScroll.GetChildren())
			libraryScroll.RemoveChild(child);
		var library = gear ? gearTab.Library : cosmeticsTab.Library;
		library.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		libraryScroll.AddChild(library);
		gearTab.Slots.Visible = gear;
		gearTab.Comparison.Visible = gear;
		if (gear) gearTab.Refresh();
		else cosmeticsTab.Refresh();
		RefreshTabs();
	}

	void RefreshTabs()
	{
		gearButton!.Text = TabText("Inventário", onGear, HasNewGear());
		cosmeticsButton!.Text = TabText("Cosméticos", !onGear, HasNewCosmetics());
		gearButton.AddThemeStyleboxOverride("normal", onGear ? BookStyle.Emphasized(BookStyle.Selected) : BookStyle.Box());
		cosmeticsButton.AddThemeStyleboxOverride("normal", !onGear ? BookStyle.Emphasized(BookStyle.Selected) : BookStyle.Box());
	}

	static string TabText(string name, bool active, bool isNew) => (active ? "▸ " : "") + name + (isNew ? " · NOVO" : "");

	bool HasNewGear() => host.Catalog.Items.Any(i => host.Inventory.IsNew(host.ProfileId, InventoryHost.GearKey(i.Id)));

	bool HasNewCosmetics() => host.OwnedCosmetics().Any(id => host.Inventory.IsNew(host.ProfileId, InventoryHost.CosmeticKey(id)));

	// A biblioteca da aba que não está à mostra fica sem pai; sem liberá-la ao fechar, ela vazaria.
	public override void _ExitTree()
	{
		foreach (var library in new Control[] { gearTab.Library, cosmeticsTab.Library })
			if (library.GetParent() is null) library.QueueFree();
		base._ExitTree();
	}

	void OpenGuideIfFirstTime()
	{
		if (host.Catalog.Items.Count == 0 || host.Inventory.GuideSeen(host.ProfileId)) return;
		OpenGuide();
	}

	void OpenGuide() => AddChild(new InventoryGuide(host.Say, () => host.Inventory.MarkGuideSeen(host.ProfileId)));
}
