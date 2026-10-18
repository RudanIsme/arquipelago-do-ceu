using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// O chão hexagonal de uma Sala ou corredor da Dungeon, num só mesh. Greybox: a cor vem da função da Sala.
public static class RoomMeshes
{
	const float TileGap = 0.98f;
	const float TileThickness = 0.5f;

	// Cada hexágono vem com a altura do topo (o desnível sobe alguns).
	public static void AddFloor(Node3D parent, IEnumerable<(HexCell Cell, float Height)> tiles, Color baseColor, bool cleared)
	{
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
