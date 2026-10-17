using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Trade;
using Godot;

namespace ArquipelagoDoCeu;

// O Mercador do Lobby: a tela, o catálogo em dados e a carga de rotação que cada Expedição concluída dá.
public partial class Main
{
	MerchantService merchant = null!;
	MerchantCatalog? merchantCatalog;

	// Sem o catálogo o Mercador fica sem estoque, e a criança segue jogando: ele é um extra.
	MerchantCatalog LoadMerchant() => merchantCatalog ??= catalogs.Optional("mercador.json", json => MerchantCatalog.Parse(json, LoadCosmetics(), LoadGear()), MerchantCatalog.Empty);

	void ShowMerchant()
	{
		if (current is null) return;
		var screenToOpen = new MerchantScreen(new MerchantHost
		{
			ProfileId = current.Id,
			Merchant = merchant,
			Catalog = LoadMerchant(),
			Cosmetics = LoadCosmetics(),
			Gear = LoadGear(),
			Inventory = inventory,
			OwnedCosmetics = () => rewardService.Cosmetics(current.Id),
			Worn = () => gear.Get(current.Id, role),
			Say = text => voice.Say(text, interrupt: true),
			Changed = RefreshDock,
		});
		screenToOpen.Closed += RefreshDock;
		ShowOverlay(screenToOpen);
	}

	// Concluir uma Expedição (a Progressão pelo Desafio épico, a Livre com ao menos um Andar) dá uma carga de rotação.
	void ExpeditionCompleted()
	{
		if (current is null) return;
		var charged = merchant.AddCharge(current.Id);
		if (!charged.IsOk) GD.PushWarning(charged.Error!);
	}
}
