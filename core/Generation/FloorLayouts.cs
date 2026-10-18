using System.Numerics;
using ArquipelagoDoCeu.World;

namespace ArquipelagoDoCeu.Generation;

// Como as Salas se espalham: a de sempre, com formas sorteadas (versão 2) ou compacta, com as Salas perto umas das outras (versão 3).
public enum LayoutStyle { Legacy, Varied, Compact }

// Posiciona as Salas de um Andar no espaço e liga cada par ligado por um corredor de hexágonos (ADR 0008).
// O grafo vem pronto do gerador: aqui só se decide onde cada Sala fica, então a versão antiga do gerador segue abrindo igual.
public static class FloorLayouts
{
	// Distância entre os centros dos espaços onde as Salas podem cair; com o desvio, os corredores saem curtos, longos, retos ou com curva.
	const int Pitch = 20;
	const int Jitter = 2;
	const int CompactPitch = 13;
	const int CompactJitter = 1;
	// Hexágonos vazios que separam uma Sala de qualquer outra coisa: sem isso a criança passaria de uma para outra sem cruzar a porta.
	const int Gap = 2;
	const int Attempts = 60;
	const int TurnCost = 2;
	const int Margin = 14;

	static readonly HexCell[] Directions = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

	// `Varied` sorteia a forma de cada Sala (versão 2 do gerador em diante); `Legacy` deixa todas na Sala de sempre.
	public static Result<FloorLayout> Place(Floor floor, RoomForms forms, ulong seed, LayoutStyle style)
	{
		var rng = new Pcg32(seed, 91);
		for (var attempt = 0; attempt < Attempts; attempt++)
		{
			var placed = PlaceRooms(floor, forms, rng, style);
			if (placed is not null && Connect(floor, placed) is { } corridors)
				return Result<FloorLayout>.Ok(new FloorLayout(placed, corridors));
		}
		return Result<FloorLayout>.Fail($"Não foi possível posicionar as Salas do Andar {floor.Number}.");
	}

	// Em largura a partir da entrada: cada Sala cai ao lado do espaço da que a levou até ela.
	static List<PlacedRoom>? PlaceRooms(Floor floor, RoomForms forms, Pcg32 rng, LayoutStyle style)
	{
		var varied = style != LayoutStyle.Legacy;
		var placed = new Dictionary<int, PlacedRoom>
		{
			[floor.Entrance] = new PlacedRoom(floor.Entrance, varied ? forms.Pick(rng) : forms.Legacy, new HexCell(0, 0)),
		};
		var queue = new Queue<int>([floor.Entrance]);

		while (queue.Count > 0)
		{
			var parent = queue.Dequeue();
			foreach (var child in floor.Neighbors(parent).Order().Where(n => !placed.ContainsKey(n)))
			{
				var room = TryPlace(child, placed.Values, placed[parent], forms, rng, style);
				if (room is null) return null;
				placed[child] = room;
				queue.Enqueue(child);
			}
		}
		return floor.Rooms.Count == placed.Count ? placed.Values.OrderBy(r => r.Room).ToList() : null;
	}

	static PlacedRoom? TryPlace(int id, IEnumerable<PlacedRoom> others, PlacedRoom parent, RoomForms forms, Pcg32 rng, LayoutStyle style)
	{
		var form = style != LayoutStyle.Legacy ? forms.Pick(rng) : forms.Legacy;
		var (pitch, jitter) = style == LayoutStyle.Compact ? (CompactPitch, CompactJitter) : (Pitch, Jitter);
		var order = Enumerable.Range(0, Directions.Length).ToList();
		rng.Shuffle(order);
		var taken = others.ToList();
		foreach (var direction in order)
		{
			var step = Directions[direction];
			var origin = new HexCell(
				parent.Origin.Q + step.Q * pitch + rng.NextInt(2 * jitter + 1) - jitter,
				parent.Origin.R + step.R * pitch + rng.NextInt(2 * jitter + 1) - jitter);
			var room = new PlacedRoom(id, form, origin);
			if (taken.All(t => Apart(room, t))) return room;
		}
		return null;
	}

	static bool Apart(PlacedRoom a, PlacedRoom b)
	{
		var near = Halo(a.Cells, Gap);
		return !b.Cells.Any(near.Contains);
	}

	// Um corredor por ligação, na ordem dos números das Salas; cada um desvia das Salas e dos corredores que já existem.
	static List<Corridor>? Connect(Floor floor, List<PlacedRoom> rooms)
	{
		var halos = rooms.ToDictionary(r => r.Room, r => Halo(r.Cells, 1));
		var corridorHalo = new HashSet<HexCell>();
		var corridors = new List<Corridor>();

		foreach (var link in floor.Links.OrderBy(l => Math.Min(l.A, l.B)).ThenBy(l => Math.Max(l.A, l.B)))
		{
			var from = rooms[link.A];
			var to = rooms[link.B];
			var start = Door(from, to, halos, corridorHalo);
			var goal = Door(to, from, halos, corridorHalo);
			if (start is null || goal is null) return null;
			var path = Route(start.Value, goal.Value, rooms, halos, corridorHalo);
			if (path is null) return null;

			corridors.Add(new Corridor(link.A, link.B, path));
			corridorHalo.UnionWith(Halo(path, 1));
		}
		return corridors;
	}

