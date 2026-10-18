using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// As ilhas do Lobby: a principal ampliada e as das torres e do Mercador, ligadas por pontes que a criança atravessa a pé.
public partial class Lobby
{
	LobbyMap map = null!;
	LobbyPlan plan = LobbyPlan.Empty;
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
		var planned = LobbyPlan.Build(Rules, map);
		if (planned.IsOk) plan = planned.Value!;
		else GD.PushError(planned.Error!);
		foreach (var cell in map.Main) tiles[new Vector2I(cell.Q, cell.R)] = TileFor(cell);
		foreach (var cell in map.Islands.Values.SelectMany(c => c)) tiles[new Vector2I(cell.Q, cell.R)] = Hash01(cell.Q * 31 + cell.R * 17) < 0.2f ? Kind.Flowers : Kind.Grass;
		foreach (var cell in map.Bridges.Values.SelectMany(c => c))
		{
			tiles[new Vector2I(cell.Q, cell.R)] = Kind.Plank;
			planks.Add(cell);
		}
	}

	// A lagoa no centro, o caminho em anel com os braços e as praças, e grama (com flores aqui e ali) no resto.
	Kind TileFor(HexCell cell) =>
		plan.Lake.Contains(cell) ? Kind.Water : plan.Path.Contains(cell) ? Kind.Path : Hash01(cell.Q * 31 + cell.R * 17) < 0.2f ? Kind.Flowers : Kind.Grass;

	// Onde cada torre está: o centro da ilha dela, que não se pisa.
	IEnumerable<HexCell> IslandCenters() => Rules.Islands.Where(i => i.Solid).Select(i => new HexCell(i.Q, i.R));

	// As pontes entre as ilhas: as peças da ponte do kit lote02 (piso, guarda-corpo e cabeceira), as mesmas dos corredores da Dungeon.
	// Sem o kit, o piso de tábuas com postes de reserva.
	void AddBridges()
	{
		foreach (var bridge in map.Bridges.Values)
			if (!CorridorKit.Add(this, bridge)) AddPlankBridge(bridge);
	}

	void AddPlankBridge(IReadOnlyList<HexCell> bridge)
	{
		RoomMeshes.AddFloor(this, bridge.Select(c => (c, 0f)), Color.FromHtml("#9C6B3F"), false);
		var post = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#5B4A3A"), Roughness = 0.9f };
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
