using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Trade;

namespace ArquipelagoDoCeu;

// Tudo o que o Mercador precisa do jogo: os serviços, os catálogos, a prévia do Personagem e o que dizer em voz.
public sealed class MerchantHost
{
	public required string ProfileId { get; init; }

	public required MerchantService Merchant { get; init; }

	public required MerchantCatalog Catalog { get; init; }

	public required CosmeticCatalog Cosmetics { get; init; }

	public required GearCatalog Gear { get; init; }

	public required InventoryService Inventory { get; init; }

	public required Func<IReadOnlyList<string>> OwnedCosmetics { get; init; }

	// O conjunto do Papel de agora, para a prévia mostrar o Personagem como ele está.
	public required Func<Gear> Worn { get; init; }

	public required Action<string> Say { get; init; }

	// Chamado quando o que a criança tem mudou (comprou, vendeu, recomprou), para o resto do jogo atualizar.
	public required Action Changed { get; init; }

	public IReadOnlyList<string> OwnedGear => Inventory.OwnedGear(ProfileId);

	// Os equipamentos vestidos em algum Papel: o Mercador nunca os compra.
	public IReadOnlyList<string> WornGear => Inventory.WornGear(ProfileId);

	public int Balance => Merchant.Balance(ProfileId);

	public static string Coins(int amount) => amount == 1 ? "1 moeda" : $"{amount} moedas";

	// O Personagem como está, com o Cosmético à venda por cima, só na prévia.
	public EquipmentLoadout TryOn(string? cosmeticId)
	{
		var looks = Inventory.WornLooks(ProfileId).ToList();
		if (cosmeticId is not null && Cosmetics.Find(cosmeticId)?.Look is { } look) looks.Add(look);
		return EquipmentLooks.For(Worn(), looks);
	}
}
