using System.Numerics;

namespace ArquipelagoDoCeu.World;

// Hexágono em coordenadas axiais (q, r).
public readonly record struct HexCell(int Q, int R)
{
	static readonly HexCell[] Offsets = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

	public int DistanceTo(HexCell other)
	{
		var dq = Q - other.Q;
		var dr = R - other.R;
		return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
	}

	public IEnumerable<HexCell> Neighbors()
	{
		var (q, r) = (Q, R);
		return Offsets.Select(o => new HexCell(q + o.Q, r + o.R));
	}
}

// O chão do Lobby: hexágonos "flat-top" de raio 1 (centro ao vértice). O mundo é plano, então um ponto
// é Vector2 (x, z). Quem anda só precisa saber o que é livre, por onde ir e até onde pode se mover.
public sealed class HexGrid
{
	readonly HashSet<HexCell> cells;
	readonly HashSet<HexCell> blocked;

	public HexGrid(IEnumerable<HexCell> cells, IEnumerable<HexCell> blocked)
	{
		this.cells = [.. cells];
		this.blocked = [.. blocked];
	}

	public static HexGrid Island(int radius, IEnumerable<HexCell> blocked)
	{
		var cells = new List<HexCell>();
		for (var q = -radius; q <= radius; q++)
			for (var r = -radius; r <= radius; r++)
				if (Math.Abs(q + r) <= radius) cells.Add(new HexCell(q, r));
		return new HexGrid(cells, blocked);
	}

	public IReadOnlyCollection<HexCell> Cells => cells;

	public bool IsWalkable(HexCell cell) => cells.Contains(cell) && !blocked.Contains(cell);

	public IEnumerable<HexCell> WalkableNeighbors(HexCell cell) => cell.Neighbors().Where(IsWalkable);

	public static Vector2 ToWorld(HexCell cell) => new(1.5f * cell.Q, MathF.Sqrt(3f) * (cell.R + cell.Q / 2f));

	public static HexCell FromWorld(Vector2 point)
	{
		var q = 2f / 3f * point.X;
		var r = point.Y / MathF.Sqrt(3f) - q / 2f;
		// Arredondamento em coordenadas cúbicas: corrige a coordenada que mais errou.
		var s = -q - r;
		var rq = MathF.Round(q);
		var rr = MathF.Round(r);
		var rs = MathF.Round(s);
		var dq = MathF.Abs(rq - q);
		var dr = MathF.Abs(rr - r);
		var ds = MathF.Abs(rs - s);
		if (dq > dr && dq > ds) rq = -rr - rs;
		else if (dr > ds) rr = -rq - rs;
		return new HexCell((int)rq, (int)rr);
	}

	// A*: cada passo custa 1 e a distância hexagonal nunca superestima o que falta.
	public IReadOnlyList<HexCell>? FindPath(HexCell from, HexCell to)
	{
		if (!IsWalkable(from) || !IsWalkable(to)) return null;

		var cameFrom = new Dictionary<HexCell, HexCell>();
		var cost = new Dictionary<HexCell, int> { [from] = 0 };
		var open = new PriorityQueue<HexCell, int>();
		open.Enqueue(from, from.DistanceTo(to));

		while (open.TryDequeue(out var current, out _))
		{
			if (current == to) return Rebuild(cameFrom, current);
			foreach (var next in WalkableNeighbors(current))
			{
				var nextCost = cost[current] + 1;
				if (cost.TryGetValue(next, out var known) && known <= nextCost) continue;
				cost[next] = nextCost;
				cameFrom[next] = current;
				open.Enqueue(next, nextCost + next.DistanceTo(to));
			}
		}
		return null;
	}

	static List<HexCell> Rebuild(Dictionary<HexCell, HexCell> cameFrom, HexCell end)
	{
		var path = new List<HexCell> { end };
		while (cameFrom.TryGetValue(path[^1], out var before)) path.Add(before);
		path.Reverse();
		return path;
	}

	// O hexágono livre mais perto de um destino bloqueado ou fora da ilha. No empate vence o mais perto de quem anda
	// (o lado do obstáculo que ele já está vendo); o resto do empate é por posição, para ser determinístico.
	public HexCell? NearestWalkable(HexCell target, HexCell? from = null)
	{
		HexCell? best = null;
		foreach (var cell in cells.Where(IsWalkable).OrderBy(c => from is null ? 0 : c.DistanceTo(from.Value)).ThenBy(c => c.Q).ThenBy(c => c.R))
			if (best is null || cell.DistanceTo(target) < best.Value.DistanceTo(target)) best = cell;
		return best;
	}

	// Livre para um corpo de certo raio: o centro e quatro pontos em volta estão em hexágonos livres.
	public bool IsFree(Vector2 point, float radius) =>
		IsWalkable(FromWorld(point))
		&& IsWalkable(FromWorld(point + new Vector2(radius, 0f)))
		&& IsWalkable(FromWorld(point - new Vector2(radius, 0f)))
		&& IsWalkable(FromWorld(point + new Vector2(0f, radius)))
		&& IsWalkable(FromWorld(point - new Vector2(0f, radius)));

	// Anda sem atravessar obstáculos nem sair da ilha; ao encostar, desliza pelo eixo que continua livre.
	public Vector2 Move(Vector2 from, Vector2 delta, float radius)
	{
		// Se já está colado num obstáculo (por exemplo, tocou perto dele), só o centro conta, para poder sair.
		bool Fits(Vector2 point) => IsFree(from, radius) ? IsFree(point, radius) : IsWalkable(FromWorld(point));

		var full = from + delta;
		if (Fits(full)) return full;
		var alongX = new Vector2(full.X, from.Y);
		if (delta.X != 0f && Fits(alongX)) return alongX;
		var alongY = new Vector2(from.X, full.Y);
		if (delta.Y != 0f && Fits(alongY)) return alongY;
		return from;
	}
}
