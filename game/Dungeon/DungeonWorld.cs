using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// A cena 3D de uma Sala da Dungeon, das Salas vizinhas já abertas e dos corredores a pé (greybox, sem tema visual).
// O centro da Sala de agora fica sempre na origem; ao trocar de Sala, `Show` diz quanto o chão andou para quem guarda posições.
public partial class DungeonWorld : Node3D, ICombatStage
{
	const float CreatureHeight = 1.7f;
	const float CreatureRadius = 0.35f;
	const float PillarHeight = 1.8f;

	public sealed record Door(int Target, Vector3 Ground, string Label, bool Locked);

	readonly Node3D room = new();
	readonly List<Door> doors = new();
	readonly List<Label3D> labels = new();
	readonly Dictionary<int, System.Numerics.Vector2> entries = new();
	Font? font;
	FloorLayout? layout;
	(int Number, bool Deep) shownFloor;
	int currentId = -1;
	HexCell spot;

	public DungeonWorld()
	{
		Grid = new HexGrid([], []);
		Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 13f, Current = false };
		Creature = new TrainingDummy { Title = "Criatura da névoa", RestingHint = "Escolha-a para lutar", RestingCompact = "Lutar [F]" };
	}

	public HexGrid Grid { get; }

	public Camera3D Camera { get; }

	public TrainingDummy Creature { get; }

	public IReadOnlyList<Door> Doors => doors;

	public FloorLayout? Layout => layout;

	public int CurrentRoom => currentId;

	// Com o que a criança pode interagir na Sala de agora: as portas trancadas (para ouvir por que), a escada e a fonte do Descanso.
	public IReadOnlyList<Interactable> Interactables(Dungeon.Expedition expedition)
	{
		var list = doors.Where(d => d.Locked).Select(d => new Interactable($"door:{d.Target}", new System.Numerics.Vector2(d.Ground.X, d.Ground.Z))).ToList();
		if (expedition.CanAscend) list.Add(new Interactable("stairs", StairsSpot));
		if (expedition.CanDescend) list.Add(new Interactable("stairs-down", StairsSpot));
		if (expedition.CanEnterDeep) list.Add(new Interactable("stairs-deep", DeepStairsSpot));
		if (expedition.CanReturnToProgression) list.Add(new Interactable("progression", FountainSpot));
		if (expedition.Current.Kind.Restores) list.Add(new Interactable("rest", FountainSpot));
		return list;
	}

	// A criatura fica no centro da Sala.
	public (Vector3 Ground, float Top) CreatureSpot => (Vector3.Zero, CreatureHeight);

	public bool CanStand(System.Numerics.Vector2 ground) => Grid.IsFree(ground, CreatureRadius);

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
		Frame(System.Numerics.Vector2.Zero, 13f);
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

	public void SetMeshyCreature(string discipline)
	{
		if (!MeshyAssets.Enabled) return;
		creatureModel?.QueueFree();
		creatureModel = new MeshyCreature(discipline == "matematica" ? "criaturas/automato-base" : "criaturas/livro-com-pernas") { Follow = Creature };
		AddChild(creatureModel);
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

	// Põe a câmera olhando `focus` (x, z) do chão, mostrando `size` unidades na altura da tela.
	public void Frame(System.Numerics.Vector2 focus, float size)
	{
		Camera.Size = size;
		Camera.LookAtFromPosition(new Vector3(focus.X + 12f, 14f, focus.Y + 12f), new Vector3(focus.X, 1f, focus.Y), Vector3.Up);
	}

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

	// Reconstrói a Sala de agora, os corredores e, atrás das portas abertas, as vizinhas; a porta de uma Sala nova não a revela.
	// Devolve quanto o chão andou desde a Sala anterior do mesmo Andar (zero na primeira vez).
	public System.Numerics.Vector2 Show(Floor floor, int roomId, IReadOnlySet<int> cleared, System.Func<int, (string Label, bool Locked, bool Known)> describe, string? note = null, bool stairsDown = false, bool deepStairs = false)
	{
		foreach (var child in room.GetChildren()) child.QueueFree();
		doors.Clear();
		labels.Clear();
		entries.Clear();

		layout = floor.Layout!;
		var here = layout.Room(roomId);
		var key = (floor.Number, floor.Deep);
		var shift = currentId >= 0 && shownFloor == key ? HexGrid.ToWorld(here.Spot - spot) : System.Numerics.Vector2.Zero;
		(shownFloor, currentId, spot) = (key, roomId, here.Spot);

		var kind = floor.Rooms[roomId].Kind;
		var walkable = new HashSet<HexCell>(here.Cells.Select(Local));
		var blocked = new HashSet<HexCell>();
		RoomMeshes.AddFloor(room, here.Cells.Select(c => (Local(c), here.HeightAt(c))), FloorMap.ColorOf(kind), cleared.Contains(roomId));

		foreach (var neighbor in floor.Neighbors(roomId).OrderBy(n => n))
		{
			var cells = layout.CorridorBetween(roomId, neighbor)!.Select(Local).ToList();
			var (label, locked, known) = describe(neighbor);
			var other = layout.Room(neighbor);
			var color = known ? FloorMap.ColorOf(floor.Rooms[neighbor].Kind) : Color.FromHtml("#8A8399");
			var door = new Door(neighbor, GroundOf(cells[0]), label, locked);
			doors.Add(door);
			var way = cells.Count > 1 ? GroundOf(cells[1]) - door.Ground : GroundOf(Local(other.Spot)) - door.Ground;
			AddDoor(door, color, new Vector3(way.X, 0f, way.Z));

			walkable.Add(cells[0]);
			if (locked)
			{
				blocked.Add(cells[0]);
				RoomMeshes.AddFloor(room, [(cells[0], 0f)], Color.FromHtml("#6B6578"), false);
				continue;
			}

			walkable.UnionWith(cells);
			if (!CorridorKit.Add(room, cells)) RoomMeshes.AddFloor(room, cells.Select(c => (c, 0f)), Color.FromHtml("#8A7FA6"), false);
			walkable.UnionWith(other.Cells.Select(Local));
			RoomMeshes.AddFloor(room, other.Cells.Select(c => (Local(c), other.HeightAt(c))), color, cleared.Contains(neighbor));
			var entry = GroundOf(Local(other.Cells.First(c => c.DistanceTo(layout.CorridorBetween(roomId, neighbor)![^1]) == 1)));
			entries[neighbor] = new System.Numerics.Vector2(entry.X, entry.Z);
		}
		Grid.Reset(walkable, blocked);

		AddSign($"{kind.Name}", new Vector3(0f, kind.HasChallenge ? 5.6f : 2.4f, 0f), 56);
		if (note is not null) AddSign(note, new Vector3(0f, 3.4f, 0f), 34);
		if (kind.Stairs == RoomStairs.Up || stairsDown) AddStairs(stairsDown);
		if (kind.Restores) AddFountain(cleared.Contains(roomId));
		if (deepStairs) AddDeepStairs();
		return shift;
	}

	HexCell Local(HexCell global) => global - spot;

	// Onde `ground` (no chão da Sala de agora) fica no espaço do Andar inteiro, que é o do mapa.
	public System.Numerics.Vector2 ToLayoutSpace(System.Numerics.Vector2 ground) => ground + HexGrid.ToWorld(spot);

	static Vector3 GroundOf(HexCell local)
	{
		var world = HexGrid.ToWorld(local);
		return new Vector3(world.X, 0f, world.Y);
	}

	// A porta da Sala `target`.
	public Door? DoorTo(int target) => doors.FirstOrDefault(d => d.Target == target);

	// Um ponto do chão dentro da Sala `target` (já aberta), colado ao fim do corredor: onde a criança pisa ao chegar nela.
	public System.Numerics.Vector2? EntryTo(int target) => entries.TryGetValue(target, out var at) ? at : null;

	// A Sala (que não é a de agora) em que o ponto do chão está, se estiver dentro de uma.
	public int? RoomAt(System.Numerics.Vector2 ground)
	{
		if (layout is null) return null;
		var found = layout.RoomAt(HexGrid.FromWorld(ground) + spot);
		return found?.Room;
	}

	// A altura do chão sob o ponto (só o desnível tem altura).
	public float HeightAt(System.Numerics.Vector2 ground)
	{
		var cell = HexGrid.FromWorld(ground) + spot;
		return layout?.RoomAt(cell) is { } placed ? placed.HeightAt(cell) : 0f;
	}

	// Os cantos da Sala de agora e das portas, no chão (x, z), para a câmera enquadrar tudo sem cortar nada.
	public IEnumerable<System.Numerics.Vector2> Bounds() =>
		layout!.Room(currentId).Cells.Select(c => HexGrid.ToWorld(Local(c))).Concat(doors.Select(d => new System.Numerics.Vector2(d.Ground.X, d.Ground.Z)));

	public System.Numerics.Vector2 CenterOfRoom()
	{
		var points = layout!.Room(currentId).Cells.Select(c => HexGrid.ToWorld(Local(c))).ToList();
		return new System.Numerics.Vector2(points.Average(p => p.X), points.Average(p => p.Y));
	}

	public static System.Numerics.Vector2 Center => System.Numerics.Vector2.Zero;

	// A escada do Andar fica no meio da Sala de saída (para subir) ou da entrada (para descer).
	public static System.Numerics.Vector2 StairsSpot => new(0f, 0f);

	// A escada lateral dos Andares profundos fica num hexágono ao lado do centro, fora do caminho da escada principal.
	public System.Numerics.Vector2 DeepStairsSpot => layout is null ? new(-2.4f, 1.4f) : HexGrid.ToWorld(Local(layout.Room(currentId).SideSpot));

	public static System.Numerics.Vector2 FountainSpot => System.Numerics.Vector2.Zero;

	// Duas colunas e uma verga; trancada, ganha um painel fechando a passagem. O texto diz o que é e se está trancada.
	void AddDoor(Door door, Color color, Vector3 way)
	{
		var tint = door.Locked ? Color.FromHtml("#6B6578") : color;
		var material = new StandardMaterial3D { AlbedoColor = tint, Roughness = 0.8f };
		var along = way.LengthSquared() > 0.0001f ? way.Normalized() : Vector3.Forward;
		var side = new Vector3(-along.Z, 0f, along.X) * 0.8f;
		if (MeshyAssets.Enabled) MeshyAssets.Add(room, "cenario/porta-arco", door.Ground, 1.9f, 3.0f, Mathf.Atan2(along.X, along.Z));
		foreach (var offset in MeshyAssets.Enabled ? System.Array.Empty<Vector3>() : new[] { side, -side })
			room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.28f, PillarHeight, 0.28f) }, MaterialOverride = material, Position = door.Ground + offset + new Vector3(0f, PillarHeight / 2f, 0f) });
		var across = Mathf.Atan2(side.X, side.Z) - Mathf.Pi / 2f;
		if (!MeshyAssets.Enabled) room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.9f, 0.26f, 0.3f) }, MaterialOverride = material, Position = door.Ground + new Vector3(0f, PillarHeight + 0.1f, 0f), Rotation = new Vector3(0f, across, 0f) });
		if (door.Locked)
			room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.5f, PillarHeight - 0.1f, 0.18f) }, MaterialOverride = material, Position = door.Ground + new Vector3(0f, PillarHeight / 2f, 0f), Rotation = new Vector3(0f, across, 0f) });
		AddSign(door.Label, door.Ground + new Vector3(0f, (MeshyAssets.Enabled ? 3.6f : PillarHeight + 0.95f), 0f), 40);
	}

	// O Descanso é uma fonte no centro da Sala; ela escurece depois de usada.
	void AddFountain(bool used)
	{
		if (MeshyAssets.Enabled)
		{
			MeshyAssets.Add(room, "cenario/fonte-descanso", Vector3.Zero, 1.8f, 1.7f);
			AddSign(used ? "Descanso usado" : "Descanso • fonte provisória", new Vector3(0f, 2f, 0f), 28);
			return;
		}
		var water = new StandardMaterial3D { AlbedoColor = used ? Color.FromHtml("#7E7A99") : Color.FromHtml("#BA9BEA"), Roughness = 0.4f };
		room.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.1f, BottomRadius = 1.2f, Height = 0.5f }, MaterialOverride = water, Position = new Vector3(0f, 0.25f, 0f) });
		room.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f }, MaterialOverride = water, Position = new Vector3(0f, 0.9f, 0f) });
	}

	void AddStairs(bool down)
	{
		if (MeshyAssets.Enabled)
		{
			MeshyAssets.Add(room, "cenario/escada-pedra", Vector3.Zero, 1.7f, 1.2f, down ? Mathf.Pi : 0f);
			if (down) AddSign("Escada para baixo", new Vector3(0f, 2.1f, 0f), 36);
			return;
		}
		var material = new StandardMaterial3D { AlbedoColor = FloorMap.ColorOf(RoomTypes.Exit), Roughness = 0.8f };
		if (down) AddSign("Escada para baixo", new Vector3(0f, 3.4f, 0f), 40);
		for (var step = 0; step < 4; step++)
			room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.8f - step * 0.3f, 0.25f, 1.8f - step * 0.3f) }, MaterialOverride = material, Position = new Vector3(0f, 0.15f + step * 0.25f, 0f) });
	}

	void AddDeepStairs()
	{
		var material = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#6F5BB0"), Roughness = 0.8f };
		var at = new Vector3(DeepStairsSpot.X, 0f, DeepStairsSpot.Y);
		for (var step = 0; step < 3; step++)
			room.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.2f - step * 0.3f, 0.2f, 1.2f - step * 0.3f) }, MaterialOverride = material, Position = at + new Vector3(0f, 0.1f + step * 0.2f, 0f) });
		AddSign("Escada lateral\nAndares profundos", at + new Vector3(0f, 1.9f, 0f), 36);
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
}
