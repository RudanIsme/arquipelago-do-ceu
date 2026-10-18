using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// A aba Inventário, como no mockup: as caixinhas em volta do Personagem, a biblioteca em grade (equipado, novo, bloqueado) e a comparação.
// Apontar, tocar ou focar mostra uma prévia; só o botão Equipar (ou Enter no equipamento focado) vale de verdade.
public sealed partial class GearTab : Node
{
	const int TargetHeight = 72;

	readonly InventoryHost host;
	readonly Action<EquipmentLoadout> showLoadout;
	readonly Control slots = new() { Name = "Slots", MouseFilter = Control.MouseFilterEnum.Ignore };
	readonly VBoxContainer library = new() { Name = "Library" };
	readonly VBoxContainer comparison = new() { Name = "Comparison" };
	readonly Dictionary<string, Button> entryButtons = [];
	// Onde cada caixinha fica em volta do Personagem (x, y no quadro de 340 px): capacete em cima, armadura à esquerda, acessório à direita.
	static readonly (float X, float Y)[] SlotSpots = [(126, 6), (8, 170), (244, 170)];

	readonly Label previewTag = BookStyle.Text("", 16);
	Gear worn = new();
	int mastery;
	GearItem? selected;

	public GearTab(InventoryHost host, Action<EquipmentLoadout> showLoadout)
	{
		this.host = host;
		this.showLoadout = showLoadout;
		slots.AddThemeConstantOverride("separation", 6);
		library.AddThemeConstantOverride("separation", 6);
		comparison.AddThemeConstantOverride("separation", 8);
		previewTag.Name = "PreviewTag";
		previewTag.AutowrapMode = TextServer.AutowrapMode.Off;
		previewTag.HorizontalAlignment = HorizontalAlignment.Center;
	}

	// A etiqueta "Prévia: ..." que fica no alto do Personagem.
	public Control Tag => previewTag;

	public event Action? Updated;

	public Control Slots => slots;

	public Control Library => library;

	public Control Comparison => comparison;

	// Os detalhes só existem com um equipamento escolhido (toque, clique ou foco); passar o mouse só mostra a prévia no Personagem.
	public bool HasSelection => selected is not null;

	public void Refresh()
	{
		worn = host.Worn;
		mastery = host.Mastery();
		BuildSlots();
		BuildLibrary();
		ShowPreview(null);
		BuildComparison(selected);
		FocusSelected();
		Updated?.Invoke();
	}

	void BuildSlots()
	{
		Clear(slots);
		var spot = 0;
		foreach (var slot in host.Catalog.Slots)
		{
			var (x, y) = spot < SlotSpots.Length ? SlotSpots[spot] : (8f + (spot - SlotSpots.Length) * 92f, 330f);
			spot++;
			var box = SlotBox(slot);
			box.Position = new Vector2(x, y);
			slots.AddChild(box);
		}
	}

