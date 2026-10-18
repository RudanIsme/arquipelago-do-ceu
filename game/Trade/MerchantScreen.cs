using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A tela do Mercador, como uma loja de RPG: abas, grade de molduras com ícone e preço e, ao escolher, o painel do item.
// Escolher vários é comprar, vender ou recomprar em lote; toda operação pede confirmação e o preço é dito em voz.
public partial class MerchantScreen : OverlayScreen
{
	const int TargetHeight = 56;
	const int TokenSize = 92;

	enum Tab { Buy, Sell, Rebuy }

	// Uma moldura da grade: o item, o que mostra e se dá para escolher.
	sealed record Entry(string Id, string Name, string Icon, int Price, string Description, string Detail, EquipmentLoadout Look, string? Blocked);

	readonly MerchantHost host;
	readonly ItemPreview preview;
	readonly VBoxContainer grid = new() { Name = "MerchantList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
	readonly PanelContainer detail = new() { Name = "MerchantDetail", CustomMinimumSize = new Vector2(340, 0), Visible = false };
	readonly VBoxContainer detailBody = new();
	readonly Label balance = BookStyle.Text("", 22);
	readonly Label note = BookStyle.Text("", 16);
	readonly Label previewName = BookStyle.Text("", 24);
	readonly Label previewDetail = BookStyle.Text("", 16);
	readonly Label summary = BookStyle.Text("", 19);
	readonly HashSet<string> selected = [];
	readonly Dictionary<Tab, Button> tabButtons = [];
	Button? action;
	Button? rotate;
	Tab tab = Tab.Buy;
	Entry? shown;
	List<Entry> entries = [];

	public MerchantScreen(MerchantHost host)
	{
		Name = "MerchantScreen";
		this.host = host;
		preview = new ItemPreview(() => host.ReduceMotion());
		balance.Name = "Balance";
		previewName.Name = "PreviewName";
		previewDetail.Name = "PreviewDetail";
		summary.Name = "Summary";
		grid.AddThemeConstantOverride("separation", 8);
		detail.AddThemeStyleboxOverride("panel", BookStyle.Box(BookStyle.Selected));
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
		page.AddThemeConstantOverride("separation", 10);
		page.AddChild(BuildHeader());
		page.AddChild(BuildTabs());
		page.AddChild(BuildBody());
		var window = BookStyle.Panel(page);
		window.Name = "MerchantWindow";
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
		var title = BookStyle.Text("Mercador", 28);
		title.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(title);
		balance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		balance.HorizontalAlignment = HorizontalAlignment.Center;
		balance.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(new TextureRect { Texture = Icons.For("coin", 32), CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, SizeFlagsVertical = SizeFlags.ShrinkCenter });
		row.AddChild(balance);
		rotate = BookStyle.Button("", AskRotate, "RotateButton", TargetHeight);
		row.AddChild(rotate);
		row.AddChild(BookStyle.Button("Ouvir o saldo", () => host.Say($"Você tem {MerchantHost.Coins(host.Balance)}."), "SayBalance", TargetHeight));
		row.AddChild(BookStyle.Button("Fechar", Close, "CloseMerchant", TargetHeight));
		return row;
	}

	Control BuildTabs()
	{
		var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
		row.AddThemeConstantOverride("separation", 8);
		foreach (var (kind, name) in new[] { (Tab.Buy, "TabBuy"), (Tab.Sell, "TabSell"), (Tab.Rebuy, "TabRebuy") })
		{
			var button = BookStyle.Button("", () => ShowTab(kind), name, TargetHeight);
			button.CustomMinimumSize = new Vector2(200, TargetHeight);
			tabButtons[kind] = button;
			row.AddChild(button);
		}
		note.AutowrapMode = TextServer.AutowrapMode.Off;
		note.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(note);
		return row;
	}

	Control BuildBody()
	{
		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 16);
		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
		scroll.AddChild(grid);
		body.AddChild(scroll);
		detailBody.AddThemeConstantOverride("separation", 8);
		detailBody.AddChild(preview);
		detailBody.AddChild(previewName);
		detailBody.AddChild(previewDetail);
		detailBody.AddChild(summary);
		detailBody.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
		action = BookStyle.Button("", Confirm, "ActionButton", TargetHeight + 8);
		detailBody.AddChild(action);
		detail.AddChild(detailBody);
		body.AddChild(detail);
		return body;
	}

	void ShowTab(Tab next)
	{
		tab = next;
		selected.Clear();
		shown = null;
		Refresh();
	}

	void Refresh()
	{
		balance.Text = $"Saldo: {MerchantHost.Coins(host.Balance)}";
		RefreshRotate();
		entries = tab switch { Tab.Buy => BuyEntries(), Tab.Sell => SellEntries(), _ => RebuyEntries() };
		selected.IntersectWith(entries.Where(e => e.Blocked is null).Select(e => e.Id));
		foreach (var child in grid.GetChildren())
		{
			grid.RemoveChild(child);
			child.QueueFree();
		}
		RefreshTabText();
		BuildGrid();
		RefreshDetail();
	}