	// Os hexágonos dos `cells` e todos os que ficam a até `radius` deles.
	static HashSet<HexCell> Halo(IEnumerable<HexCell> cells, int radius)
	{
		var halo = new HashSet<HexCell>();
		foreach (var cell in cells)
			for (var q = -radius; q <= radius; q++)
				for (var r = -radius; r <= radius; r++)
					if (Math.Abs(q + r) <= radius) halo.Add(cell + new HexCell(q, r));
		return halo;
	}

	// A porta de `room` voltada para `target`: o hexágono vazio, encostado na Sala, mais adiantado nessa direção.
	// Fica longe de qualquer outra Sala e dos outros corredores.
	static HexCell? Door(PlacedRoom room, PlacedRoom target, Dictionary<int, HashSet<HexCell>> halos, HashSet<HexCell> corridorHalo)
	{
		var origin = HexGrid.ToWorld(room.Spot);
		var toward = Vector2.Normalize(HexGrid.ToWorld(target.Spot) - origin);
		var own = room.Cells.ToHashSet();
		var outside = Outside(own);
		return own.SelectMany(c => c.Neighbors())
			.Where(n => outside.Contains(n) && !corridorHalo.Contains(n) && !halos.Any(h => h.Key != room.Room && h.Value.Contains(n)))
			.OrderByDescending(n => Vector2.Dot(HexGrid.ToWorld(n) - origin, toward))
			.ThenBy(n => n.Q).ThenBy(n => n.R)
			.Cast<HexCell?>().FirstOrDefault();
	}

	// Os hexágonos vazios ao redor da forma que dão para o lado de fora; os de dentro de um buraco ou vão fechado ficam de fora.
	static HashSet<HexCell> Outside(HashSet<HexCell> room)
	{
		var (minQ, maxQ) = (room.Min(c => c.Q) - 2, room.Max(c => c.Q) + 2);
		var (minR, maxR) = (room.Min(c => c.R) - 2, room.Max(c => c.R) + 2);
		var start = new HexCell(minQ, minR);
		var seen = new HashSet<HexCell> { start };
		var queue = new Queue<HexCell>([start]);
		while (queue.Count > 0)
			foreach (var next in queue.Dequeue().Neighbors().Where(n => n.Q >= minQ && n.Q <= maxQ && n.R >= minR && n.R <= maxR && !room.Contains(n)))
				if (seen.Add(next)) queue.Enqueue(next);
		return seen;
	}

	// Menor caminho de hexágonos, com um custo por curva para os corredores saírem retos quando dá.
	static List<HexCell>? Route(HexCell start, HexCell goal, List<PlacedRoom> rooms, Dictionary<int, HashSet<HexCell>> halos, HashSet<HexCell> corridorHalo)
	{
		var all = rooms.SelectMany(r => r.Cells).ToList();
		var (minQ, maxQ) = (all.Min(c => c.Q) - Margin, all.Max(c => c.Q) + Margin);
		var (minR, maxR) = (all.Min(c => c.R) - Margin, all.Max(c => c.R) + Margin);
		var anyHalo = halos.Values.SelectMany(h => h).ToHashSet();

		bool Open(HexCell c) =>
			c == goal || (c.Q >= minQ && c.Q <= maxQ && c.R >= minR && c.R <= maxR && !anyHalo.Contains(c) && !corridorHalo.Contains(c));

		var cost = new Dictionary<(HexCell, int), int> { [(start, -1)] = 0 };
		var from = new Dictionary<(HexCell, int), (HexCell, int)>();
		var open = new PriorityQueue<(HexCell Cell, int Dir), int>();
		open.Enqueue((start, -1), start.DistanceTo(goal));
		while (open.TryDequeue(out var current, out _))
		{
			if (current.Cell == goal) return Rebuild(from, current);
			for (var d = 0; d < Directions.Length; d++)
			{
				var next = current.Cell + Directions[d];
				if (!Open(next)) continue;
				var total = cost[current] + 1 + (current.Dir >= 0 && current.Dir != d ? TurnCost : 0);
				var key = (next, d);
				if (cost.TryGetValue(key, out var known) && known <= total) continue;
				cost[key] = total;
				from[key] = current;
				open.Enqueue(key, total + next.DistanceTo(goal));
			}
		}
		return null;
	}

	static List<HexCell> Rebuild(Dictionary<(HexCell, int), (HexCell, int)> from, (HexCell Cell, int Dir) end)
	{
		var path = new List<HexCell> { end.Cell };
		var current = end;
		while (from.TryGetValue(current, out var before))
		{
			path.Add(before.Item1);
			current = before;
		}
		path.Reverse();
		return path;
	}
}
