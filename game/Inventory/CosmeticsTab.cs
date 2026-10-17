using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A aba Cosméticos: a mesma estrutura do Inventário, sem atributos. Vestir ou tirar muda o Personagem na hora.
public sealed partial class CosmeticsTab : Node
{
	const int TargetHeight = 72;

	readonly InventoryHost host;
	readonly Action<EquipmentLoadout> showLoadout;
	readonly VBoxContainer library = new() { Name = "CosmeticsLibrary" };
	string? focusId;

	public CosmeticsTab(InventoryHost host, Action<EquipmentLoadout> showLoadout)
	{
		this.host = host;
		this.showLoadout = showLoadout;
		library.AddThemeConstantOverride("separation", 6);
	}

	public event Action? Updated;

	public Control Library => library;

	public void Refresh()
	{
		foreach (var child in library.GetChildren())
		{
			library.RemoveChild(child);
			child.QueueFree();
		}
		library.AddChild(BookStyle.Text("Cosméticos só mudam a aparência. Eles não dão vantagem nenhuma.", 16));
		var owned = host.OwnedCosmetics().Select(host.Cosmetics.Find).OfType<Cosmetic>().ToList();
		if (owned.Count == 0) library.AddChild(BookStyle.Text("Você ainda não ganhou Cosméticos. Complete Andares nas torres para ganhar.", 18));
		foreach (var cosmetic in owned)
			library.AddChild(CosmeticButton(cosmetic));
		showLoadout(host.Loadout(host.Worn));
		FocusLast();
		Updated?.Invoke();
	}

	Button CosmeticButton(Cosmetic cosmetic)
	{
		var isWorn = host.Inventory.Worn(host.ProfileId).Contains(cosmetic.Id);
		var tags = new List<string> { isWorn ? "Vestido ✓" : "Toque para vestir" };
		if (host.Inventory.IsNew(host.ProfileId, InventoryHost.CosmeticKey(cosmetic.Id))) tags.Add("NOVO");
		var button = BookStyle.Button($"{cosmetic.Name}\n{string.Join(" · ", tags)}", () => Toggle(cosmetic, isWorn), "Cosmetic_" + cosmetic.Id, TargetHeight);
		button.Alignment = HorizontalAlignment.Left;
		button.FocusEntered += () => { host.Inventory.MarkSeen(host.ProfileId, InventoryHost.CosmeticKey(cosmetic.Id)); Updated?.Invoke(); };
		return button;
	}

	void Toggle(Cosmetic cosmetic, bool isWorn)
	{
		var result = isWorn
			? host.Inventory.Remove(host.ProfileId, cosmetic.Id)
			: host.Inventory.Wear(host.ProfileId, cosmetic.Id, host.OwnedCosmetics());
		if (!result.IsOk) { host.Say(result.Error!); return; }
		host.Inventory.MarkSeen(host.ProfileId, InventoryHost.CosmeticKey(cosmetic.Id));
		host.Say(isWorn ? $"{cosmetic.Name} guardado." : $"{cosmetic.Name} vestido.");
		focusId = cosmetic.Id;
		host.Changed();
		Refresh();
	}

	void FocusLast()
	{
		if (focusId is null || library.FindChild("Cosmetic_" + focusId, true, false) is not Button button) return;
		Callable.From(() => { if (GodotObject.IsInstanceValid(button) && button.IsInsideTree()) button.GrabFocus(); }).CallDeferred();
	}
}
