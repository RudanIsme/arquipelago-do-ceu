using System.Text.Json;
using System.Text.Json.Serialization;
using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Equipment;

// Um espaço do Personagem (Capacete, Armadura, Acessório...), declarado em dados.
public sealed record GearSlot(string Id, string Name);

// Um equipamento: ocupa um espaço, dá um efeito e pede uma Maestria mínima do Papel. Não evolui: um substitui o outro.
public sealed record GearItem(string Id, string Name, string Description, string Slot, string Effect, double Value, int MinMastery);

// O quanto um efeito soma no conjunto vestido.
public sealed record GearTotal(string Effect, IGearEffect Definition, double Value);

// O que um efeito muda no Personagem. Um efeito novo é uma classe registrada em GearEffects, sem tocar no cálculo dos bônus.
public interface IGearEffect
{
	string Id { get; }

	// Soma o total do efeito (já dentro do limite) aos bônus e devolve os bônus novos.
	EquipmentBonuses Apply(EquipmentBonuses bonuses, double total);

	string Describe(double total);

	// O nome curto do atributo ("Energia") e o valor com unidade ("12%"), para a comparação do Inventário e a voz.
	string Label => Id;

	string Format(double value) => value.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}

sealed class EnergyEffect : IGearEffect
{
	public string Id => "energy";

	public EquipmentBonuses Apply(EquipmentBonuses bonuses, double total) => bonuses with { ExtraEnergy = (int)Math.Round(total) };

	public string Describe(double total) => $"Energia máxima +{(int)Math.Round(total)}";

	public string Label => "Energia";
}

sealed class SpeedEffect : IGearEffect
{
	public string Id => "speed";

	public EquipmentBonuses Apply(EquipmentBonuses bonuses, double total) => bonuses with { SpeedMultiplier = (float)(1 + total) };

	public string Describe(double total) => $"Velocidade +{Math.Round(total * 100)}%";

	public string Label => "Velocidade";

	public string Format(double value) => $"{Math.Round(value * 100)}%";
}

sealed class ReachEffect : IGearEffect
{
	public string Id => "reach";

	public EquipmentBonuses Apply(EquipmentBonuses bonuses, double total) => bonuses with { InteractionReachBonus = (float)total };

	public string Describe(double total) => $"Alcance +{total.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}";

	public string Label => "Alcance";
}

// Os efeitos que o jogo sabe aplicar, por id.
public sealed class GearEffects
{
	readonly IReadOnlyDictionary<string, IGearEffect> byId;

	GearEffects(IReadOnlyDictionary<string, IGearEffect> byId) => this.byId = byId;

	public static GearEffects Default { get; } = new(new IGearEffect[] { new EnergyEffect(), new SpeedEffect(), new ReachEffect() }.ToDictionary(e => e.Id));

	public IGearEffect? Find(string id) => byId.GetValueOrDefault(id);

	public GearEffects With(IGearEffect effect) => new(new Dictionary<string, IGearEffect>(byId) { [effect.Id] = effect });
}

// O que um Papel veste: uma peça por espaço (ou nada). No save é um objeto simples de espaço para peça.
[JsonConverter(typeof(GearJsonConverter))]
public sealed class Gear : IEquatable<Gear>
{
	readonly IReadOnlyDictionary<string, string> items;

	public Gear(IReadOnlyDictionary<string, string>? items = null) => this.items = new Dictionary<string, string>(items ?? new Dictionary<string, string>());

	public IReadOnlyDictionary<string, string> Items => items;

	public string? In(string slot) => items.GetValueOrDefault(slot);

	public Gear With(string slot, string? item)
	{
		var next = new Dictionary<string, string>(items);
		if (item is null) next.Remove(slot);
		else next[slot] = item;
		return new Gear(next);
	}

	public bool Equals(Gear? other) => other is not null && items.Count == other.items.Count && items.All(p => other.items.GetValueOrDefault(p.Key) == p.Value);

	public override bool Equals(object? obj) => obj is Gear other && Equals(other);

	public override int GetHashCode() => items.Aggregate(items.Count, (hash, p) => hash ^ HashCode.Combine(p.Key, p.Value));
}

sealed class GearJsonConverter : JsonConverter<Gear>
{
	public override Gear Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var read = JsonSerializer.Deserialize<Dictionary<string, string?>>(ref reader, options) ?? [];
		return new Gear(read.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value!));
	}

	public override void Write(Utf8JsonWriter writer, Gear value, JsonSerializerOptions options) =>
		JsonSerializer.Serialize(writer, value.Items.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value), options);
}

// Os equipamentos do jogo, em dados (game/data/equipamentos.json), e as regras de vestir e de somar os efeitos.
// Quem não tem a Maestria do Papel não equipa. Os efeitos vêm do conjunto de agora e nunca acumulam.
public sealed class GearCatalog
{
	readonly GearEffects effects;
	readonly IReadOnlyList<(string Id, double Limit)> limits;

	GearCatalog(IReadOnlyList<GearSlot> slots, IReadOnlyList<GearItem> items, IReadOnlyList<(string Id, double Limit)> limits, GearEffects effects)
	{
		Slots = slots;
		Items = items;
		this.limits = limits;
		this.effects = effects;
	}

	public IReadOnlyList<GearSlot> Slots { get; }

	public IReadOnlyList<GearItem> Items { get; }

	public static GearCatalog Empty => new([], [], [], GearEffects.Default);

	public GearItem? Find(string? id) => id is null ? null : Items.FirstOrDefault(i => i.Id == id);

	public IEnumerable<GearItem> For(string slot) => Items.Where(i => i.Slot == slot);