	void RefreshTabText()
	{
		tabButtons[Tab.Buy].Text = $"À venda · {BuyEntries().Count}";
		tabButtons[Tab.Sell].Text = $"Vender · {SellEntries().Count}";
		tabButtons[Tab.Rebuy].Text = $"Recomprar · {RebuyEntries().Count}";
		foreach (var (kind, button) in tabButtons)
			button.AddThemeStyleboxOverride("normal", kind == tab ? BookStyle.Emphasized(BookStyle.Selected) : BookStyle.Box());
	}

	void RefreshRotate()
	{
		var charge = host.Merchant.HasCharge(host.ProfileId);
		rotate!.Text = charge ? "Rotacionar · 1 carga" : "Rotacionar · sem carga";
		rotate.Disabled = !charge;
		note.Text = tab == Tab.Buy ? (charge ? "Você tem uma carga: Rotacionar troca o estoque." : "O estoque só troca com uma carga. Conclua uma Expedição.") : "";
	}

	// --- O que cada aba lista

	List<Entry> BuyEntries()
	{
		var owned = host.OwnedCosmetics();
		var list = new List<Entry>();
		foreach (var offer in host.Merchant.Stock(host.ProfileId))
		{
			if (host.Cosmetics.Find(offer.Id) is not { } cosmetic) continue;
			var look = new EquipmentLoadout(Cosmetics: cosmetic.Look is { } l ? [l] : []);
			list.Add(new Entry(offer.Id, cosmetic.Name, cosmetic.Look?.Kind ?? "star", offer.Price, cosmetic.Description, "Cosmético · só muda a aparência", look, owned.Contains(offer.Id) ? "Você já tem" : null));
		}
		return list;
	}

	List<Entry> SellEntries()
	{
		var worn = host.WornGear;
		return [.. host.OwnedGear.Select(host.Gear.Find).OfType<GearItem>().Where(i => host.Catalog.SellPrice(i.Id) > 0)
			.Select(i => GearEntry(i, host.Catalog.SellPrice(i.Id), worn.Contains(i.Id) ? "Vestido: não se vende" : null))];
	}

	List<Entry> RebuyEntries() =>
		[.. host.Merchant.Buybacks(host.ProfileId).Select(b => (Item: host.Gear.Find(b.Item), b.Price)).Where(p => p.Item is not null).Select(p => GearEntry(p.Item!, p.Price, null))];

	Entry GearEntry(GearItem item, int price, string? blocked) =>
		new(item.Id, item.Name, item.Slot, price, item.Description, $"{host.Gear.Slots.FirstOrDefault(s => s.Id == item.Slot)?.Name} · Maestria mínima {item.MinMastery}", EquipmentLooks.For(new Gear().With(item.Slot, item.Id)), blocked);

	// --- A grade

	void BuildGrid()
	{
		if (entries.Count == 0)
		{
			grid.AddChild(BookStyle.Text(tab switch { Tab.Buy => "Nada à venda agora.", Tab.Sell => "Você ainda não tem equipamento para vender.", _ => "O que você vender fica aqui, pelo mesmo preço, sem prazo." }, 18));
			return;
		}
		var cells = new GridContainer { Columns = 6 };
		cells.AddThemeConstantOverride("h_separation", 10);
		cells.AddThemeConstantOverride("v_separation", 10);
		foreach (var entry in entries)
			cells.AddChild(Token(entry));
		grid.AddChild(cells);
	}

	Button Token(Entry entry)
	{
		var prefix = tab switch { Tab.Buy => "Offer_", Tab.Sell => "Sell_", _ => "Rebuy_" };
		var chosen = selected.Contains(entry.Id);
		var badge = entry.Blocked is not null ? (tab == Tab.Buy ? "tem" : "usado") : $"{(chosen ? "✓ " : "")}{entry.Price}";
		var button = ItemToken.Create(entry.Name, () => Toggle(entry), prefix + entry.Id, TokenSize, badge, dim: entry.Blocked is not null, icon: Icons.For(entry.Icon, 52));
		button.TooltipText = $"{entry.Name} · {MerchantHost.Coins(entry.Price)}" + (entry.Blocked is null ? "" : $" · {entry.Blocked}");
		if (chosen) button.AddThemeStyleboxOverride("normal", BookStyle.Emphasized(BookStyle.Selected));
		button.FocusEntered += () => host.Say($"{entry.Name}. {MerchantHost.Coins(entry.Price)}. {entry.Blocked}");
		return button;
	}

	void Toggle(Entry entry)
	{
		if (entry.Blocked is not null) { host.Say(entry.Blocked + "."); return; }
		if (!selected.Add(entry.Id)) selected.Remove(entry.Id);
		shown = selected.Contains(entry.Id) ? entry : entries.FirstOrDefault(e => selected.Contains(e.Id));
		Refresh();
	}

