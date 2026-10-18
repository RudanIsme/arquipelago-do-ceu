using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

// O contorno e a base de toda ilha com o kit lote02: margem reta nas arestas voltadas para o vazio, margem convexa nos cantos de fora e
// uma rocha do kit sob cada hexágono da beira, na escala do modelo. A margem côncava ainda não está no jogo (está na folha de pedidos de arte).
public partial class Lobby
{
	const float HexEdge = 0.8660254f;
	const float RockDrop = 0f;

	// As ilhas do Lobby (a principal e as das torres e do Mercador) e as pontes: tudo o que está em `tiles` e não é água nem tábua.
	void AddIslandKit()
	{
		var land = tiles.Where(t => t.Value is not (Kind.Water or Kind.Plank)).Select(t => t.Key).ToList();
		AddKit(land.ToDictionary(c => c, c => HexToWorld(c.X, c.Y)), cell => tiles.ContainsKey(cell));
	}

	// `centers` são os hexágonos da ilha (coordenada do hexágono e centro no mundo); `occupied` diz se um vizinho tem chão (tile, ponte, água).
	void AddKit(IReadOnlyDictionary<Vector2I, Vector3> centers, System.Func<Vector2I, bool> occupied)
	{
		foreach (var (cell, center) in centers)
		{
			var exposed = Enumerable.Range(0, Neighbors.Length).Where(i => !occupied(cell + Neighbors[i])).ToList();
			if (exposed.Count == 0) continue;
			AddRock(cell, center);
			foreach (var i in exposed) AddEdge(center, Direction(i), "margem-reta", HexEdge, 0.5f);
			foreach (var i in exposed.Where(i => exposed.Contains((i + 1) % Neighbors.Length)))
				AddEdge(center, (Direction(i) + Direction((i + 1) % Neighbors.Length)).Normalized(), "margem-convexa", 1f, 0.575f);
		}
	}

	// A direção do centro do hexágono para o vizinho `i`, no mundo.
	static Vector3 Direction(int i)
	{
		var step = HexToWorld(Neighbors[i].X, Neighbors[i].Y);
		return step.Normalized();
	}

	// Uma peça de margem: na aresta (ou no canto) que fica `reach` do centro, com o comprimento dela ao longo da borda.
	void AddEdge(Vector3 center, Vector3 outward, string asset, float reach, float halfLength)
	{
		var tangent = new Vector3(-outward.Z, 0f, outward.X);
		var at = center + outward * reach;
		AddLote02(asset, at - tangent * halfLength, yaw: Mathf.Atan2(-tangent.Z, tangent.X));
	}

	// A rocha de baixo, uma por hexágono da beira, girada e com um pouco de variação (sempre igual nos três eixos).
	void AddRock(Vector2I cell, Vector3 center)
	{
		var variation = Hash01(cell.X * 53 + cell.Y * 29);
		var scale = 1.1f + 0.35f * variation;
		AddLote02("rocha-inferior-avaliacao", center + new Vector3(0f, RockDrop, 0f), Vector3.One * scale, yaw: Mathf.Tau * Hash01(cell.X * 7 + cell.Y * 41));
	}

	// Uma ilha solta (as de Ciências, História e Geografia) feita de hexágonos do kit, com o mesmo contorno e a mesma rocha.
	void AddKitIslet(Vector3 center, int rings)
	{
		var cells = new Dictionary<Vector2I, Vector3>();
		for (var q = -rings; q <= rings; q++)
			for (var r = -rings; r <= rings; r++)
			{
				if (System.Math.Abs(q + r) > rings) continue;
				var world = center + HexToWorld(q, r);
				AddLote02("hex-base-v02", world);
				cells[new Vector2I(q, r)] = world;
			}
		AddKit(cells, cells.ContainsKey);
	}
}
