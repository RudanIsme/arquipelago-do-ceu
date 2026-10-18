using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// As ilhas do Lobby: a principal ampliada e as das torres e do Mercador, ligadas por pontes que a criança atravessa a pé.
public partial class Lobby
{
	LobbyMap map = null!;
	readonly HashSet<HexCell> planks = new();

	// Os hexágonos de cada ilha e ponte entram no mesmo mapa de tiles, então colisão e caminho valem em todas.
	void BuildIslands()
	{
		var built = LobbyMap.Build(Rules);
		if (!built.IsOk)
		{
			GD.PushError(built.Error!);
			built = LobbyMap.Build(Rules with { Islands = [] });
		}
		map = built.Value!;
		foreach (var cell in map.Main) tiles[new Vector2I(cell.Q, cell.R)] = TileFor(cell);
		foreach (var cell in map.Islands.Values.SelectMany(c => c)) tiles[new Vector2I(cell.Q, cell.R)] = Hash01(cell.Q * 31 + cell.R * 17) < 0.2f ? Kind.Flowers : Kind.Grass;
		foreach (var cell in map.Bridges.Values.SelectMany(c => c))
		{
			tiles[new Vector2I(cell.Q, cell.R)] = Kind.Plank;
			planks.Add(cell);
		}
	}

	Kind TileFor(HexCell cell)
	{
		var (q, r) = (cell.Q, cell.R);
		var onPath = (r == 0 && Mathf.Abs(q) <= 3) || (q == 0 && Mathf.Abs(r) <= 3) || (q == 2 && (r == -3 || r == -1));
		var onPond = (q == 3 && r == -2) || (q == 3 && r == -3) || (q == 2 && r == -2) || (r == -2 && (q == 4 || q == 5));
		var flowery = Hash01(q * 31 + r * 17) < 0.2f;
		return onPond ? Kind.Water : onPath ? Kind.Path : flowery ? Kind.Flowers : Kind.Grass;
	}

	// Onde cada torre está: o centro da ilha dela, que não se pisa.
	IEnumerable<HexCell> IslandCenters() => Rules.Islands.Where(i => i.Solid).Select(i => new HexCell(i.Q, i.R));

	// A rocha debaixo da ilha principal cresce com o raio; a das outras ilhas é menor.
	void AddRocks()
	{
		var scale = 10f * (Rules.IslandRadius + 0.5f) / 5.5f;
		AddLote02("rocha-inferior-avaliacao", new Vector3(0, .573f, 0), new Vector3(scale, 2.146f, scale));
		foreach (var island in Rules.Islands)
		{
			var center = HexToWorld(island.Q, island.R);
			var radius = island.Radius * 1.6f + 0.5f;
			AddLote02("rocha-inferior-avaliacao", center + new Vector3(0, .28f, 0), new Vector3(radius, 1.36f, radius));
		}
	}

	// As pontes: tábuas de madeira sobre o vazio, com postes de cada lado.
	void AddBridges()
	{
		RoomMeshes.AddFloor(this, planks.Select(c => (c, 0f)), Color.FromHtml("#9C6B3F"), false);
		var post = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#5B4A3A"), Roughness = 0.9f };
		foreach (var bridge in map.Bridges.Values)
			for (var i = 0; i < bridge.Count; i++)
			{
				var from = HexToWorld(bridge[i].Q, bridge[i].R);
				var to = i + 1 < bridge.Count ? HexToWorld(bridge[i + 1].Q, bridge[i + 1].R) : from + (from - HexToWorld(bridge[Mathf.Max(0, i - 1)].Q, bridge[Mathf.Max(0, i - 1)].R));
				var along = (to - from).Normalized();
				var side = new Vector3(-along.Z, 0f, along.X) * 0.85f;
				foreach (var offset in new[] { side, -side })
					AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.7f, 0.14f) }, MaterialOverride = post, Position = from + offset + new Vector3(0f, 0.35f, 0f) });
			}
	}
}
