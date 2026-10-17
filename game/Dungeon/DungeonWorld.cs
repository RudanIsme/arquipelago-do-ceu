using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// A cena 3D de uma Sala da Dungeon: o chão hexagonal, as portas para as Salas ligadas e a criatura.
// Greybox, sem tema visual: cada função tem cor, forma e texto, e o jogo reconstrói a Sala a cada mudança.
public partial class DungeonWorld : Node3D, ICombatStage
{
	const int RoomRadius = 3;
	const float TileGap = 0.98f;
	const float TileThickness = 0.5f;
	const float CreatureHeight = 1.7f;
	const float PillarHeight = 1.8f;

	public sealed record Door(int Target, Vector3 Ground, string Label, bool Locked);

	// Cada porta ocupa um hexágono da borda; um Andar tem poucas ligações por Sala, então 18 posições sobram.
	static readonly HexCell[] Ring = BuildRing();

	readonly Node3D room = new();
	readonly List<Door> doors = new();
	readonly List<Label3D> labels = new();
	Font? font;

	public DungeonWorld()
	{
		Grid = HexGrid.Island(RoomRadius, []);
		Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 13f, Current = false };
		Creature = new TrainingDummy { Title = "Criatura da névoa", RestingHint = "Escolha-a para lutar", RestingCompact = "Lutar [F]" };
	}

	public HexGrid Grid { get; }

	public Camera3D Camera { get; }

	public TrainingDummy Creature { get; }

	public IReadOnlyList<Door> Doors => doors;

	// Com o que a criança pode interagir na Sala de agora: as portas, a escada e a fonte do Descanso.
	public IReadOnlyList<Interactable> Interactables(Dungeon.Expedition expedition)
	{
		var list = doors.Select(d => new Interactable($"door:{d.Target}", new System.Numerics.Vector2(d.Ground.X, d.Ground.Z))).ToList();
		if (expedition.CanAscend) list.Add(new Interactable("stairs", StairsSpot));
		if (expedition.CanDescend) list.Add(new Interactable("stairs-down", StairsSpot));
		if (expedition.Current.Kind == RoomKind.Rest) list.Add(new Interactable("rest", FountainSpot));
		return list;
	}

	// A criatura fica no centro da Sala.
	public (Vector3 Ground, float Top) CreatureSpot => (Vector3.Zero, CreatureHeight);

	public bool ReduceMotion { get; set; }

	public override void _Ready()
	{
		Camera.Environment = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Color.FromHtml("#241C31"),
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = Color.FromHtml("#C9BFDC"),
			AmbientLightEnergy = 0.8f,
		};
		AddChild(Camera);
		Camera.LookAtFromPosition(new Vector3(12f, 14f, 12f), new Vector3(0f, 1f, 0f), Vector3.Up);
		AddChild(new DirectionalLight3D { LightColor = Color.FromHtml("#FFE4BC"), LightEnergy = 1.1f, ShadowEnabled = true, RotationDegrees = new Vector3(-50f, -30f, 0f) });
		AddChild(room);
		AddChild(Creature);
		AddChild(barrier);

		var model = Lobby.LoadModel("boneco");
		if (model is not null)
		{
			AddChild(model);
			Lobby.FitToHeight(model, CreatureHeight, Vector3.Zero);
			creatureModel = model;
		}
	}

	Node3D? creatureModel;
	readonly MeshInstance3D barrier = new()
	{
		Mesh = new BoxMesh { Size = new Vector3(3.4f, 2.2f, 0.4f) },
		MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.7f, 1f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
		Position = new Vector3(0f, 1.1f, 0f),
		Rotation = new Vector3(0f, Mathf.Pi / 4f, 0f),
		Visible = false,
	};

	public void SetFont(Font? value)
	{
		font = value;
		foreach (var label in labels) label.Font = value;
		Creature.SetFont(value);
	}

	// A Sala some com a criatura quando a luta termina; o modelo e os textos dela vão junto.
	// No Puzzle o alvo é uma barreira trancada, e não a criatura.
	public void ShowTarget(bool fight, bool puzzle)
	{
		Creature.Visible = fight;
		if (creatureModel is not null) creatureModel.Visible = fight && !puzzle;
		barrier.Visible = fight && puzzle;
	}

	// Reconstrói a Sala: chão pela função, portas para as vizinhas e o letreiro do centro.
	// `describe` dá o texto da porta, se está trancada e se a Sala já é conhecida: a porta de uma Sala nova não a revela.
	public void Show(Floor floor, int roomId, IReadOnlySet<int> cleared, System.Func<int, (string Label, bool Locked, bool Known)> describe, string? note = null, bool stairsDown = false)
	{
		foreach (var child in room.GetChildren()) child.QueueFree();
		doors.Clear();
		labels.Clear();

		var kind = floor.Rooms[roomId].Kind;
		AddFloor(FloorMap.ColorOf(kind), cleared.Contains(roomId));

		var neighbors = floor.Neighbors(roomId).OrderBy(n => n).ToList();
		for (var i = 0; i < neighbors.Count; i++)
		{
			var cell = Ring[(i * Ring.Length / neighbors.Count + Ring.Length / (2 * neighbors.Count)) % Ring.Length];
			var ground = Lobby.HexToWorld(cell.Q, cell.R);
			var (label, locked, known) = describe(neighbors[i]);
			var door = new Door(neighbors[i], ground, label, locked);
			doors.Add(door);
			AddDoor(door, known ? FloorMap.ColorOf(floor.Rooms[neighbors[i]].Kind) : Color.FromHtml("#8A8399"));
		}

		AddSign($"{FloorMap.NameOf(kind)}", new Vector3(0f, kind is RoomKind.Combat or RoomKind.Epic or RoomKind.Puzzle ? 5.6f : 2.4f, 0f), 56);
		if (note is not null) AddSign(note, new Vector3(0f, 3.4f, 0f), 34);
		if (kind == RoomKind.Exit || stairsDown) AddStairs(stairsDown);
		if (kind == RoomKind.Rest) AddFountain(cleared.Contains(roomId));
	}

	// A porta da Sala `target`, para a criança chegar nela vinda de lá.
	public Door? DoorTo(int target) => doors.FirstOrDefault(d => d.Target == target);

	// Um ponto do chão de dentro da Sala, junto da porta: onde a criança aparece ao chegar por ela.
	public static System.Numerics.Vector2 InsideOf(Door door) => new(door.Ground.X * 0.62f, door.Ground.Z * 0.62f);

	public static System.Numerics.Vector2 Center => System.Numerics.Vector2.Zero;

	// A escada do Andar fica no meio da Sala de saída (para subir) ou da entrada (para descer).
	public static System.Numerics.Vector2 StairsSpot => new(0f, 0f);

	void AddFloor(Color baseColor, bool cleared)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var cell in HexGridCells())
		{
			var center = Lobby.HexToWorld(cell.Q, cell.R);
			var tint = (cell.Q + cell.R) % 2 == 0 ? 0.85f : 1f;
			var top = baseColor.Lerp(Colors.White, 0.35f) * tint;
			top = cleared ? top.Lerp(Color.FromHtml("#8BC34A"), 0.35f) : top;
			top.A = 1f;
			var corners = Enumerable.Range(0, 6).Select(i => center + new Vector3(Mathf.Cos(Mathf.DegToRad(60f * i)), 0f, Mathf.Sin(Mathf.DegToRad(60f * i))) * TileGap).ToArray();
			for (var i = 0; i < 6; i++)
			{
				var a = corners[i];
				var b = corners[(i + 1) % 6];
				Triangle(st, center, a, b, Vector3.Up, top);
				var down = new Vector3(0f, -TileThickness, 0f);
				var outward = ((a + b) / 2f - center).Normalized();
				var side = top.Darkened(0.4f);
				Triangle(st, a, a + down, b, outward, side);
				Triangle(st, b, a + down, b + down, outward, side);
			}
		}
		room.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f } });
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

	// Duas colunas e uma verga; a cor é a da Sala do outro lado, e o texto diz o que é e se está trancada.
	void AddDoor(Door door, Color color)
	{
		var tint = door.Locked ? Color.FromHtml("#6B6578") : color;
		var material = new StandardMaterial3D { AlbedoColor = tint, Roughness = 0.8f };
		var way = new Vector3(door.Ground.X, 0f, door.Ground.Z).Normalized();
		var side = new Vector3(-way.Z, 0f, way.X) * 0.55f;
		foreach (var offset in new[] { side, -side })
			room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.28f, PillarHeight, 0.28f) }, MaterialOverride = material, Position = door.Ground + offset + new Vector3(0f, PillarHeight / 2f, 0f) });
		var lintel = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.26f, 0.3f) }, MaterialOverride = material, Position = door.Ground + new Vector3(0f, PillarHeight + 0.1f, 0f) };
		lintel.Rotation = new Vector3(0f, Mathf.Atan2(side.X, side.Z) - Mathf.Pi / 2f, 0f);
		room.AddChild(lintel);
		AddSign(door.Label, door.Ground + new Vector3(0f, PillarHeight + 0.95f, 0f), 40);
	}

	// O Descanso é uma fonte no centro da Sala; ela escurece depois de usada.
	void AddFountain(bool used)
	{
		var water = new StandardMaterial3D { AlbedoColor = used ? Color.FromHtml("#7E7A99") : Color.FromHtml("#BA9BEA"), Roughness = 0.4f };
		room.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.1f, BottomRadius = 1.2f, Height = 0.5f }, MaterialOverride = water, Position = new Vector3(0f, 0.25f, 0f) });
		room.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f }, MaterialOverride = water, Position = new Vector3(0f, 0.9f, 0f) });
	}

	public static System.Numerics.Vector2 FountainSpot => System.Numerics.Vector2.Zero;

	void AddStairs(bool down)
	{
		var material = new StandardMaterial3D { AlbedoColor = FloorMap.ColorOf(RoomKind.Exit), Roughness = 0.8f };
		if (down) AddSign("Escada para baixo", new Vector3(0f, 3.4f, 0f), 40);
		for (var step = 0; step < 4; step++)
			room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.8f - step * 0.3f, 0.25f, 1.8f - step * 0.3f) }, MaterialOverride = material, Position = new Vector3(0f, 0.15f + step * 0.25f, 0f) });
	}

	void AddSign(string text, Vector3 at, int size)
	{
		var label = new Label3D
		{
			Text = text,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = size,
			PixelSize = 0.012f,
			OutlineSize = 16,
			OutlineModulate = Color.FromHtml("#1B1424"),
			Modulate = Color.FromHtml("#FFF6E0"),
			NoDepthTest = true,
			Position = at,
			Font = font,
		};
		room.AddChild(label);
		labels.Add(label);
	}

	IEnumerable<HexCell> HexGridCells() => Grid.Cells;

	static HexCell[] BuildRing()
	{
		var cells = new List<HexCell>();
		for (var q = -RoomRadius; q <= RoomRadius; q++)
			for (var r = -RoomRadius; r <= RoomRadius; r++)
			{
				var cell = new HexCell(q, r);
				if (cell.DistanceTo(new HexCell(0, 0)) == RoomRadius) cells.Add(cell);
			}
		return cells.OrderBy(c => { var p = HexGrid.ToWorld(c); return System.MathF.Atan2(p.Y, p.X); }).ToArray();
	}
}
