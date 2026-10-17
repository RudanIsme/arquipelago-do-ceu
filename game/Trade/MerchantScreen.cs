using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A tela do Mercador: o estoque de Cosméticos, os equipamentos para vender (um ou vários), a linha Recomprar, o saldo e o botão
// Rotacionar. Toda compra, venda e recompra pede confirmação, e cada preço aparece escrito e é dito em voz.
public partial class MerchantScreen : OverlayScreen
{
	const int TargetHeight = 72;

	readonly MerchantHost host;
	readonly CharacterPortrait portrait = new();
	readonly VBoxContainer list = new() { Name = "MerchantList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
	readonly Label balance = BookStyle.Text("", 22);
	readonly HashSet<string> selected = [];
	readonly Dictionary<string, (Button Button, GearItem Item, bool Worn)> sellRows = [];
	Button? sellButton;
	Button? rotate;
	Label? rotateNote;

	public MerchantScreen(MerchantHost host)
	{
		Name = "MerchantScreen";
		this.host = host;
		list.AddThemeConstantOverride("separation", 6);
		balance.Name = "Balance";
	}

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Stop;
		var shade = new ColorRect { Color = new Color(0.03f, 0.06f, 0.09f, 0.45f), MouseFilter = MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);
		AddChild(BuildWindow());
		Refresh();
		host.Say($"Mercador. Você tem {MerchantHost.Coins(host.Balance)}.");
	}

	Control BuildWindow()
	{
		var page = new VBoxContainer();
		page.AddThemeConstantOverride("separation", 8);
		page.AddChild(BuildHeader());
		page.AddChild(BuildBody());
		var window = BookStyle.Panel(page);
		window.Name = "MerchantWindow";
		window.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		window.OffsetLeft = 16;
		window.OffsetTop = 16;
		window.OffsetRight = -16;
		window.OffsetBottom = -16;
		return window;
	}

