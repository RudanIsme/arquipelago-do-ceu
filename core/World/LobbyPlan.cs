using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.World;

// O desenho do Lobby em zonas, tudo relativo ao raio da ilha (em dados, ajustado à mão): a lagoa no centro, o caminho em anel, uma praça e
// um caminho até cada zona e até cada ponte, e as árvores. Mudar o raio muda o tamanho de tudo junto, sem deixar canto vazio nem nada fora da ilha.
public sealed class LobbyPlan
{
	// A distância do centro à borda da ilha, em unidades do mundo (um hexágono tem 1,5 de largura de passo): as frações dos dados valem sobre ela.
	const float StepWidth = 1.5f;
	// Quantos pontos se amostram no anel: o bastante para os hexágonos virarem um caminho contínuo.
	const int RingSamples = 720;

	LobbyPlan(IReadOnlySet<HexCell> lake, IReadOnlySet<HexCell> path, IReadOnlyDictionary<string, HexCell> zones, IReadOnlyList<HexCell> trees)
	{
		Lake = lake;
		Path = path;
		Zones = zones;
		Trees = trees;
	}

	public IReadOnlySet<HexCell> Lake { get; }

	// O caminho: o anel, os braços até as zonas e as pontes, e a praça em volta de cada zona.
	public IReadOnlySet<HexCell> Path { get; }

	public IReadOnlyDictionary<string, HexCell> Zones { get; }

	public IReadOnlyList<HexCell> Trees { get; }

	public static LobbyPlan Empty { get; } = new(new HashSet<HexCell>(), new HashSet<HexCell>(), new Dictionary<string, HexCell>(), []);

	// O hexágono de um ponto dado em fração do raio da ilha (x para leste, z para o sul).
	public static HexCell CellOf(float x, float z, int islandRadius)
	{
		var reach = islandRadius * StepWidth;
		return HexGrid.FromWorld(new Vector2(x * reach, z * reach));
	}

	public static Result<LobbyPlan> Build(LobbyRules rules, LobbyMap map)
	{
		if (rules.Layout is not { } layout) return Result<LobbyPlan>.Ok(Empty);

		var reach = rules.IslandRadius * StepWidth;
		var main = map.Main;
		var lake = main.Where(c => Distance(c) <= layout.LakeRadius * reach).ToHashSet();
		var ring = Circle(layout.RingRadius * reach).Where(c => main.Contains(c) && !lake.Contains(c)).Distinct().ToList();
		if (ring.Count == 0) return Result<LobbyPlan>.Fail("O anel do Lobby ('lobby.layout.ringRadius') não cai na ilha.");

		var zones = new Dictionary<string, HexCell>();
		var path = new HashSet<HexCell>(ring);
		foreach (var zone in layout.Zones)
		{
			var cell = CellOf(zone.X, zone.Z, rules.IslandRadius);
			if (!main.Contains(cell) || lake.Contains(cell)) return Result<LobbyPlan>.Fail($"A zona '{zone.Id}' do Lobby cai fora da ilha ou dentro da lagoa.");
			zones[zone.Id] = cell;
			path.UnionWith(Arm(Nearest(ring, cell), cell, main, lake));
			path.UnionWith(cell.Neighbors().Where(n => main.Contains(n) && !lake.Contains(n)));
		}
		foreach (var island in rules.Islands)
		{
			var gate = new HexCell(island.FromQ, island.FromR);
			path.UnionWith(Arm(Nearest(ring, gate), gate, main, lake));
		}

		var trees = new List<HexCell>();
		foreach (var spot in layout.Trees)
		{
			var cell = CellOf(spot.X, spot.Z, rules.IslandRadius);
			if (!main.Contains(cell) || lake.Contains(cell) || path.Contains(cell) || zones.ContainsValue(cell) || trees.Contains(cell))
				return Result<LobbyPlan>.Fail($"Uma árvore do Lobby ({spot.X}, {spot.Z}) cai fora da ilha, na lagoa, no caminho, numa zona ou em cima de outra árvore.");
			trees.Add(cell);
		}
		return Result<LobbyPlan>.Ok(new LobbyPlan(lake, path, zones, trees));
	}

	static float Distance(HexCell cell) => HexGrid.ToWorld(cell).Length();

	// Os hexágonos por onde passa um círculo do centro da ilha: amostrar fino deixa o anel sem buracos.
	static IEnumerable<HexCell> Circle(float radius) =>
		Enumerable.Range(0, RingSamples).Select(i => HexGrid.FromWorld(new Vector2(MathF.Cos(i * MathF.Tau / RingSamples), MathF.Sin(i * MathF.Tau / RingSamples)) * radius));

	static HexCell Nearest(IReadOnlyList<HexCell> cells, HexCell target) => cells.OrderBy(c => c.DistanceTo(target)).First();

	// O caminho de `from` até `to`, sem entrar na lagoa nem sair da ilha.
	static IEnumerable<HexCell> Arm(HexCell from, HexCell to, IReadOnlySet<HexCell> main, IReadOnlySet<HexCell> lake) =>
		LobbyMap.Line(from, to).Prepend(from).Where(c => main.Contains(c) && !lake.Contains(c));
}
