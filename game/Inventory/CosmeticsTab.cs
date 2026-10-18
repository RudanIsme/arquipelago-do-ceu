using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A aba Cosméticos: a mesma estrutura do Inventário (grade, prévia no Personagem e painel ao lado), sem atributos.
// Tocar num Cosmético só mostra a prévia; o botão Vestir (ou Tirar) muda o Personagem de verdade, na hora.
public sealed partial class CosmeticsTab : Node
{
	const int TargetHeight = 72;

	readonly InventoryHost host;
	readonly Action<EquipmentLoadout> showLoadout;
	readonly Action<string> setTag;
	readonly VBoxContainer library = new() { Name = "CosmeticsLibrary" };
	readonly VBoxContainer comparison = new() { Name = "CosmeticsComparison" };
	readonly Dictionary<string, Button> buttons = [];
	string? selectedId;

	public CosmeticsTab(InventoryHost host, Action<EquipmentLoadout> showLoadout, Action<string> setTag)
	{
		this.host = host;
		this.showLoadout = showLoadout;
		this.setTag = setTag;
		library.AddThemeConstantOverride("separation", 10);
		comparison.AddThemeConstantOverride("separation", 8);
	}

	public event Action? Updated;

	public Control Library => library;

	public Control Comparison => comparison;

	public void Refresh()
	{
		foreach (var child in library.GetChildren())
		{
			library.RemoveChild(child);
			child.QueueFree();
		}
		var owned = host.OwnedCosmetics().Select(host.Cosmetics.Find).OfType<Cosmetic>().ToList();
		library.AddChild(GearTab.Header("Meus cosméticos", owned.Count == 1 ? "1 item" : $"{owned.Count} itens"));
		if (owned.Count == 0) library.AddChild(BookStyle.Text("Você ainda não ganhou Cosméticos. Complete Andares nas torres para ganhar.", 18));
		var grid = GearTab.Grid();
		buttons.Clear();
		foreach (var cosmetic in owned)
		{
			var token = CosmeticButton(cosmetic);
			buttons[cosmetic.Id] = token;
			grid.AddChild(token);
		}
		library.AddChild(grid);
		library.AddChild(BookStyle.Text("Troca na hora, sem sair daqui. Só muda a aparência.", 16));
		Select(owned.FirstOrDefault(c => c.Id == selectedId));
	}

	Button CosmeticButton(Cosmetic cosmetic)
	{
		var worn = host.Inventory.Worn(host.ProfileId).Contains(cosmetic.Id);
		var isNew = host.Inventory.IsNew(host.ProfileId, InventoryHost.CosmeticKey(cosmetic.Id));
		var button = ItemToken.Create(cosmetic.Name, () => Select(cosmetic), "Cosmetic_" + cosmetic.Id, 78, worn ? "✓" : isNew ? "novo" : null);
		button.FocusEntered += () => Select(cosmetic);
		return button;
	}

	// Mostra a prévia do Cosmético escolhido (por cima do que já está vestido) e o painel dele. Sem escolha, o Personagem como está.
	void Select(Cosmetic? cosmetic)
	{
		selectedId = cosmetic?.Id;
		foreach (var child in comparison.GetChildren())
		{
			comparison.RemoveChild(child);
			child.QueueFree();
		}
		if (cosmetic is null)
		{
			showLoadout(host.Loadout(host.Worn));
			setTag("");
			comparison.AddChild(BookStyle.Text("Escolha um Cosmético para ver como fica.", 18));
			return;
		}
		var worn = host.Inventory.Worn(host.ProfileId).Contains(cosmetic.Id);
		host.Inventory.MarkSeen(host.ProfileId, InventoryHost.CosmeticKey(cosmetic.Id));
		if (buttons.TryGetValue(cosmetic.Id, out var token)) ItemToken.SetBadge(token, worn ? "✓" : null);
		showLoadout(Preview(cosmetic, worn));
		setTag($"Prévia: {cosmetic.Name}");
		comparison.AddChild(BookStyle.Text(cosmetic.Name, 24));
		comparison.AddChild(BookStyle.Text("Cosmético", 16));
		comparison.AddChild(BookStyle.Text("Só muda a aparência.", 19));
		comparison.AddChild(BookStyle.Text("Energia, velocidade e alcance ficam como estão.", 19));
		comparison.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
		comparison.AddChild(Actions(cosmetic, worn));
		Updated?.Invoke();
	}

	EquipmentLoadout Preview(Cosmetic cosmetic, bool worn)
	{
		var looks = host.Inventory.WornLooks(host.ProfileId).ToList();
		if (!worn && cosmetic.Look is { } look) looks.Add(look);
		return EquipmentLooks.For(host.Worn, looks);
	}

	Control Actions(Cosmetic cosmetic, bool worn)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var listen = BookStyle.Button("Ouvir", () => host.Say($"{cosmetic.Name}. Só muda a aparência."), "ListenButton", TargetHeight);
		var wear = BookStyle.Button(worn ? "Tirar" : "Vestir", () => Toggle(cosmetic, worn), "WearButton", TargetHeight);
		listen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		wear.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(listen);
		row.AddChild(wear);
		return row;
	}

	void Toggle(Cosmetic cosmetic, bool worn)
	{
		var result = worn
			? host.Inventory.Remove(host.ProfileId, cosmetic.Id)
			: host.Inventory.Wear(host.ProfileId, cosmetic.Id, host.OwnedCosmetics());
		if (!result.IsOk) { host.Say(result.Error!); return; }
		host.Say(worn ? $"{cosmetic.Name} guardado." : $"{cosmetic.Name} vestido.");
		host.Changed();
		Refresh();
		// A lista é refeita; o foco volta ao botão que a criança acabou de usar, para o teclado e o controle seguirem de onde estavam.
		Callable.From(() => { if (comparison.FindChild("WearButton", true, false) is { } wear && wear.IsInsideTree()) ((Control)wear).GrabFocus(); }).CallDeferred();
	}
}
