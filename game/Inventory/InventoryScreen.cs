using System;
using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

// O Inventário, como no mockup: o Personagem equipado ao centro das caixinhas, a biblioteca em grade e o painel de comparação,
// com as abas Inventário e Cosméticos e o chip do Papel. Esc fecha.
public partial class InventoryScreen : OverlayScreen
{
	const float DollWidth = 340f;
	const float DollHeight = 440f;

	readonly InventoryHost host;
	readonly GearTab gearTab;
	readonly CosmeticsTab cosmeticsTab;
	readonly CharacterPortrait portrait;
	readonly ScrollContainer libraryScroll = new() { Name = "LibraryScroll", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
	readonly PanelContainer comparisonPanel = new() { Name = "ComparisonPanel", CustomMinimumSize = new Vector2(DollWidth, 0) };
	Button? gearButton;
	Button? cosmeticsButton;
	bool onGear = true;

	public InventoryScreen(InventoryHost host)
	{
		Name = "InventoryScreen";
		this.host = host;
		portrait = new CharacterPortrait(host.Portrait);
		gearTab = new GearTab(host, portrait.Show);
		cosmeticsTab = new CosmeticsTab(host, portrait.Show, text => ((Label)gearTab.Tag).Text = text);
		gearTab.Updated += RefreshTabs;
		cosmeticsTab.Updated += RefreshTabs;
		comparisonPanel.AddThemeStyleboxOverride("panel", BookStyle.Box(BookStyle.Selected));
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
		page.AddThemeConstantOverride("separation", 10);
		page.AddChild(BuildHeader());
		page.AddChild(BuildTabs());
		page.AddChild(BuildBody());
		var window = BookStyle.Panel(page);
		window.Name = "InventoryWindow";
		window.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		window.OffsetLeft = 24;
		window.OffsetTop = 16;
		window.OffsetRight = -24;
		window.OffsetBottom = -16;
		return window;
	}

	Control BuildHeader()
	{
		var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
		row.AddThemeConstantOverride("separation", 8);
		var title = BookStyle.Text("Inventário", 28);
		title.AutowrapMode = TextServer.AutowrapMode.Off;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(title);
		row.AddChild(BookStyle.Button("?", OpenGuide, "GuideButton", 56));
		row.AddChild(BookStyle.Button("Fechar", Close, "CloseInventory", 56));
		return row;
	}

	// As abas à esquerda e, à direita, o chip do Papel de quem está equipado.
	Control BuildTabs()
	{
		var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
		row.AddThemeConstantOverride("separation", 8);
		gearButton = BookStyle.Button("", () => ShowTab(gear: true), "TabGear", 56);
		cosmeticsButton = BookStyle.Button("", () => ShowTab(gear: false), "TabCosmetics", 56);
		gearButton.CustomMinimumSize = new Vector2(220, 56);
		cosmeticsButton.CustomMinimumSize = new Vector2(220, 56);
		row.AddChild(gearButton);
		row.AddChild(cosmeticsButton);
		row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		var chip = BookStyle.Button(host.RoleName, () => { Close(); host.SwitchRole!(); }, "RoleChip", 56);
		chip.Icon = Icons.For("swap", 28);
		chip.IconAlignment = HorizontalAlignment.Right;
		chip.CustomMinimumSize = new Vector2(220, 56);
		chip.Disabled = host.SwitchRole is null;
		chip.TooltipText = host.SwitchRole is null ? "Esta torre tem um Papel só" : "Trocar de Papel";
		row.AddChild(chip);
		return row;
	}

	Control BuildBody()
	{
		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 16);
		body.AddChild(BuildDoll());
		body.AddChild(libraryScroll);
		body.AddChild(comparisonPanel);
		return body;
	}

	// O quadro do Personagem: a etiqueta da prévia em cima, as caixinhas dos espaços em volta e o retrato ao centro.
	Control BuildDoll()
	{
		var doll = new Control { Name = "Doll", CustomMinimumSize = new Vector2(DollWidth, DollHeight), SizeFlagsVertical = SizeFlags.ShrinkBegin };
		var background = new Panel { MouseFilter = MouseFilterEnum.Ignore };
		background.AddThemeStyleboxOverride("panel", BookStyle.Box(BookStyle.Selected));
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		doll.AddChild(background);
		portrait.Position = new Vector2(100, 130);
		portrait.Size = new Vector2(140, 300);
		doll.AddChild(portrait);
		var tag = (Label)gearTab.Tag;
		tag.Position = new Vector2(0, 8);
		tag.Size = new Vector2(DollWidth, 24);
		doll.AddChild(tag);
		gearTab.Slots.Position = new Vector2(0, 30);
		doll.AddChild(gearTab.Slots);
		return doll;
	}

	void ShowTab(bool gear)
	{
		onGear = gear;
		foreach (var child in libraryScroll.GetChildren())
			libraryScroll.RemoveChild(child);
		foreach (var child in comparisonPanel.GetChildren())
			comparisonPanel.RemoveChild(child);
		var library = gear ? gearTab.Library : cosmeticsTab.Library;
		library.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		libraryScroll.AddChild(library);
		var comparison = gear ? gearTab.Comparison : cosmeticsTab.Comparison;
		comparison.SizeFlagsVertical = SizeFlags.ExpandFill;
		comparisonPanel.AddChild(comparison);
		comparisonPanel.Visible = false;
		gearTab.Slots.Visible = gear;
		if (gear) gearTab.Refresh();
		else cosmeticsTab.Refresh();
		RefreshTabs();
	}

	void RefreshTabs()
	{
		comparisonPanel.Visible = onGear ? gearTab.HasSelection : cosmeticsTab.HasSelection;
		gearButton!.Text = TabText("Inventário", onGear, HasNewGear());
		cosmeticsButton!.Text = TabText("Cosméticos", !onGear, HasNewCosmetics());
		gearButton.AddThemeStyleboxOverride("normal", onGear ? BookStyle.Emphasized(BookStyle.Selected) : BookStyle.Box());
		cosmeticsButton.AddThemeStyleboxOverride("normal", !onGear ? BookStyle.Emphasized(BookStyle.Selected) : BookStyle.Box());
	}

	static string TabText(string name, bool active, bool isNew) => (active ? "▸ " : "") + name + (isNew ? " · NOVO" : "");

	bool HasNewGear() => host.OwnedGear().Any(id => host.Inventory.IsNew(host.ProfileId, InventoryHost.GearKey(id)));

	bool HasNewCosmetics() => host.OwnedCosmetics().Any(id => host.Inventory.IsNew(host.ProfileId, InventoryHost.CosmeticKey(id)));

	// O que não está à mostra na aba atual fica sem pai; sem liberá-lo ao fechar, vazaria.
	public override void _ExitTree()
	{
		foreach (var part in new Control[] { gearTab.Library, cosmeticsTab.Library, gearTab.Comparison, cosmeticsTab.Comparison })
			if (part.GetParent() is null) part.QueueFree();
		base._ExitTree();
	}

	void OpenGuideIfFirstTime()
	{
		if (host.OwnedGear().Count == 0 || host.Inventory.GuideSeen(host.ProfileId)) return;
		OpenGuide();
	}

	void OpenGuide() => AddChild(new InventoryGuide(host.Say, () => host.Inventory.MarkGuideSeen(host.ProfileId)));
}
