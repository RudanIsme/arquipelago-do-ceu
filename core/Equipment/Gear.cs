using ArquipelagoDoCeu.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Equipment;

public enum GearSlot { Helmet, Armor, Accessory }

public enum GearAttribute { Energy, Speed, Reach }

// Um equipamento: ocupa um espaço, dá um atributo e pede uma Maestria mínima do Papel. Não evolui: um substitui o outro.
public sealed record GearItem(string Id, string Name, string Description, GearSlot Slot, GearAttribute Attribute, double Value, int MinMastery);

// O máximo que o conjunto inteiro pode somar, igual ao do experimento do PR #97.
public sealed record GearLimits(int Energy, double Speed, double Reach);

// O que um Papel veste: um item por espaço (ou nada). Os ids vêm do catálogo em dados.
public sealed record Gear(string? Helmet = null, string? Armor = null, string? Accessory = null)
{
	public string? In(GearSlot slot) => slot switch { GearSlot.Helmet => Helmet, GearSlot.Armor => Armor, _ => Accessory };

	public Gear With(GearSlot slot, string? id) => slot switch
	{
		GearSlot.Helmet => this with { Helmet = id },
		GearSlot.Armor => this with { Armor = id },
		_ => this with { Accessory = id },
	};
}

// Os equipamentos do jogo, em dados (game/data/equipamentos.json), e as regras de vestir e de somar os atributos.
// Quem não tem a Maestria do Papel não equipa. Os atributos vêm do conjunto de agora e nunca acumulam.
public sealed class GearCatalog
{
	GearCatalog(IReadOnlyList<GearItem> items, GearLimits limits)
	{
		Items = items;
		Limits = limits;
	}

	public IReadOnlyList<GearItem> Items { get; }

	public GearLimits Limits { get; }

	public static GearCatalog Empty => new([], new GearLimits(0, 0, 0));

	public GearItem? Find(string? id) => id is null ? null : Items.FirstOrDefault(i => i.Id == id);

	public IEnumerable<GearItem> For(GearSlot slot) => Items.Where(i => i.Slot == slot);

	public Result<Gear> Equip(Gear current, string itemId, int mastery)
	{
		if (Find(itemId) is not { } item) return Result<Gear>.Fail("Esse equipamento não existe.");
		if (mastery < item.MinMastery) return Result<Gear>.Fail($"Para vestir {item.Name} você precisa de Maestria {item.MinMastery}.");
		return Result<Gear>.Ok(current.With(item.Slot, item.Id));
	}

	public Gear Unequip(Gear current, GearSlot slot) => current.With(slot, null);

	// Soma os atributos do que está vestido, até o limite. Item que saiu do catálogo é ignorado em vez de quebrar.
	public EquipmentBonuses Bonuses(Gear gear)
	{
		var worn = new[] { GearSlot.Helmet, GearSlot.Armor, GearSlot.Accessory }.Select(s => Find(gear.In(s))).OfType<GearItem>().ToList();
		double Sum(GearAttribute attribute) => worn.Where(i => i.Attribute == attribute).Sum(i => i.Value);
		return new EquipmentBonuses(
			(float)(1 + Math.Min(Limits.Speed, Sum(GearAttribute.Speed))),
			(int)Math.Min(Limits.Energy, Math.Round(Sum(GearAttribute.Energy))),
			(float)Math.Min(Limits.Reach, Sum(GearAttribute.Reach)));
	}

	// O texto dos atributos de um conjunto, na mesma conta que vale em jogo.
	public string Summary(Gear gear)
	{
		var bonus = Bonuses(gear);
		var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
		return $"Velocidade +{Math.Round((bonus.SpeedMultiplier - 1) * 100)}% · Energia máxima +{bonus.ExtraEnergy} · Alcance +{bonus.InteractionReachBonus.ToString("0.##", pt)}";
	}

	public static Result<GearCatalog> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de equipamentos");
		if (!read.IsOk) return Result<GearCatalog>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.Limits is not { } limits || dto.Items is not { Count: > 0 }) return Result<GearCatalog>.Fail("O catálogo de equipamentos precisa de limites e de ao menos um item.");

		var items = dto.Items.Select(i => new GearItem(i.Id ?? "", i.Name ?? "", i.Description ?? "", i.Slot, i.Attribute, i.Value, i.MinMastery)).ToList();
		if (items.Any(i => i.Id.Length == 0 || i.Name.Length == 0 || i.MinMastery < 0 || i.Value < 0)) return Result<GearCatalog>.Fail("Um equipamento do catálogo está sem id, nome ou com valor inválido.");
		if (CatalogJson.Duplicate(items, i => i.Id) is { } repeated) return Result<GearCatalog>.Fail($"O catálogo de equipamentos repete o id {repeated}.");

		var limit = new GearLimits(limits.Energy, limits.Speed, limits.Reach);
		var over = items.FirstOrDefault(i => i.Value > (i.Attribute switch { GearAttribute.Energy => limit.Energy, GearAttribute.Speed => limit.Speed, _ => limit.Reach }));
		return over is null ? Result<GearCatalog>.Ok(new GearCatalog(items, limit)) : Result<GearCatalog>.Fail($"O equipamento {over.Id} passa do limite do conjunto.");
	}

	sealed record FileDto(LimitsDto? Limits, List<ItemDto>? Items);

	sealed record LimitsDto(int Energy, double Speed, double Reach);

	sealed record ItemDto(string? Id, string? Name, string? Description, GearSlot Slot, GearAttribute Attribute, double Value, int MinMastery);
}

// Lê e grava o equipamento de cada Papel (a torre) no Perfil. A Maestria necessária vem de quem chama (ProgressService.Mastery).
public sealed class GearService(SaveSession session, GearCatalog catalog)
{
	public Gear Get(string profileId, string role) =>
		session.Data.Profiles.FirstOrDefault(p => p.Id == profileId)?.Gear?.GetValueOrDefault(role) ?? new Gear();

	public EquipmentBonuses Bonuses(string profileId, string role) => catalog.Bonuses(Get(profileId, role));

	public Result<Gear> Equip(string profileId, string role, string itemId, int mastery)
	{
		var equipped = catalog.Equip(Get(profileId, role), itemId, mastery);
		return equipped.IsOk ? Save(profileId, role, equipped.Value!) : equipped;
	}

	public Result<Gear> Unequip(string profileId, string role, GearSlot slot) => Save(profileId, role, catalog.Unequip(Get(profileId, role), slot));

	// Grava o conjunto inteiro de uma vez (a oficina troca armadura e capacete juntos).
	public Result<Gear> Set(string profileId, string role, Gear gear) => Save(profileId, role, gear);

	Result<Gear> Save(string profileId, string role, Gear gear)
	{
		var current = session.Data.Profiles.FirstOrDefault(p => p.Id == profileId);
		if (current is null) return Result<Gear>.Fail("Perfil não encontrado.");

		var all = new Dictionary<string, Gear>(current.Gear ?? new Dictionary<string, Gear>()) { [role] = gear };
		var updated = current with { Gear = all };
		var saved = session.Commit(data => data with { Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList() });
		return saved.IsOk ? Result<Gear>.Ok(gear) : Result<Gear>.Fail(saved.Error!);
	}
}
