using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// O chão hexagonal de uma Sala da Dungeon: o hexágono do kit lote02, tingido com a cor da função da Sala (a cor nunca é a única pista).
// Sem o kit, vale o chão de prismas num só mesh.
public static class RoomMeshes
{
	const float TileGap = 0.98f;
	const float TileThickness = 0.5f;

	// Cada hexágono vem com a altura do topo (o desnível sobe alguns).
	public static void AddFloor(Node3D parent, IEnumerable<(HexCell Cell, float Height)> tiles, Color baseColor, bool cleared)
	{
		var list = tiles.ToList();
		if (AddKitFloor(parent, list, baseColor, cleared)) return;
		tiles = list;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var (cell, height) in tiles)
		{
			var world = HexGrid.ToWorld(cell);
			var center = new Vector3(world.X, height, world.Y);
			var tint = (cell.Q + cell.R) % 2 == 0 ? 0.85f : 1f;
			var top = baseColor.Lerp(Colors.White, 0.35f) * tint;
			top = cleared ? top.Lerp(Color.FromHtml("#8BC34A"), 0.35f) : top;
			top.A = 1f;
			var corners = Enumerable.Range(0, 6).Select(i => center + new Vector3(Mathf.Cos(Mathf.DegToRad(60f * i)), 0f, Mathf.Sin(Mathf.DegToRad(60f * i))) * TileGap).ToArray();
			var down = new Vector3(0f, -TileThickness - height, 0f);
			var side = top.Darkened(0.4f);
			for (var i = 0; i < 6; i++)
			{
				var a = corners[i];
				var b = corners[(i + 1) % 6];
				Triangle(st, center, a, b, Vector3.Up, top);
				var outward = ((a + b) / 2f - center).Normalized();
				Triangle(st, a, a + down, b, outward, side);
				Triangle(st, b, a + down, b + down, outward, side);
			}
		}
		parent.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f } });
	}

	const string KitTile = "lote02/hex-base-v02";
	static readonly Dictionary<Color, Material> Tints = [];

	// O hexágono do kit no lugar de cada prisma. Devolve falso se o modelo não carregou, e aí vale o chão de reserva.
	static bool AddKitFloor(Node3D parent, List<(HexCell Cell, float Height)> tiles, Color baseColor, bool cleared)
	{
		if (!ResourceLoader.Exists($"res://assets/modelos/{KitTile}.glb")) return false;
		foreach (var (cell, height) in tiles)
		{
			if (Lobby.LoadModel(KitTile) is not { } tile) return false;
			var world = HexGrid.ToWorld(cell);
			var tint = (cell.Q + cell.R) % 2 == 0 ? 0.9f : 1f;
			var color = baseColor.Lerp(Colors.White, 0.3f) * tint;
			color = cleared ? color.Lerp(Color.FromHtml("#8BC34A"), 0.3f) : color;
			color.A = 1f;
			parent.AddChild(tile);
			tile.Position = new Vector3(world.X, height, world.Y);
			foreach (var mesh in tile.FindChildren("*", "MeshInstance3D", true, false))
				if (mesh is MeshInstance3D shown && Tinted(shown, color) is { } material) shown.MaterialOverride = material;
		}
		return true;
	}

	// Uma cópia do material do kit na cor da Sala, uma só para cada cor.
	static Material? Tinted(MeshInstance3D mesh, Color color)
	{
		if (Tints.TryGetValue(color, out var cached) && IsInstanceValid(cached)) return cached;
		if (mesh.GetActiveMaterial(0) is not StandardMaterial3D source) return null;
		var copy = (StandardMaterial3D)source.Duplicate();
		// O atlas do kit é de grama: sem ele a cor da função da Sala aparece inteira, e a forma do hexágono continua a do kit.
		copy.AlbedoTexture = null;
		copy.AlbedoColor = color;
		Tints[color] = copy;
		return copy;
	}

	static bool IsInstanceValid(GodotObject @object) => GodotObject.IsInstanceValid(@object);

	static void Triangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
	{
		foreach (var point in new[] { a, b, c })
		{
			st.SetNormal(normal);
			st.SetColor(color);
			st.AddVertex(point);
		}
	}
}
