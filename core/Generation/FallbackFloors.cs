using ArquipelagoDoCeu.Data;
using System.Text.Json;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Generation;

// Os Andares feitos à mão, em dados, que entram no lugar de um Andar gerado que o validador recusou (ADR 0003).
// Há um para cada combinação do que um Andar do Plano pode ter (Item, Marco, épico).
public sealed class FallbackFloors
{
	readonly IReadOnlyList<Template> templates;

	FallbackFloors(IReadOnlyList<Template> templates) => this.templates = templates;

	public Floor For(FloorPlan plan)
	{
		var t = templates.First(t => t.Item == plan.HoldsItem && t.Marco == plan.HasMarco && t.Epic == plan.HasEpic);
		return new Floor(plan.Number, t.Rooms, t.Links, 0, t.Rooms.Count - 1, plan.Deep);
	}

	public static Result<FallbackFloors> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O arquivo dos Andares de reserva");
		if (!read.IsOk) return Result<FallbackFloors>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.Floors is not { Count: > 0 }) return Result<FallbackFloors>.Fail("O arquivo dos Andares de reserva não traz nenhum Andar.");

		var templates = new List<Template>();
		foreach (var f in dto.Floors)
		{
			var kinds = (f.Rooms ?? []).Select(r => RoomTypes.Default.Find(r.Kind ?? "")).ToList();
			if (kinds.Count == 0 || kinds.Contains(null))
				return Result<FallbackFloors>.Fail("Um Andar de reserva tem uma Sala com função desconhecida ou nenhuma Sala.");

			var rooms = kinds.Select((k, i) => new Room(i, k!, f.Rooms![i].Item)).ToList();
			var links = (f.Links ?? []).Where(l => l.Length == 2).Select(l => new Link(l[0], l[1])).ToList();
			var plan = new FloorPlan(1, f.Item, f.Marco, f.Epic);
			var template = new Template(f.Item, f.Marco, f.Epic, rooms, links);
			var valid = FloorValidator.Validate(new Floor(1, rooms, links, 0, rooms.Count - 1), plan);
			if (!valid.IsOk) return Result<FallbackFloors>.Fail($"Um Andar de reserva é inválido. {valid.Error}");
			templates.Add(template);
		}

		var missing = Shapes.FirstOrDefault(s => !templates.Any(t => (t.Item, t.Marco, t.Epic) == s));
		return missing == default
			? Result<FallbackFloors>.Ok(new FallbackFloors(templates))
			: Result<FallbackFloors>.Fail($"Falta um Andar de reserva para Item={missing.Item}, Marco={missing.Marco}, épico={missing.Epic}.");
	}

	// O Plano nunca põe o Item no último Andar nem junto do Marco, então só estes formatos existem.
	static readonly (bool Item, bool Marco, bool Epic)[] Shapes =
		[(false, false, false), (true, false, false), (false, true, false), (false, false, true), (false, true, true)];

	sealed record Template(bool Item, bool Marco, bool Epic, IReadOnlyList<Room> Rooms, IReadOnlyList<Link> Links);

	sealed record FileDto(List<FloorDto>? Floors);

	sealed record FloorDto(bool Item, bool Marco, bool Epic, List<RoomDto>? Rooms, List<int[]>? Links);

	sealed record RoomDto(string? Kind, bool Item);
}

// Entrega o Andar gerado quando ele passa no validador (do grafo e do layout) e o de reserva quando não passa.
// O gerador e o posicionador são injetáveis para o teste poder provar a troca com um Andar quebrado.
public delegate Floor GenerateFloor(DungeonRules rules, ulong world, string discipline, int level, FloorPlan plan, int version);

public delegate Result<FloorLayout> PlaceLayout(Floor floor, RoomForms forms, ulong seed, bool varied);

public sealed class FloorProvider(DungeonRules rules, FallbackFloors fallback, GenerateFloor? generate = null, RoomTypes? types = null, RoomForms? forms = null, PlaceLayout? layout = null)
{
	// A partir desta versão do gerador cada Sala sorteia a forma; as anteriores seguem com a Sala de sempre (ADR 0008).
	public const int VariedFromVersion = 2;

	const int FallbackTries = 20;

	readonly GenerateFloor generator = generate ?? ((r, w, d, l, p, v) => FloorGenerator.Generate(r, w, d, l, p, v, types));
	readonly PlaceLayout placer = layout ?? FloorLayouts.Place;

	public Floor Get(ulong world, string discipline, int level, FloorPlan plan, int version)
	{
		var floor = generator(rules, world, discipline, level, plan, version);
		var valid = FloorValidator.Validate(floor, plan).IsOk;
		if (forms is null) return valid ? floor : fallback.For(plan);

		var seed = FloorGenerator.SeedOf(world, discipline, level, plan, version);
		var varied = version >= VariedFromVersion;
		if (valid && Lay(floor, seed, varied, placer) is { } laid) return laid;
		return LaidFallback(plan, seed, varied);
	}

	Floor? Lay(Floor floor, ulong seed, bool varied, PlaceLayout place)
	{
		var placed = place(floor, forms!, seed, varied);
		if (!placed.IsOk) return null;
		var laid = floor with { Layout = placed.Value };
		return FloorLayoutValidator.Validate(laid).IsOk ? laid : null;
	}

	// O Andar de reserva também precisa de layout: tenta formas variadas e, se preciso, a Sala de sempre, com outras posições.
	Floor LaidFallback(FloorPlan plan, ulong seed, bool varied)
	{
		var floor = fallback.For(plan);
		for (var attempt = 0; attempt < FallbackTries; attempt++)
			foreach (var tryVaried in new[] { varied, false })
				if (Lay(floor, seed + (ulong)attempt, tryVaried, FloorLayouts.Place) is { } laid) return laid;
		return floor;
	}
}
