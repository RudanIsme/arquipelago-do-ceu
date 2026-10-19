using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// Cena do Lobby: ilha hexagonal flutuante, câmera isométrica e luz quente.
public partial class Lobby : Node3D, ICombatStage
{
	const float TileSize = 1f;         // do centro ao vértice; a grade de colisão (HexGrid) também assume 1
	const float TileThickness = 0.5f;
	const float TileGap = 0.98f;       // fresta fina entre os hexágonos
	const float PortalHeight = 2.8f;
	// A seta de interação de uma torre fica perto da base dela, e não lá em cima.
	const float TowerBaseHeight = 1.2f;
	const float LecternHeight = 1.4f;
	const float GuideHeight = 1.8f;
	const float DummyHeight = 1.7f;
	const float TowerHeight = 5f;

	// O Mobile não desenha SSAO e avisa a cada vez que ele é ligado; só o Forward+ e o Compatibility o têm.
	static bool SsaoAvailable => RenderingServer.GetCurrentRenderingMethod() != "mobile";

	enum Kind { Grass = 0, Flowers = 1, Path = 2, Water = 3, Plank = 4 }

	// Vizinhos em coordenadas axiais; a ordem é a mesma dos bits da máscara no shader.
	static readonly Vector2I[] Neighbors = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

	// Grupos de árvores (q, r): os pontos do desenho em zonas, longe do caminho, da lagoa e das praças.
	IEnumerable<Vector2I> TreeSpots => plan.Trees.Select(c => new Vector2I(c.Q, c.R));

	readonly Dictionary<Vector2I, Kind> tiles = new();

	// O chão que o Personagem pode pisar: a ilha, menos a lagoa e as árvores.
	public HexGrid Grid { get; private set; } = null!;
	public Camera3D Camera { get; private set; } = null!;

	// Lugares, objetos e alcance vêm dos dados; quem instancia o Lobby define isto antes de adicioná-lo à cena.
	public LobbyRules Rules { get; set; } = null!;

	public IReadOnlyList<Interactable> Interactables { get; private set; } = [];

	// Sem animação o indicador do objeto fica parado.
	public bool ReduceMotion { get; set; }

	readonly List<Label3D> signs = new();
	readonly Dictionary<string, Node3D> markers = new();
	readonly Dictionary<string, (Vector3 Ground, float Top)> footprints = new();
	double clock;
	bool reducedEffects;
	Godot.Environment? environment;
	CameraAttributesPractical? attributes;

	public override void _Ready()
	{
		BuildMap();
		Grid = new HexGrid(tiles.Keys.Select(c => new HexCell(c.X, c.Y)), BlockedCells());
		AddEnvironment();
		AddCamera();
		AddDepthOfField();
		AddTerrain();
		AddIslandKit();
		AddBridges();
		AddLote02Details();
		AddTrees();
		AddObjects();
		AddPlaces();
		AddConceptWaterfall();
		AddLamps();
	}

	// Hexágono "flat-top": vértices à esquerda e à direita, como nos mockups.
	public static Vector3 HexToWorld(int q, int r) =>
		new(HexGrid.ToWorld(new HexCell(q, r)).X, 0f, HexGrid.ToWorld(new HexCell(q, r)).Y);

	void BuildMap() => BuildIslands();

	IEnumerable<HexCell> BlockedCells() =>
		tiles.Where(t => t.Value == Kind.Water).Select(t => t.Key).Concat(TreeSpots).Select(c => new HexCell(c.X, c.Y))
			.Concat(Rules.Objects.Select(o => new HexCell(o.Q, o.R))).Concat(IslandCenters());

