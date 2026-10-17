using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// A aba Inventário: as caixinhas do que o Personagem veste, a biblioteca de equipamentos e a comparação.
// Apontar, tocar ou focar mostra uma prévia; só o botão Equipar (ou Enter no equipamento focado) vale de verdade.
public sealed partial class GearTab : Node
{
	const int TargetHeight = 72;

	readonly InventoryHost host;
	readonly Action<EquipmentLoadout> showLoadout;
	readonly VBoxContainer slots = new() { Name = "Slots" };
	readonly VBoxContainer library = new() { Name = "Library" };
	readonly VBoxContainer comparison = new() { Name = "Comparison" };
	readonly Dictionary<string, Button> entryButtons = [];
	Gear worn = new();
	int mastery;
	GearItem? selected;

	public GearTab(InventoryHost host, Action<EquipmentLoadout> showLoadout)
	{
		this.host = host;
		this.showLoadout = showLoadout;
		slots.AddThemeConstantOverride("separation", 6);
		library.AddThemeConstantOverride("separation", 6);
		comparison.AddThemeConstantOverride("separation", 4);
	}

	public event Action? Updated;

	public Control Slots => slots;

	public Control Library => library;

	public Control Comparison => comparison;

	public void Refresh()
	{
		worn = host.Worn;
		mastery = host.Mastery();
		BuildSlots();
		BuildLibrary();
		ShowPreview(null);
		FocusSelected();
		Updated?.Invoke();
	}

	void BuildSlots()
	{
		Clear(slots);
		foreach (var slot in host.Catalog.Slots)
			slots.AddChild(SlotButton(slot));
	}

	Button SlotButton(GearSlot slot)
	{
		var item = host.Catalog.Find(worn.In(slot.Id));
		var button = BookStyle.Button($"{slot.Name}: {item?.Name ?? "vazio"}", () => { if (item is not null) Select(item); }, "Slot_" + slot.Id, TargetHeight);
		button.Alignment = HorizontalAlignment.Left;
		return button;
	}

	void BuildLibrary()
	{
		Clear(library);
		entryButtons.Clear();
		library.AddChild(BookStyle.Text("Equipamentos", 22));
		foreach (var entry in host.Library.Entries(worn, mastery))
			library.AddChild(EntryButton(entry));
		library.AddChild(BookStyle.Text("Itens", 22));
		AddHeldItems();
	}

	void AddHeldItems()
	{
		var held = host.HeldItems();
		if (held.Count == 0) library.AddChild(BookStyle.Text("Nenhum Item de troca ainda.", 16));
		foreach (var item in held)
			library.AddChild(BookStyle.Panel(BookStyle.Text($"{item.Name}\n{item.Source}", 16)));
	}

	Button EntryButton(LibraryEntry entry)
	{
		var item = entry.Item;
		var button = BookStyle.Button(EntryText(entry), () => OnPressed(item), "Gear_" + item.Id, TargetHeight);
		button.Alignment = HorizontalAlignment.Left;
		button.MouseEntered += () => ShowPreview(item);
		button.MouseExited += () => ShowPreview(null);
		button.FocusEntered += () => Select(item);
		entryButtons[item.Id] = button;
		return button;
	}

	string EntryText(LibraryEntry entry)
	{
		var tags = new List<string>();
		if (entry.Equipped) tags.Add("Equipado");
		if (entry.Locked) tags.Add($"Bloqueado: Maestria {entry.Item.MinMastery}");
		if (host.Inventory.IsNew(host.ProfileId, InventoryHost.GearKey(entry.Item.Id))) tags.Add("NOVO");
		var line = string.Join(" · ", tags.Prepend(host.SlotName(entry.Item.Slot)));
		return $"{entry.Item.Name}\n{line}";
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
		if (changed) host.Say(Spoken(item));
	}

	void UpdateEntryText(GearItem item)
	{
		var entry = host.Library.Entries(worn, mastery).First(e => e.Item.Id == item.Id);
		if (entryButtons.TryGetValue(item.Id, out var button)) button.Text = EntryText(entry);
	}

	// Mostra no Personagem e no painel o equipamento apontado; sem ele, o escolhido; sem nenhum, o que está vestido.
	void ShowPreview(GearItem? item)
	{
		var shown = item ?? selected;
		showLoadout(host.Loadout(shown is null ? worn : worn.With(shown.Slot, shown.Id)));
		BuildComparison(shown);
	}

	void BuildComparison(GearItem? item)
	{
		Clear(comparison);
		if (item is null)
		{
			comparison.AddChild(BookStyle.Text("Escolha um equipamento para ver o que muda.", 16));
			return;
		}
		comparison.AddChild(BookStyle.Text(item.Name, 22));
		comparison.AddChild(BookStyle.Text(item.Description, 16));
		foreach (var line in ComparisonLines(item))
			comparison.AddChild(BookStyle.Text(line, 18));
		comparison.AddChild(Actions(item));
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