	// A caixinha do espaço: o equipamento vestido (ou vazio) e, embaixo, o nome do espaço.
	Control SlotBox(GearSlot slot)
	{
		var item = host.Catalog.Find(worn.In(slot.Id));
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(88, 0) };
		column.AddThemeConstantOverride("separation", 3);
		var button = ItemToken.Create(item?.Name ?? "vazio", () => { if (item is not null) Select(item); }, "Slot_" + slot.Id, 84, icon: Icons.For(slot.Id, 56));
		if (item is null) button.Disabled = true;
		column.AddChild(button);
		var label = BookStyle.Text(slot.Name, 16);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(label);
		return column;
	}

	void BuildLibrary()
	{
		Clear(library);
		entryButtons.Clear();
		library.AddChild(Header("Equipamentos", "os usados primeiro"));
		var grid = Grid();
		foreach (var entry in host.Library.Entries(worn, mastery, host.OwnedGear()))
			grid.AddChild(EntryButton(entry));
		library.AddChild(grid);
		AddHeldItems();
		library.AddChild(BookStyle.Text("Passe o mouse ou toque num equipamento para ver o que ele muda. O Personagem já aparece com ele. O cadeado mostra a Maestria que falta e impede de equipar.", 16));
	}

	internal static Control Header(string title, string note)
	{
		var row = new HBoxContainer();
		var head = BookStyle.Text(title, 22);
		head.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		head.AutowrapMode = TextServer.AutowrapMode.Off;
		var side = BookStyle.Text(note, 16);
		side.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(head);
		row.AddChild(side);
		return row;
	}

	internal static GridContainer Grid()
	{
		var grid = new GridContainer { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		return grid;
	}

	void AddHeldItems()
	{
		var held = host.HeldItems();
		library.AddChild(Header("Itens", held.Count == 0 ? "nenhum ainda" : held.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
		var grid = Grid();
		foreach (var item in held)
		{
			var token = ItemToken.Create(item.Name, () => host.Say($"{item.Name}. {item.Source}."), "Held_" + item.Source, 78, item.Source.Replace("Nível ", "N"), icon: Icons.For("key", 48));
			token.TooltipText = item.Name;
			grid.AddChild(token);
		}
		library.AddChild(grid);
	}

	Button EntryButton(LibraryEntry entry)
	{
		var item = entry.Item;
		var button = ItemToken.Create(item.Name, () => OnPressed(item), "Gear_" + item.Id, 78, Badge(entry), dim: entry.Locked, icon: Icons.For(item.Slot, 48));
		button.MouseEntered += () => ShowPreview(item);
		button.MouseExited += () => ShowPreview(null);
		button.FocusEntered += () => Select(item);
		entryButtons[item.Id] = button;
		return button;
	}

	// O que o cantinho do quadrado diz: equipado, bloqueado (com a Maestria) ou novo. Sempre em texto, nunca só cor.
	string? Badge(LibraryEntry entry)
	{
		if (entry.Equipped) return "✓";
		if (entry.Locked) return $"M{entry.Item.MinMastery}";
		return host.Inventory.IsNew(host.ProfileId, InventoryHost.GearKey(entry.Item.Id)) ? "novo" : null;
	}

	// Enter no teclado confirma; mouse e toque só selecionam, e o botão Equipar confirma.
	void OnPressed(GearItem item)
	{
		Select(item);
		if (Input.IsActionJustPressed("ui_accept")) Equip(item);
	}

	void Select(GearItem item)
	{
		var changed = selected?.Id != item.Id;
		selected = item;
		host.Inventory.MarkSeen(host.ProfileId, InventoryHost.GearKey(item.Id));
		UpdateEntryText(item);
		Updated?.Invoke();
		ShowPreview(item);
		BuildComparison(item);
		if (changed) host.Say(Spoken(item));
	}

	void UpdateEntryText(GearItem item)
	{
		var entry = host.Library.Entries(worn, mastery, host.OwnedGear()).First(e => e.Item.Id == item.Id);
		if (entryButtons.TryGetValue(item.Id, out var button)) ItemToken.SetBadge(button, Badge(entry));
	}

	// Mostra no Personagem e no painel o equipamento apontado; sem ele, o escolhido; sem nenhum, o que está vestido.
	void ShowPreview(GearItem? item)
	{
		var shown = item ?? selected;
		showLoadout(host.Loadout(shown is null ? worn : worn.With(shown.Slot, shown.Id)));
		previewTag.Text = shown is null ? "" : $"Prévia: {shown.Name}";
	}

	void BuildComparison(GearItem? item)
	{
		Clear(comparison);
		if (item is null) return;
		comparison.AddChild(BookStyle.Text(item.Name, 24));
		comparison.AddChild(BookStyle.Text(Subtitle(item), 16));
		comparison.AddChild(BookStyle.Text(item.Description, 16));
		foreach (var line in ComparisonLines(item))
			comparison.AddChild(BookStyle.Text(line, 19));
		comparison.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
		comparison.AddChild(Actions(item));
	}

	// O espaço, o que ele troca e a Maestria pedida.
	string Subtitle(GearItem item)
	{
		var instead = host.Catalog.Find(worn.In(item.Slot)) is { } current && current.Id != item.Id ? $" · troca {current.Name}" : "";
		return $"{host.SlotName(item.Slot)}{instead} · pede Maestria {item.MinMastery} (você tem {mastery})";
	}

	IEnumerable<string> ComparisonLines(GearItem item)
	{
		if (IsEquipped(item)) return ["Você já veste este equipamento."];
		var lines = host.Library.Compare(worn, worn.With(item.Slot, item.Id)).Select(c => $"{c.Arrow} {c.Label}: {c.FromText} → {c.ToText}").ToList();
		if (lines.Count == 0) lines.Add("Nenhum atributo muda.");
		if (mastery < item.MinMastery) lines.Add($"Bloqueado: precisa de Maestria {item.MinMastery}. A sua é {mastery}.");
		if (!host.CanChangeGear) lines.Add("Troque o equipamento no Lobby.");
		return lines;
	}

	Control Actions(GearItem item)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var equipped = IsEquipped(item);
		var equip = BookStyle.Button(equipped ? "Tirar" : "Equipar", () => { if (equipped) Remove(item); else Equip(item); }, "EquipButton", TargetHeight);
		equip.Disabled = !host.CanChangeGear || (!equipped && mastery < item.MinMastery);
		equip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var listen = BookStyle.Button("Ouvir", () => host.Say(Spoken(item)), "ListenButton", TargetHeight);
		listen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(listen);
		row.AddChild(equip);
		return row;
	}

	void Equip(GearItem item)
	{
		if (IsEquipped(item)) return;
		if (!host.CanChangeGear) { host.Say("Troque o equipamento no Lobby."); return; }
		var result = host.Gear.Equip(host.ProfileId, host.RoleId, item.Id, mastery);
		if (!result.IsOk) { host.Say(result.Error!); return; }
		host.Say($"{item.Name} equipado.");
		Changed();
	}

	void Remove(GearItem item)
	{
		if (!host.CanChangeGear) { host.Say("Troque o equipamento no Lobby."); return; }
		var result = host.Gear.Unequip(host.ProfileId, host.RoleId, item.Slot);
		if (!result.IsOk) { host.Say(result.Error!); return; }
		host.Say($"{item.Name} guardado.");
		Changed();
	}

	void Changed()
	{
		host.Changed();
		Refresh();
	}

	bool IsEquipped(GearItem item) => worn.In(item.Slot) == item.Id;

	string Spoken(GearItem item)
	{
		var parts = new List<string> { item.Name };
		if (IsEquipped(item)) parts.Add("Você já veste este equipamento.");
		else if (mastery < item.MinMastery) parts.Add($"Falta Maestria. Precisa de {item.MinMastery}.");
		else parts.AddRange(SpokenChanges(item));
		return string.Join(". ", parts);
	}

	IEnumerable<string> SpokenChanges(GearItem item)
	{
		var changes = host.Library.Compare(worn, worn.With(item.Slot, item.Id));
		return changes.Count == 0 ? ["Nenhum atributo muda"] : changes.Select(c => c.Spoken);
	}

	void FocusSelected()
	{
		if (selected is null || !entryButtons.TryGetValue(selected.Id, out var button)) return;
		Callable.From(() => { if (GodotObject.IsInstanceValid(button) && button.IsInsideTree()) button.GrabFocus(); }).CallDeferred();
	}

	static void Clear(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
