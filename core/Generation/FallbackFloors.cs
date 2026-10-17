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
		return new Floor(plan.Number, t.Rooms, t.Links, 0, t.Rooms.Count - 1);
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
			var kinds = (f.Rooms ?? []).Select(r => Enum.TryParse<RoomKind>(r.Kind, ignoreCase: true, out var k) ? (RoomKind?)k : null).ToList();
			if (kinds.Count == 0 || kinds.Contains(null))
				return Result<FallbackFloors>.Fail("Um Andar de reserva tem uma Sala com função desconhecida ou nenhuma Sala.");

			var rooms = kinds.Select((k, i) => new Room(i, k!.Value, f.Rooms![i].Item)).ToList();
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

// Entrega o Andar gerado quando ele passa no validador e o de reserva quando não passa.
// O gerador é injetável para o teste poder provar a troca com um Andar quebrado.
public delegate Floor GenerateFloor(DungeonRules rules, ulong world, string discipline, int level, FloorPlan plan, int version);

public sealed class FloorProvider(DungeonRules rules, FallbackFloors fallback, GenerateFloor? generate = null)
{
	readonly GenerateFloor generator = generate ?? FloorGenerator.Generate;

	public Floor Get(ulong world, string discipline, int level, FloorPlan plan, int version)
	{
		var floor = generator(rules, world, discipline, level, plan, version);
		return FloorValidator.Validate(floor, plan).IsOk ? floor : fallback.For(plan);
	}
}