	// Luz quente e sombras suaves (direção de arte). O glow só pega o que emite luz, nunca números nem pontilhado (interface 2D).
	// O SSAO está nos dados, mas só o Forward+ o desenha; o Mobile, que é o do jogo, o ignora.
	void AddEnvironment()
	{
		environment = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Color.FromHtml("#A8D1F5"),
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = Color.FromHtml("#D6E2FF"),
			AmbientLightEnergy = 0.7f,
			SsaoEnabled = SsaoAvailable,
			SsaoRadius = 1.2f,
			SsaoIntensity = 1.4f,
			GlowEnabled = true,
			GlowHdrThreshold = 1.1f,
			GlowIntensity = 0.7f,
			GlowBloom = 0f,
		};
		AddChild(new WorldEnvironment { Environment = environment });
		sun = new DirectionalLight3D
		{
			LightColor = Color.FromHtml("#FFE4BC"),
			LightEnergy = 1.2f,
			ShadowEnabled = true,
			ShadowBlur = 2f,
			RotationDegrees = new Vector3(-50f, -30f, 0f),
		};
		AddChild(sun);
	}

	// Desfoque leve das bordas (efeito maquete): só o que está bem longe da câmera, para os números e as criaturas ficarem nítidos.
	void AddDepthOfField()
	{
		attributes = new CameraAttributesPractical
		{
			DofBlurFarEnabled = true,
			DofBlurFarDistance = 60f,
			DofBlurFarTransition = 20f,
			DofBlurAmount = 0.01f,
		};
		Camera.Attributes = attributes;
	}

	// Opção de acessibilidade: desliga o desfoque, o brilho e o SSAO.
	public void SetEffects(bool reduced)
	{
		reducedEffects = reduced;
		if (environment is null || attributes is null) return;
		environment.GlowEnabled = !reduced;
		environment.SsaoEnabled = SsaoAvailable && !reduced;
		attributes.DofBlurFarEnabled = !reduced;
	}

	// Ortográfica e isométrica de verdade: a câmera olha a origem a 35,264 graus de elevação.
	void AddCamera()
	{
		Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 22f, Current = true };
		AddChild(Camera);
		Camera.LookAtFromPosition(new Vector3(12f, 14f, 12f), new Vector3(0f, 2f, 0f), Vector3.Up);
	}

	void AddTrees()
	{
		var scene = GD.Load<PackedScene>("res://assets/modelos/arvores.glb");
		if (scene is null)
		{
			GD.PushError("Não foi possível carregar o modelo das árvores.");
			return;
		}
		foreach (var spot in TreeSpots)
		{
			var tree = scene.Instantiate<Node3D>();
			AddChild(tree);
			tree.Scale = Vector3.One * 1.4f;
			PlaceOnGround(tree, HexToWorld(spot.X, spot.Y));
		}
	}

	// Os modelos do Meshy têm a origem no centro da malha. Aqui a base da caixa vai para o chão.
	static void PlaceOnGround(Node3D model, Vector3 ground)
	{
		var lowest = float.MaxValue;
		foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			var box = mesh.GlobalTransform * mesh.GetAabb();
			lowest = Mathf.Min(lowest, box.Position.Y);
		}
		model.Position = lowest == float.MaxValue ? ground : ground - new Vector3(0f, lowest, 0f);
	}

	// Objetos com que a criança interage: o modelo, os hexágonos bloqueados (em BlockedCells) e um indicador
	// em forma de seta, que só aparece quando o Personagem está ao alcance (a cor nunca é a única pista).
	void AddObjects()
	{
		var list = new List<Interactable>();
		foreach (var item in Rules.Objects)
		{
			var ground = HexToWorld(item.Q, item.R);
			// O Boneco é uma criatura: escolhida com clique ou tecla F, e não usada pelo botão de interação.
			if (item.Id != "boneco") list.Add(new Interactable(item.Id, new System.Numerics.Vector2(ground.X, ground.Z)));
			var height = item.Id switch { "portal" => PortalHeight, "guia" => GuideHeight, "boneco" => DummyHeight, _ => LecternHeight };
			var top = height;
			if (LoadModel(item.Id == "guia" ? "guia-reconstruido-v05" : item.Model) is { } model)
			{
				AddChild(model);
				top = FitToHeight(model, height, ground);
				if (item.Id == "portal")
					model.AddChild(new LobbyAmbience(model, true, () => ReduceMotion, () => reducedEffects));
				else if (item.Id == "guia")
					model.AddChild(new GuideIdle(model, () => ReduceMotion));
			}
			else if (item.Id.StartsWith("torre-")) top = TowerBaseHeight;
			else if (item.Id == "mestre")
			{
				top = AddHall(ground, item.Name);
				if (MeshyAssets.Enabled) MeshyAssets.Add(this, "cenario/pedestal-mestre", ground, 3f, 0.6f);
			}
			else if (item.Id == "mercador") top = AddStall(ground, item.Name);
			var marker = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0f, Height = 0.55f, RadialSegments = 4, Rings = 0 },
				MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#F2B632"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
				Position = ground + new Vector3(0f, top + 0.7f, 0f),
				Visible = false,
			};
			AddChild(marker);
			markers[item.Id] = marker;
			footprints[item.Id] = (ground, top);
		}
		Interactables = list;
	}

	// A barraca do Mercador: balcão, dois postes e um toldo. Greybox, até a arte definitiva.
	float AddStall(Vector3 ground, string name)
	{
		const float height = 2.0f;
		var wood = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#8A5A32"), Roughness = 0.9f };
		var cloth = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#D9534F"), Roughness = 0.8f };
		AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.8f, 0.7f) }, MaterialOverride = wood, Position = ground + new Vector3(0f, 0.4f, 0f) });
		foreach (var side in new[] { -0.75f, 0.75f })
			AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, height, 0.12f) }, MaterialOverride = wood, Position = ground + new Vector3(side, height / 2f, -0.3f) });
		AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.9f, 0.1f, 1.1f) }, MaterialOverride = cloth, Position = ground + new Vector3(0f, height, -0.1f), Rotation = new Vector3(0.18f, 0f, 0f) });
		var sign = new Label3D { Text = name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 44, PixelSize = 0.012f, OutlineSize = 14, OutlineModulate = Color.FromHtml("#1B1424"), Modulate = Color.FromHtml("#FFF6E0"), NoDepthTest = true, Position = ground + new Vector3(0f, height + 0.7f, 0f) };
		AddChild(sign);
		signs.Add(sign);
		sign.AddToGroup(CameraController.LabelGroup);
		return height;
	}

	// Ilhas e torres ao redor, cada uma com a placa; torre sem modelo vira uma torre simples em greybox.
	void AddPlaces()
	{
		foreach (var place in Rules.Places)
		{
			var center = new Vector3(place.X, 0f, place.Z);
			bool landmark = place.Id is "ciencias" or "historia" or "geografia";
			if (!map.Islands.ContainsKey(place.Id)) AddKitIslet(center, landmark ? 2 : 1);
			var top = 5.2f;
			if (LoadModel(place.Model) is { } model)
			{
				AddChild(model);
				// A fachada deste GLB aponta para +Z; orienta a porta ao centro do Lobby.
				if (place.Id == "matematica") model.Rotation = new Vector3(0, Mathf.Atan2(-center.X, -center.Z), 0);
				if (landmark)
				{
					PrepareLandmark(model, place.Id);
					var bounds = WorldBox(model);
					float desired = place.Id == "historia" ? 3.2f : 3.5f;
					// Mede antes de girar: a caixa alinhada ao mundo cresce na diagonal, mas a ilha não muda.
					float height = Mathf.Min(desired, bounds.Size.Y * 4.9f / Mathf.Max(bounds.Size.X, bounds.Size.Z));
					float facing = place.Id == "historia" ? Mathf.Atan2(center.X, center.Z) : Mathf.Atan2(-center.X, -center.Z);
					model.Rotation = new Vector3(0, facing, 0);
					top = FitToHeight(model, height, center + new Vector3(0f, .10f, 0f));
				}
				else
				{
					top = FitToHeight(model, TowerHeight, center + new Vector3(0f, 0.2f, 0f));
					pickable[$"torre-{place.Id}"] = model;
				}
			}
			else AddGreyboxTower(center + new Vector3(0f, 0.2f, 0f));

			var sign = new Label3D
			{
				Text = $"{place.Name}\n{place.Sign}",
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FontSize = 56,
				PixelSize = 0.012f,
				OutlineSize = 16,
				OutlineModulate = Color.FromHtml("#1B1424"),
				Modulate = Color.FromHtml("#FFF6E0"),
				NoDepthTest = true,
				Position = center + new Vector3(0f, top + 1.1f, 0f),
			};
			AddChild(sign);
			signs.Add(sign);
			sign.SetMeta("future_place", place.Id != "matematica" && place.Id != "portugues");
			sign.AddToGroup(CameraController.LabelGroup);
		}
	}

	void AddGreyboxTower(Vector3 ground)
	{
		var body = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 1.0f, Height = 3.4f, RadialSegments = 8, Rings = 0 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#D8CFC0"), Roughness = 1f },
			Position = ground + new Vector3(0f, 1.7f, 0f),
		};
		var roof = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 1.2f, Height = 1.8f, RadialSegments = 8, Rings = 0 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#7A44D1"), Roughness = 1f },
			Position = ground + new Vector3(0f, 4.3f, 0f),
		};
		AddChild(body);
		AddChild(roof);
	}

	internal static Node3D? LoadModel(string? name)
	{
		if (name is null) return null;
		var path = $"res://assets/modelos/{name}.glb";
		var scene = GD.Load<PackedScene>(path);
		if (scene is null) GD.PushError($"Não foi possível carregar o modelo {path}.");
		return scene?.Instantiate<Node3D>();
	}

	// Escala o modelo para a altura do jogo e assenta a base no chão; devolve a altura do topo no mundo.
	internal static float FitToHeight(Node3D model, float height, Vector3 ground)
	{
		var box = WorldBox(model);
		if (box.Size.Y > 0.0001f) model.Scale = Vector3.One * (height / box.Size.Y);
		PlaceOnGround(model, ground);
		return ground.Y + height;
	}

	static Aabb WorldBox(Node3D model)
	{
		Aabb? merged = null;
		foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			var box = mesh.GlobalTransform * mesh.GetAabb();
			merged = merged is null ? box : merged.Value.Merge(box);
		}
		return merged ?? new Aabb();
	}

	// Onde fica o pé e a altura do topo de um objeto do Lobby, para efeitos que partem dele ou chegam nele.
	public (Vector3 Ground, float Top) FootprintOf(string objectId) => footprints[objectId];

	// No Lobby a criatura é o Boneco de treino.
	public (Vector3 Ground, float Top) CreatureSpot => footprints["boneco"];

	public bool CanStand(System.Numerics.Vector2 ground) => Grid.IsFree(ground, 0.35f);

	// Mostra o indicador só no objeto ao alcance (ou em nenhum, com null).
	public void Highlight(string? objectId)
	{
		foreach (var (id, marker) in markers) marker.Visible = id == objectId;
		GlowOnly(objectId);
	}

	public void SetCompactSigns(bool compact)
	{
		foreach (var sign in signs) sign.Visible = !compact || !sign.GetMeta("future_place", false).AsBool();
	}

	public void SetFont(Font? font)
	{
		foreach (var sign in signs) sign.Font = font;
	}

	public override void _Process(double delta)
	{
		clock += delta;
		TickLandmarks(delta);
		TickDay();
		var bob = ReduceMotion ? 0f : Mathf.Sin((float)clock * 4f) * 0.15f;
		foreach (var marker in markers.Values)
			if (marker.Visible) marker.Position = new Vector3(marker.Position.X, MarkerBaseY(marker) + bob, marker.Position.Z);
	}

	readonly Dictionary<Node3D, float> markerBase = new();

	float MarkerBaseY(Node3D marker)
	{
		if (!markerBase.TryGetValue(marker, out var y)) markerBase[marker] = y = marker.Position.Y;
		return y;
	}

	static float Hash01(int n)
	{
		var x = Mathf.Sin(n * 12.9898f) * 43758.5453f;
		return x - Mathf.Floor(x);
	}
}
