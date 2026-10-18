using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// O mapa pelo layout real do Andar: a silhueta de cada Sala (os hexágonos dela, em escala) e os corredores entre elas.
// A névoa, o "?" e o cadeado continuam valendo; o que a criança não viu não é desenhado.
public partial class FloorMap
{
	const float Padding = 16f;

	float unit = 1f;
	Vector2? playerAt;
	Vector2 shift;

	// Põe cada Sala na posição do layout, encaixando o Andar inteiro no controle.
	void LayoutSpace(Vector2 size)
	{
		var layout = floor.Layout!;
		var points = layout.Rooms.SelectMany(r => r.Cells).Concat(layout.Corridors.SelectMany(c => c.Cells)).Select(Project).ToList();
		var (minX, maxX) = (points.Min(p => p.X), points.Max(p => p.X));
		var (minY, maxY) = (points.Min(p => p.Y), points.Max(p => p.Y));
		unit = Mathf.Min((size.X - 2 * Padding) / (maxX - minX + 2f), (size.Y - 2 * Padding) / (maxY - minY + 2f));
		shift = new Vector2(size.X / 2f - (minX + maxX) / 2f * unit, size.Y / 2f - (minY + maxY) / 2f * unit);
		foreach (var room in layout.Rooms) positions[room.Room] = Place(room.Spot);
		Radius = Mathf.Clamp(unit * 2.2f, 9f, 17f);
	}

	static Vector2 Project(HexCell cell)
	{
		var world = HexGrid.ToWorld(cell);
		return new Vector2(world.X, world.Y);
	}

	Vector2 Place(HexCell cell) => Project(cell) * unit + shift;

	// Onde a criança está agora, no espaço do Andar: o marcador anda no mapa enquanto ela anda, e não só de Sala em Sala.
	public void SetPlayer(System.Numerics.Vector2 layoutSpace)
	{
		if (floor.Layout is null) return;
		var next = new Vector2(layoutSpace.X, layoutSpace.Y) * unit + shift;
		if (playerAt is { } now && now.DistanceTo(next) < 0.5f) return;
		playerAt = next;
		QueueRedraw();
	}

	void DrawSilhouettes()
	{
		var layout = floor.Layout!;
		var line = Color.FromHtml("#D9A64A");
		foreach (var corridor in layout.Corridors.Where(c => SightOf(c.From) == Sight.Seen || SightOf(c.To) == Sight.Seen))
		{
			var path = corridor.Cells.Select(Place).ToArray();
			DrawPolyline(path, Colors.Black, Mathf.Max(5f, unit * 1.6f), true);
			DrawPolyline(path, line, Mathf.Max(3f, unit * 1.1f), true);
			if (floor.Rooms[corridor.From].Kind.IsDoor || floor.Rooms[corridor.To].Kind.IsDoor)
				DrawDoorMark(path[Mathf.Max(0, path.Length / 2 - 1)], path[Mathf.Min(path.Length - 1, path.Length / 2 + 1)]);
		}

		foreach (var room in layout.Rooms)
		{
			var sight = SightOf(room.Room);
			if (sight == Sight.Hidden) continue;
			var kind = floor.Rooms[room.Room].Kind;
			var fill = sight == Sight.Fog ? Color.FromHtml("#8A8399") : ColorOf(kind);
			if (sight == Sight.Seen && Cleared.Contains(room.Room)) fill = new Color(fill.R, fill.G, fill.B, 0.55f);
			foreach (var cell in room.Cells) DrawColoredPolygon(Hexagon(Place(cell)), fill);
		}
	}

	Vector2[] Hexagon(Vector2 center) =>
		Enumerable.Range(0, 6).Select(i => center + unit * 0.98f * new Vector2(Mathf.Cos(i * Mathf.Tau / 6f), Mathf.Sin(i * Mathf.Tau / 6f))).ToArray();
}