	public Result<Gear> Equip(Gear current, string itemId, int mastery)
	{
		if (Find(itemId) is not { } item) return Result<Gear>.Fail("Esse equipamento não existe.");
		if (mastery < item.MinMastery) return Result<Gear>.Fail($"Para vestir {item.Name} você precisa de Maestria {item.MinMastery}.");
		return Result<Gear>.Ok(current.With(item.Slot, item.Id));
	}

	public Gear Unequip(Gear current, string slot) => current.With(slot, null);

	// Soma os efeitos do que está vestido, até o limite de cada um. Item que saiu do catálogo é ignorado em vez de quebrar.
	public EquipmentBonuses Bonuses(Gear gear)
	{
		var worn = Slots.Select(s => Find(gear.In(s.Id))).OfType<GearItem>().ToList();
		var bonuses = new EquipmentBonuses();
		foreach (var (id, limit) in limits)
		{
			var total = Math.Min(limit, worn.Where(i => i.Effect == id).Sum(i => i.Value));
			if (effects.Find(id) is { } effect) bonuses = effect.Apply(bonuses, total);
		}
		return bonuses;
	}

	// O total de cada efeito do conjunto (já dentro do limite), na ordem do catálogo: a mesma conta de Bonuses.
	public IReadOnlyList<GearTotal> Totals(Gear gear)
	{
		var worn = Slots.Select(s => Find(gear.In(s.Id))).OfType<GearItem>().ToList();
		return limits.Where(l => effects.Find(l.Id) is not null)
			.Select(l => new GearTotal(l.Id, effects.Find(l.Id)!, Math.Min(l.Limit, worn.Where(i => i.Effect == l.Id).Sum(i => i.Value)))).ToList();
	}

	// O texto dos efeitos de um conjunto, na mesma conta que vale em jogo.
	public string Summary(Gear gear)
	{
		var worn = Slots.Select(s => Find(gear.In(s.Id))).OfType<GearItem>().ToList();
		return string.Join(" · ", limits.Select(l => effects.Find(l.Id)?.Describe(Math.Min(l.Limit, worn.Where(i => i.Effect == l.Id).Sum(i => i.Value)))).OfType<string>());
	}

	public static Result<GearCatalog> Parse(string json, GearEffects effects)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de equipamentos");
		if (!read.IsOk) return Result<GearCatalog>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.Slots is not { Count: > 0 } || dto.Effects is not { Count: > 0 } || dto.Items is not { Count: > 0 })
			return Result<GearCatalog>.Fail("O catálogo de equipamentos precisa de espaços, efeitos e ao menos um item.");

		var slots = dto.Slots.Select(s => new GearSlot(s.Id ?? "", s.Name ?? "")).ToList();
		if (slots.Any(s => s.Id.Length == 0 || s.Name.Length == 0)) return Result<GearCatalog>.Fail("Um espaço do catálogo de equipamentos está sem id ou nome.");
		if (CatalogJson.Duplicate(slots, s => s.Id) is { } repeatedSlot) return Result<GearCatalog>.Fail($"O catálogo de equipamentos repete o espaço {repeatedSlot}.");

		var limits = dto.Effects.Select(e => (Id: e.Id ?? "", e.Limit)).ToList();
		var withoutClass = limits.Where(l => effects.Find(l.Id) is null).Select(l => l.Id).FirstOrDefault();
		if (withoutClass is not null) return Result<GearCatalog>.Fail($"O efeito {withoutClass} do catálogo de equipamentos não tem uma classe registrada.");

		var items = dto.Items.Select(i => new GearItem(i.Id ?? "", i.Name ?? "", i.Description ?? "", i.Slot ?? "", i.Effect ?? "", i.Value, i.MinMastery)).ToList();
		if (items.Any(i => i.Id.Length == 0 || i.Name.Length == 0 || i.MinMastery < 0 || i.Value < 0)) return Result<GearCatalog>.Fail("Um equipamento do catálogo está sem id, nome ou com valor inválido.");
		if (CatalogJson.Duplicate(items, i => i.Id) is { } repeated) return Result<GearCatalog>.Fail($"O catálogo de equipamentos repete o id {repeated}.");

		var problem = items.Select(i => ItemProblem(i, slots, limits)).FirstOrDefault(p => p is not null);
		return problem is null ? Result<GearCatalog>.Ok(new GearCatalog(slots, items, limits, effects)) : Result<GearCatalog>.Fail(problem);
	}

	static string? ItemProblem(GearItem item, IReadOnlyList<GearSlot> slots, IReadOnlyList<(string Id, double Limit)> limits)
	{
		if (slots.All(s => s.Id != item.Slot)) return $"O equipamento {item.Id} usa o espaço {item.Slot}, que não existe.";
		var limit = limits.Where(l => l.Id == item.Effect).Select(l => (double?)l.Limit).FirstOrDefault();
		if (limit is null) return $"O equipamento {item.Id} usa o efeito {item.Effect}, que não existe.";
		return item.Value > limit ? $"O equipamento {item.Id} passa do limite do efeito {item.Effect}." : null;
	}

	sealed record FileDto(List<SlotDto>? Slots, List<EffectDto>? Effects, List<ItemDto>? Items);

	sealed record SlotDto(string? Id, string? Name);

	sealed record EffectDto(string? Id, double Limit);

	sealed record ItemDto(string? Id, string? Name, string? Description, string? Slot, string? Effect, double Value, int MinMastery);
}

// Lê e grava o equipamento de cada Papel (a torre) no Perfil. A Maestria necessária vem de quem chama (MasteryService.Of).
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

	public Result<Gear> Unequip(string profileId, string role, string slot) => Save(profileId, role, catalog.Unequip(Get(profileId, role), slot));

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
