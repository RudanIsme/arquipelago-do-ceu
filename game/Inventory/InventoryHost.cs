using System.Linq;
using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu;

// Um Item de troca que a criança guarda, para a seção Itens do Inventário (só leitura).
public sealed record HeldItem(string Name, string Source);

// Tudo o que o Inventário precisa do jogo: os dados, as ações e o que dizer em voz. A tela não conhece o resto do Main.
public sealed class InventoryHost
{
	public required string ProfileId { get; init; }

	public required string RoleId { get; init; }

	public required string RoleName { get; init; }

	public required GearCatalog Catalog { get; init; }

	public required CosmeticCatalog Cosmetics { get; init; }

	public required GearService Gear { get; init; }

	public required InventoryService Inventory { get; init; }

	public required Func<int> Mastery { get; init; }

	public required Func<IReadOnlyList<string>> OwnedCosmetics { get; init; }

	public required Func<IReadOnlyList<HeldItem>> HeldItems { get; init; }

	public required Action<string> Say { get; init; }

	// Chamado quando o que o Personagem veste mudou de verdade (equipar, tirar, vestir um Cosmético).
	public required Action Changed { get; init; }

	// Na Dungeon só os Cosméticos mudam: equipamento mexe na Energia máxima, e isso se troca no Lobby.
	public required bool CanChangeGear { get; init; }

	public GearLibrary Library => new(Catalog);

	public Gear Worn => Gear.Get(ProfileId, RoleId);

	public EquipmentLoadout Loadout(Gear gear) => EquipmentLooks.For(gear, Inventory.WornLooks(ProfileId));

	public string SlotName(string slotId) => Catalog.Slots.FirstOrDefault(s => s.Id == slotId)?.Name ?? slotId;

	// As chaves ainda não vistas, sem precisar montar o host inteiro (o dock pergunta isso toda hora).
	public static IEnumerable<string> NewKeys(InventoryService inventory, string profileId, GearCatalog catalog, IEnumerable<string> ownedCosmetics) =>
		catalog.Items.Select(i => GearKey(i.Id)).Concat(ownedCosmetics.Select(CosmeticKey)).Where(key => inventory.IsNew(profileId, key));

	public static string GearKey(string id) => "gear:" + id;

	public static string CosmeticKey(string id) => "cosmetic:" + id;
}
