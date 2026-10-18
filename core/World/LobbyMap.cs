using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.World;

// O chão do Lobby em ilhas: a ilha principal (um disco, menos os cantos recortados) e uma ilha para cada torre e para o Mercador,
// ligadas por pontes a pé. Tudo é hexágono da mesma malha, então colisão, caminho e câmera funcionam como antes.
public sealed class LobbyMap
{
	LobbyMap(HashSet<HexCell> main, Dictionary<string, IReadOnlySet<HexCell>> islands, Dictionary<string, IReadOnlyList<HexCell>> bridges)
	{
		Main = main;
		Islands = islands;
		Bridges = bridges;
	}

	public IReadOnlySet<HexCell> Main { get; }

	public IReadOnlyDictionary<string, IReadOnlySet<HexCell>> Islands { get; }

	// Os hexágonos da ponte de cada ilha, da ilha principal para ela.
	public IReadOnlyDictionary<string, IReadOnlyList<HexCell>> Bridges { get; }

	public IEnumerable<HexCell> AllCells => Main.Concat(Islands.Values.SelectMany(c => c)).Concat(Bridges.Values.SelectMany(c => c));

	public static Result<LobbyMap> Build(LobbyRules rules)
	{
		var main = Disc(new HexCell(0, 0), rules.IslandRadius);
		main.ExceptWith(rules.Carved.Select(c => new HexCell(c.Q, c.R)));
		var islands = new Dictionary<string, IReadOnlySet<HexCell>>();
		var bridges = new Dictionary<string, IReadOnlyList<HexCell>>();
		var taken = new HashSet<HexCell>(main);

		foreach (var island in rules.Islands)
		{
			var center = new HexCell(island.Q, island.R);
			var from = new HexCell(island.FromQ, island.FromR);
			var ground = Disc(center, island.Radius);
			if (!main.Contains(from)) return Result<LobbyMap>.Fail($"A ponte da ilha '{island.Id}' precisa sair de um hexágono da ilha principal.");
			if (ground.Any(taken.Contains)) return Result<LobbyMap>.Fail($"A ilha '{island.Id}' toca outra ilha ou a ilha principal.");

			var bridge = Line(from, center).Where(c => !main.Contains(c) && !ground.Contains(c)).ToList();
			if (bridge.Count == 0) return Result<LobbyMap>.Fail($"A ilha '{island.Id}' fica colada: falta lugar para a ponte.");
			if (!bridge[0].Neighbors().Any(main.Contains) || !bridge[^1].Neighbors().Any(ground.Contains))
				return Result<LobbyMap>.Fail($"A ponte da ilha '{island.Id}' não liga a ilha principal à ilha.");
			if (bridge.Any(taken.Contains)) return Result<LobbyMap>.Fail($"A ponte da ilha '{island.Id}' passa por outra ilha.");

			islands[island.Id] = ground;
			bridges[island.Id] = bridge;
			taken.UnionWith(ground);
			taken.UnionWith(bridge);
		}
		return Result<LobbyMap>.Ok(new LobbyMap(main, islands, bridges));
	}

	// O chão pisável: ilhas e pontes, menos onde há objeto e o centro de cada ilha de torre (onde a torre está).
	public HexGrid Grid(LobbyRules rules)
	{
		var blocked = rules.Objects.Select(o => new HexCell(o.Q, o.R)).Concat(rules.Islands.Where(i => i.Solid).Select(i => new HexCell(i.Q, i.R)));
		return new HexGrid(AllCells, blocked);
	}

	static HashSet<HexCell> Disc(HexCell center, int radius)
	{
		var cells = new HashSet<HexCell>();
		for (var q = -radius; q <= radius; q++)
			for (var r = -radius; r <= radius; r++)
				if (Math.Abs(q + r) <= radius) cells.Add(new HexCell(center.Q + q, center.R + r));
		return cells;
	}

	// Os hexágonos de uma reta de `a` até `b`, em passos iguais pelo cubo e arredondados.
	public static IReadOnlyList<HexCell> Line(HexCell a, HexCell b)
	{
		var steps = a.DistanceTo(b);
		var cells = new List<HexCell>();
		for (var i = 1; i <= steps; i++)
		{
			var t = (double)i / steps;
			var x = a.Q + (b.Q - a.Q) * t;
			var z = a.R + (b.R - a.R) * t;
			cells.Add(Round(x, -x - z, z));
		}
		return cells;
	}

	static HexCell Round(double x, double y, double z)
	{
		var (rx, ry, rz) = (Math.Round(x), Math.Round(y), Math.Round(z));
		var (dx, dy, dz) = (Math.Abs(rx - x), Math.Abs(ry - y), Math.Abs(rz - z));
		if (dx > dy && dx > dz) rx = -ry - rz;
		else if (dy <= dz) rz = -rx - ry;
		return new HexCell((int)rx, (int)rz);
	}
}