	Control BuildHeader()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var title = BookStyle.Text("Mercador", 26);
		title.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(title);
		balance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		balance.HorizontalAlignment = HorizontalAlignment.Center;
		balance.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(balance);
		rotate = BookStyle.Button("", AskRotate, "RotateButton", TargetHeight);
		row.AddChild(rotate);
		row.AddChild(BookStyle.Button("Ouvir o saldo", () => host.Say($"Você tem {MerchantHost.Coins(host.Balance)}."), "SayBalance", TargetHeight));
		row.AddChild(BookStyle.Button("Fechar", Close, "CloseMerchant", TargetHeight));
		return row;
	}

	Control BuildBody()
	{
		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 16);
		portrait.CustomMinimumSize = new Vector2(300, 0);
		body.AddChild(portrait);
		var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		rotateNote = BookStyle.Text("", 16);
		column.AddChild(rotateNote);
		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
		scroll.AddChild(list);
		column.AddChild(scroll);
		body.AddChild(column);
		return body;
	}

	void Refresh()
	{
		balance.Text = $"Saldo: {MerchantHost.Coins(host.Balance)}";
		RefreshRotate();
		foreach (var child in list.GetChildren())
		{
			list.RemoveChild(child);
			child.QueueFree();
		}
		AddStock();
		AddSellable();
		AddBuyback();
		portrait.Show(host.TryOn(null));
	}

	void RefreshRotate()
	{
		var charge = host.Merchant.HasCharge(host.ProfileId);
		rotate!.Text = charge ? "Rotacionar · 1 carga" : "Rotacionar · sem carga";
		rotate.Disabled = !charge;
		rotateNote!.Text = charge
			? "Você tem uma carga: o botão Rotacionar troca o estoque."
			: "O estoque só troca com uma carga. Conclua uma Expedição para ganhar uma.";
	}

	void AddStock()
	{
		list.AddChild(BookStyle.Text("À venda", 22));
		var owned = host.OwnedCosmetics();
		foreach (var offer in host.Merchant.Stock(host.ProfileId))
		{
			var cosmetic = host.Cosmetics.Find(offer.Id);
			if (cosmetic is null) continue;
			var state = owned.Contains(offer.Id) ? "Você já tem" : "Toque para comprar";
			var button = Row($"{cosmetic.Name}\n{MerchantHost.Coins(offer.Price)} · {state}", "Offer_" + offer.Id);
			button.Pressed += () => AskBuy(cosmetic, offer.Price, owned.Contains(offer.Id));
			button.FocusEntered += () => Preview(cosmetic, offer.Price);
			button.MouseEntered += () => portrait.Show(host.TryOn(offer.Id));
			button.MouseExited += () => portrait.Show(host.TryOn(null));
			list.AddChild(button);
		}
	}

	void AddSellable()
	{
		list.AddChild(BookStyle.Text("Vender equipamento", 22));
		sellRows.Clear();
		var worn = host.WornGear;
		var items = host.OwnedGear.Select(host.Gear.Find).OfType<GearItem>().Where(i => host.Catalog.SellPrice(i.Id) > 0).ToList();
		if (items.Count == 0) list.AddChild(BookStyle.Text("Você ainda não tem equipamento para vender.", 16));
		selected.IntersectWith(items.Where(i => !worn.Contains(i.Id)).Select(i => i.Id));
		foreach (var item in items)
		{
			var row = SellRow(item, worn.Contains(item.Id));
			sellRows[item.Id] = (row, item, worn.Contains(item.Id));
			list.AddChild(row);
		}
		sellButton = BookStyle.Button("", AskSell, "SellSelected", TargetHeight);
		list.AddChild(sellButton);
		UpdateSelection();
	}

	// Marcar um equipamento só atualiza os textos: a lista não é refeita, para a rolagem e o foco ficarem onde estão.
	void UpdateSelection()
	{
		foreach (var (button, item, isWorn) in sellRows.Values)
			button.Text = SellText(item, isWorn);
		var total = selected.Sum(host.Catalog.SellPrice);
		sellButton!.Text = $"Vender selecionados ({selected.Count}) · {MerchantHost.Coins(total)}";
		sellButton.Disabled = selected.Count == 0;
	}

	string SellText(GearItem item, bool isWorn)
	{
		var mark = selected.Contains(item.Id) ? "✓ " : "";
		var detail = isWorn ? "Vestido: não se vende" : $"Vende por {MerchantHost.Coins(host.Catalog.SellPrice(item.Id))}";
		return $"{mark}{item.Name} · {host.Gear.Slots.FirstOrDefault(sl => sl.Id == item.Slot)?.Name}\n{detail}";
	}

	Button SellRow(GearItem item, bool isWorn)
	{
		var button = Row(SellText(item, isWorn), "Sell_" + item.Id);
		button.Disabled = isWorn;
		button.Pressed += () => ToggleSelected(item);
		button.FocusEntered += () => host.Say(SellText(item, isWorn).Replace('\n', '.'));
		return button;
	}

	void AddBuyback()
	{
		list.AddChild(BookStyle.Text("Recomprar", 22));
		var entries = host.Merchant.Buybacks(host.ProfileId);
		if (entries.Count == 0) list.AddChild(BookStyle.Text("O que você vender fica aqui, pelo mesmo preço, sem prazo.", 16));
		foreach (var entry in entries)
		{
			var item = host.Gear.Find(entry.Item);
			if (item is null) continue;
			var button = Row($"{item.Name}\nRecomprar por {MerchantHost.Coins(entry.Price)}", "Rebuy_" + entry.Item);
			button.Pressed += () => AskRebuy(item, entry.Price);
			button.FocusEntered += () => host.Say($"{item.Name}. Recomprar por {MerchantHost.Coins(entry.Price)}.");
			list.AddChild(button);
		}
	}

	static Button Row(string text, string name)
	{
		var button = BookStyle.Button(text, () => { }, name, TargetHeight);
		button.Alignment = HorizontalAlignment.Left;
		return button;
	}

	void Preview(Cosmetic cosmetic, int price)
	{
		portrait.Show(host.TryOn(cosmetic.Id));
		host.Say($"{cosmetic.Name}. {MerchantHost.Coins(price)}.");
	}

	void ToggleSelected(GearItem item)
	{
		if (!selected.Add(item.Id)) selected.Remove(item.Id);
		UpdateSelection();
	}

	void AskBuy(Cosmetic cosmetic, int price, bool owned)
	{
		if (owned) { host.Say("Você já tem esse Cosmético."); return; }
		if (host.Balance < price) { host.Say($"Faltam {MerchantHost.Coins(price - host.Balance)}."); return; }
		Ask($"Comprar {cosmetic.Name} por {MerchantHost.Coins(price)}?", () => Done(host.Merchant.Buy(host.ProfileId, cosmetic.Id), $"{cosmetic.Name} comprado."));
	}

	void AskSell()
	{
		var ids = selected.ToList();
		var total = ids.Sum(host.Catalog.SellPrice);
		var names = string.Join(", ", ids.Select(id => host.Gear.Find(id)?.Name));
		Ask($"Vender {names} por {MerchantHost.Coins(total)}?", () =>
		{
			var sold = host.Merchant.Sell(host.ProfileId, ids);
			selected.Clear();
			Done(sold, $"Vendido por {MerchantHost.Coins(total)}.");
		});
	}

	void AskRebuy(GearItem item, int price)
	{
		if (host.Balance < price) { host.Say($"Faltam {MerchantHost.Coins(price - host.Balance)}."); return; }
		Ask($"Recomprar {item.Name} por {MerchantHost.Coins(price)}?", () => Done(host.Merchant.Rebuy(host.ProfileId, item.Id), $"{item.Name} recomprado."));
	}

	void AskRotate() =>
		Ask("Trocar o estoque agora? Isso gasta a carga.", () => Done(host.Merchant.Rotate(host.ProfileId), "O estoque mudou."));

	void Ask(string question, System.Action yes) => AddChild(new ConfirmDialog(question, host.Say, yes));

	void Done<T>(Result<T> result, string success)
	{
		if (!result.IsOk)
		{
			host.Say(result.Error!);
			Refresh();
			return;
		}
		host.Changed();
		Refresh();
		host.Say($"{success} Você tem {MerchantHost.Coins(host.Balance)}.");
	}
}