	// --- O painel do item: só existe com algo escolhido

	void RefreshDetail()
	{
		var chosen = entries.Where(e => selected.Contains(e.Id)).ToList();
		detail.Visible = chosen.Count > 0;
		if (chosen.Count == 0) return;

		var entry = shown is not null && selected.Contains(shown.Id) ? shown : chosen[0];
		preview.Show(entry.Look);
		previewName.Text = chosen.Count == 1 ? entry.Name : $"{entry.Name} e mais {chosen.Count - 1}";
		previewDetail.Text = $"{entry.Description}\n{entry.Detail}";
		var total = chosen.Sum(e => e.Price);
		var missing = total - host.Balance;
		var verb = tab switch { Tab.Buy => "Comprar", Tab.Sell => "Vender", _ => "Recomprar" };
		summary.Text = tab == Tab.Sell
			? $"{chosen.Count} {(chosen.Count == 1 ? "item" : "itens")} · você recebe {MerchantHost.Coins(total)}"
			: $"{chosen.Count} {(chosen.Count == 1 ? "item" : "itens")} · total {MerchantHost.Coins(total)}\nVocê tem {MerchantHost.Coins(host.Balance)}" + (missing > 0 ? $" · faltam {MerchantHost.Coins(missing)}" : "");
		action!.Text = chosen.Count == 1 ? $"{verb} · {MerchantHost.Coins(total)}" : $"{verb} ({chosen.Count}) · {MerchantHost.Coins(total)}";
		action.Disabled = tab != Tab.Sell && missing > 0;
	}

	// --- Confirmar

	void Confirm()
	{
		var chosen = entries.Where(e => selected.Contains(e.Id)).ToList();
		if (chosen.Count == 0) return;
		var total = chosen.Sum(e => e.Price);
		var names = string.Join(", ", chosen.Select(e => e.Name));
		switch (tab)
		{
			case Tab.Buy: AskPay($"Comprar {names} por {MerchantHost.Coins(total)}?", total, sale: false, () => Each(chosen, e => host.Merchant.Buy(host.ProfileId, e.Id), $"{names} comprado.")); break;
			case Tab.Rebuy: AskPay($"Recomprar {names} por {MerchantHost.Coins(total)}?", total, sale: false, () => Each(chosen, e => host.Merchant.Rebuy(host.ProfileId, e.Id), $"{names} recomprado.")); break;
			default: AskPay($"Vender {names} por {MerchantHost.Coins(total)}?", total, sale: true, () => Done(host.Merchant.Sell(host.ProfileId, [.. chosen.Select(e => e.Id)]), $"Vendido por {MerchantHost.Coins(total)}.")); break;
		}
	}

	// Em lote, uma operação por item; se uma falha, o jogo diz por quê e o resto da seleção continua escolhido.
	void Each<T>(List<Entry> chosen, Func<Entry, Result<T>> operation, string success)
	{
		foreach (var entry in chosen)
		{
			var result = operation(entry);
			if (result.IsOk) { selected.Remove(entry.Id); continue; }
			host.Changed();
			host.Say(result.Error!);
			Refresh();
			return;
		}
		Done(Result<bool>.Ok(true), success);
	}

	void AskRotate() =>
		Ask("Trocar o estoque agora? Isso gasta a carga.", () => Done(host.Merchant.Rotate(host.ProfileId), "O estoque mudou."));

	// Toda compra, venda e recompra pergunta como pagar: "Eu pago" (monta o valor ou o troco do Real) ou "Pagar sozinho".
	void AskPay(string question, int coins, bool sale, Action yes)
	{
		var cents = coins * host.Catalog.CentsPerCoin;
		var challenge = sale ? new ChangeChallenge(MoneyTask.Pay, cents, cents) : ChangeChallenge.ForPurchase(cents, host.Money);
		var prompt = sale ? $"O Mercador paga {MoneyCatalog.Format(cents)}. Monte esse valor para conferir."
			: challenge.Task == MoneyTask.Pay ? $"Pague {MoneyCatalog.Format(cents)}. Monte esse valor com moedas e notas."
			: $"A compra deu {MoneyCatalog.Format(cents)}. Você dá uma nota de {MoneyCatalog.Format(challenge.Given)}. Monte o troco!";
		AddChild(new PaymentDialog(question, prompt, challenge, host.Money, host.Say, yes));
	}

	void Ask(string question, Action yes) => AddChild(new ConfirmDialog(question, host.Say, yes));

	void Done<T>(Result<T> result, string success)
	{
		if (!result.IsOk)
		{
			host.Say(result.Error!);
			Refresh();
			return;
		}
		selected.Clear();
		host.Changed();
		Refresh();
		host.Say($"{success} Você tem {MerchantHost.Coins(host.Balance)}.");
	}
}
