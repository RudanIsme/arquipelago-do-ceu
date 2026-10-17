using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using Godot;

namespace ArquipelagoDoCeu;

// Desenha o grafo de um Andar, entrada em cima. Cada função tem forma e letra, para a cor nunca ser a única informação.
// Com uma Expedição ligada só aparece o explorado: Salas visitadas, vizinhas sob névoa ("?") e o cadeado.
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
	Expedition? source;
	bool complete;

	public Expedition? Source
	{
		get => source;
		set { source = value; QueueRedraw(); }
	}

	// O selo de Andar completo, no canto do mapa.
	public bool Complete
	{
		get => complete;
		set { complete = value; QueueRedraw(); }
	}

	public void Refresh() => QueueRedraw();

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

	public static string Legend => string.Join("   ", Look.Values.Select(v => $"{v.Letter} {v.Name}")) + "\n? Sala nova (névoa)   Cadeado: falta vencer uma Sala";

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
			if (SightOf(link.A) == Sight.Seen || SightOf(link.B) == Sight.Seen)
			{
				DrawLine(positions[link.A], positions[link.B], line, 3f, true);
				if (floor.Rooms[link.A].Kind == RoomKind.Puzzle || floor.Rooms[link.B].Kind == RoomKind.Puzzle) DrawDoorMark(positions[link.A], positions[link.B]);
			}

		var font = ThemeDB.FallbackFont;
		foreach (var room in floor.Rooms)
		{
			var sight = SightOf(room.Id);
			if (sight == Sight.Hidden) continue;
			if (sight == Sight.Fog)
			{
				DrawFog(font, positions[room.Id], source!.IsLocked(room.Id));
				continue;
			}

			var (letter, _, color) = Look[room.Kind];
			var at = positions[room.Id];
			var done = Cleared.Contains(room.Id);
			Shape(room.Kind, at, new Color(color.R, color.G, color.B, done ? 0.55f : 1f));
			var width = font.GetStringSize(letter, HorizontalAlignment.Left, -1, 20).X;
			DrawString(font, at + new Vector2(-width / 2, 7), letter, HorizontalAlignment.Left, -1, 20, Colors.Black);
			if (room.HoldsItem)
				DrawString(font, at + new Vector2(Radius - 4, -Radius + 6), "★", HorizontalAlignment.Left, -1, 18, Color.FromHtml("#FFEB3B"));
			// Cada estado tem forma própria: visto (vencida), arco (Fast travel) e boneco (onde a criança está).
			if (done) DrawCheck(at + new Vector2(-Radius + 3, -Radius + 5));
			if (room.Kind == RoomKind.FastTravel) DrawArch(at + new Vector2(-Radius + 2, Radius - 2));
			if (Here == room.Id) DrawPerson(at + new Vector2(Radius - 1, Radius - 3));
		}

		if (!complete) return;
		DrawCircle(new Vector2(18, 18), 16, Colors.Black);
		DrawCircle(new Vector2(18, 18), 14, Color.FromHtml("#FFD54F"));
		DrawString(font, new Vector2(7, 26), "★", HorizontalAlignment.Left, -1, 22, Colors.Black);
	}

	Sight SightOf(int room) => source?.SightOf(room) ?? Sight.Seen;

	void DrawCheck(Vector2 at)
	{
		Vector2[] points = [at + new Vector2(-5, 0), at + new Vector2(-1, 4), at + new Vector2(6, -5)];
		DrawPolyline(points, Colors.Black, 6f, true);
		DrawPolyline(points, Color.FromHtml("#7CFC8A"), 3f, true);
	}

	void DrawArch(Vector2 at)
	{
		DrawArc(at + new Vector2(0, -2), 6f, Mathf.Pi, Mathf.Tau, 10, Colors.Black, 6f, true);
		DrawLine(at + new Vector2(-6, -2), at + new Vector2(-6, 5), Colors.Black, 6f);
		DrawLine(at + new Vector2(6, -2), at + new Vector2(6, 5), Colors.Black, 6f);
		DrawArc(at + new Vector2(0, -2), 6f, Mathf.Pi, Mathf.Tau, 10, Colors.White, 3f, true);
		DrawLine(at + new Vector2(-6, -2), at + new Vector2(-6, 5), Colors.White, 3f);
		DrawLine(at + new Vector2(6, -2), at + new Vector2(6, 5), Colors.White, 3f);
	}

	void DrawPerson(Vector2 at)
	{
		DrawCircle(at + new Vector2(0, -6), 5f, Colors.Black);
		DrawRect(new Rect2(at + new Vector2(-5, -1), new Vector2(10, 9)), Colors.Black);
		DrawCircle(at + new Vector2(0, -6), 3.5f, Colors.White);
		DrawRect(new Rect2(at + new Vector2(-3.5f, 0), new Vector2(7, 6.5f)), Colors.White);
	}

	// Um traço atravessando a linha: é a porta (o Puzzle) entre duas Salas.
	static readonly Func<Vector2, Vector2, Vector2> Perpendicular = (a, b) => (b - a).Normalized().Orthogonal();

	void DrawDoorMark(Vector2 a, Vector2 b)
	{
		var mid = (a + b) / 2;
		var across = Perpendicular(a, b) * 8f;
		DrawLine(mid - across, mid + across, Colors.Black, 6f);
		DrawLine(mid - across, mid + across, Colors.White, 3f);
	}

	// Parar o dedo ou o mouse sobre uma Sala pede a descrição dela em voz alta.
	public event Action<int>? RoomPointed;

	int? pointed;
	int pointEpoch;

	public override void _Ready() => MouseExited += () => pointed = null;

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not (InputEventMouseMotion or InputEventMouseButton { Pressed: true })) return;
		var position = ((InputEventMouse)@event).Position;
		var near = positions.Where(p => SightOf(p.Key) != Sight.Hidden && p.Value.DistanceTo(position) <= Radius + 6).Select(p => (int?)p.Key).FirstOrDefault();
		var click = @event is InputEventMouseButton;
		if (near == pointed && !click) return;
		pointed = near;
		var mine = ++pointEpoch;
		if (near is not { } room) return;
		// Passar o mouse pelo mapa não fala a cada Sala: só a em que ele para (um clique ou toque fala na hora).
		if (click) RoomPointed?.Invoke(room);
		else GetTree().CreateTimer(0.4f, true, false, true).Timeout += () =>
		{
			if (mine == pointEpoch && pointed == room) RoomPointed?.Invoke(room);
		};
	}

	// A Sala sob névoa é um círculo cinza com "?"; o cadeado é desenhado (corpo e argola), e nunca só uma cor.
	void DrawFog(Font font, Vector2 at, bool locked)
	{
		DrawCircle(at, Radius, Color.FromHtml("#8A8399"));
		DrawArc(at, Radius, 0, Mathf.Tau, 24, Color.FromHtml("#E6E0F2"), 2f, true);
		var width = font.GetStringSize("?", HorizontalAlignment.Left, -1, 20).X;
		DrawString(font, at + new Vector2(-width / 2, 7), "?", HorizontalAlignment.Left, -1, 20, Colors.Black);
		if (!locked) return;

		var lock_ = at + new Vector2(Radius - 2, -Radius + 4);
		DrawArc(lock_ + new Vector2(0, -3), 5f, Mathf.Pi, Mathf.Tau, 10, Colors.Black, 5f, true);
		DrawArc(lock_ + new Vector2(0, -3), 5f, Mathf.Pi, Mathf.Tau, 10, Colors.White, 2.5f, true);
		DrawRect(new Rect2(lock_ + new Vector2(-8, -3), new Vector2(16, 13)), Colors.Black);
		DrawRect(new Rect2(lock_ + new Vector2(-6, -1), new Vector2(12, 9)), Color.FromHtml("#FFD54F"));
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
