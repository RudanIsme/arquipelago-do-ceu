using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Generation;

// A função de uma Sala. `Exit` é a escada para o Andar seguinte; no último Andar a saída é o Desafio épico.
public enum RoomKind { Entrance, Combat, Puzzle, Rest, FastTravel, Marco, Epic, Exit }

public sealed record Room(int Id, RoomKind Kind, bool HoldsItem = false);

public sealed record Link(int A, int B);

// Um Andar: grafo de Salas ligadas entre si. Combate, Puzzle, Marco e épico se vencem para seguir adiante.
public sealed record Floor(int Number, IReadOnlyList<Room> Rooms, IReadOnlyList<Link> Links, int Entrance, int Exit)
{
	public IEnumerable<int> Neighbors(int room) =>
		Links.Where(l => l.A == room || l.B == room).Select(l => l.A == room ? l.B : l.A);
}

// Gera um Andar inteiro só pela seed e pelo Plano (ADR 0003). Tudo fica antes do Marco, que é o único caminho até a saída,
// então Fast travel, Descanso e todo Puzzle são alcançáveis sem Item.
public static class FloorGenerator
{
	const int Fixed = 3; // entrada, saída e Fast travel; o Descanso entra junto das Salas livres

	public static Floor Generate(DungeonRules rules, ulong world, string discipline, int level, FloorPlan plan, int generatorVersion)
	{
		var rng = new Pcg32(Seed.Mix(world, discipline, level, plan.Number, generatorVersion));
		var total = rules.RoomsMin + rng.NextInt(rules.RoomsMax - rules.RoomsMin + 1);

		var free = FreeRooms(plan, total, rng);
		var rooms = new List<Room> { new(0, RoomKind.Entrance) };
		var links = new List<Link>();

		// Cada Sala se liga a uma já colocada: o resultado é uma árvore a partir da entrada.
		foreach (var room in free)
		{
			var id = rooms.Count;
			links.Add(new Link(rng.NextInt(rooms.Count), id));
			rooms.Add(room with { Id = id });
		}

		// Um laço a mais deixa o grafo livre; fica entre Salas anteriores ao Marco, que continua sendo a única passagem.
		AddLoop(rooms.Count, links, rng);

		var gate = rng.NextInt(rooms.Count);
		if (plan.HasMarco)
		{
			var marco = rooms.Count;
			links.Add(new Link(gate, marco));
			rooms.Add(new Room(marco, RoomKind.Marco));
			gate = marco;
		}

		var exit = rooms.Count;
		links.Add(new Link(gate, exit));
		rooms.Add(new Room(exit, plan.HasEpic ? RoomKind.Epic : RoomKind.Exit));
		return new Floor(plan.Number, rooms, links, 0, exit);
	}

	// As Salas que ficam antes do Marco, em ordem sorteada: Fast travel, Descanso e as de combate ou Puzzle.
	static List<Room> FreeRooms(FloorPlan plan, int total, Pcg32 rng)
	{
		var fillers = total - Fixed - 1 - (plan.HasMarco ? 1 : 0);
		var rooms = new List<Room> { new(0, RoomKind.FastTravel), new(0, RoomKind.Rest) };
		for (var i = 0; i < fillers; i++)
			rooms.Add(new Room(0, rng.NextInt(2) == 0 ? RoomKind.Combat : RoomKind.Puzzle));

		if (plan.HoldsItem)
		{
			var candidates = Enumerable.Range(0, rooms.Count).Where(i => rooms[i].Kind is RoomKind.Combat or RoomKind.Puzzle).ToList();
			var pick = candidates[rng.NextInt(candidates.Count)];
			rooms[pick] = rooms[pick] with { HoldsItem = true };
		}

		rng.Shuffle(rooms);
		return rooms;
	}

	static void AddLoop(int roomCount, List<Link> links, Pcg32 rng)
	{
		if (roomCount < 4 || rng.NextInt(2) == 0) return;
		for (var attempt = 0; attempt < 8; attempt++)
		{
			var a = rng.NextInt(roomCount);
			var b = rng.NextInt(roomCount);
			if (a == b || links.Any(l => (l.A == a && l.B == b) || (l.A == b && l.B == a))) continue;
			links.Add(new Link(Math.Min(a, b), Math.Max(a, b)));
			return;
		}
	}
}
