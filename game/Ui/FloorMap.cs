using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Generation;
using Godot;

namespace ArquipelagoDoCeu;

// Desenha o grafo de um Andar: a entrada em cima e as Salas mais longe dela mais abaixo.
// Cada função tem forma e letra próprias, para a cor nunca ser a única informação.
public partial class FloorMap : Control
{
	const float Radius = 17f;

	static readonly Dictionary<RoomKind, (string Letter, string Name, Color Color)> Look = new()
	{
		[RoomKind.Entrance] = ("E", "Entrada", Color.FromHtml("#8BC34A")),
		[RoomKind.Combat] = ("C", "Combate", Color.FromHtml("#E57373")),
		[RoomKind.Puzzle] = ("P", "Puzzle", Color.FromHtml("#64B5F6")),
		[RoomKind.Rest] = ("D", "Descanso", Color.FromHtml("#BA9BEA")),
		[RoomKind.FastTravel] = ("F", "Fast travel", Color.FromHtml("#4DD0C4")),
		[RoomKind.Marco] = ("M", "Marco", Color.FromHtml("#FFB74D")),
		[RoomKind.Epic] = ("É", "Épico", Color.FromHtml("#FFD54F")),
		[RoomKind.Exit] = ("S", "Saída", Color.FromHtml("#B0BEC5")),
	};

	readonly Floor floor;
	readonly Dictionary<int, Vector2> positions = new();

	// Salas já vencidas ou visitadas, e a Sala onde a criança está; usados pelo jogo, e vazios no mapa de depuração.
	ISet<int> cleared = new HashSet<int>();
	int? here;

	public ISet<int> Cleared
	{
		get => cleared;
		set { cleared = value; QueueRedraw(); }
	}

	public int? Here
	{
		get => here;
		set { here = value; QueueRedraw(); }
	}

	public FloorMap(Floor floor, Vector2 size)
	{
		this.floor = floor;
		CustomMinimumSize = size;
		Size = size;
		Layout(size);
	}

	public static Color ColorOf(RoomKind kind) => Look[kind].Color;

	public static string NameOf(RoomKind kind) => Look[kind].Name;

	public static string Legend => string.Join("   ", Look.Values.Select(v => $"{v.Letter} {v.Name}"));

	void Layout(Vector2 size)
	{
		var depth = new Dictionary<int, int> { [floor.Entrance] = 0 };
		var queue = new Queue<int>([floor.Entrance]);
		while (queue.Count > 0)
		{
			var room = queue.Dequeue();
			foreach (var next in floor.Neighbors(room).Where(n => !depth.ContainsKey(n)))
			{
				depth[next] = depth[room] + 1;
				queue.Enqueue(next);
			}
		}

		var levels = depth.GroupBy(p => p.Value).OrderBy(g => g.Key).ToList();
		var step = levels.Count > 1 ? (size.Y - 2 * (Radius + 8)) / (levels.Count - 1) : 0f;
		foreach (var level in levels)
		{
			var ids = level.Select(p => p.Key).OrderBy(id => id).ToList();
			for (var i = 0; i < ids.Count; i++)
				positions[ids[i]] = new Vector2(size.X * (i + 1) / (ids.Count + 1), Radius + 8 + level.Key * step);
		}
	}

	public override void _Draw()
	{
		var line = Color.FromHtml("#D9A64A");
		foreach (var link in floor.Links)
			DrawLine(positions[link.A], positions[link.B], line, 3f, true);

		var font = ThemeDB.FallbackFont;
		foreach (var room in floor.Rooms)
		{
			var (letter, _, color) = Look[room.Kind];
			var at = positions[room.Id];
			var dim = Cleared.Contains(room.Id) ? 0.45f : 1f;
			var fill = new Color(color.R, color.G, color.B, dim);
			Shape(room.Kind, at, fill);
			if (Here == room.Id) DrawArc(at, Radius + 6, 0, Mathf.Tau, 24, Colors.White, 3f, true);
			var width = font.GetStringSize(letter, HorizontalAlignment.Left, -1, 20).X;
			DrawString(font, at + new Vector2(-width / 2, 7), letter, HorizontalAlignment.Left, -1, 20, Colors.Black);
			if (room.HoldsItem)
				DrawString(font, at + new Vector2(Radius - 4, -Radius + 6), "★", HorizontalAlignment.Left, -1, 18, Color.FromHtml("#FFEB3B"));
		}
	}

	// Combate é círculo, Puzzle é quadrado, Marco e épico são losangos e o resto é um hexágono: a forma diz a função.
	void Shape(RoomKind kind, Vector2 at, Color fill)
	{
		switch (kind)
		{
			case RoomKind.Combat or RoomKind.Entrance:
				DrawCircle(at, Radius, fill);
				break;
			case RoomKind.Puzzle:
				DrawRect(new Rect2(at - new Vector2(Radius, Radius), new Vector2(Radius * 2, Radius * 2)), fill);
				break;
			case RoomKind.Marco or RoomKind.Epic:
				DrawColoredPolygon([at + new Vector2(0, -Radius - 4), at + new Vector2(Radius + 4, 0), at + new Vector2(0, Radius + 4), at + new Vector2(-Radius - 4, 0)], fill);
				break;
			default:
				DrawColoredPolygon(Enumerable.Range(0, 6).Select(i => at + Radius * new Vector2(Mathf.Cos(i * Mathf.Tau / 6), Mathf.Sin(i * Mathf.Tau / 6))).ToArray(), fill);
				break;
		}
	}
}
