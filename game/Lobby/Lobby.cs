using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// Cena do Lobby: ilha hexagonal flutuante, câmera isométrica e luz quente.
public partial class Lobby : Node3D, ICombatStage
{
	const int IslandRadius = 5;
	const float TileSize = 1f;         // do centro ao vértice; a grade de colisão (HexGrid) também assume 1
	const float TileThickness = 0.5f;
	const float TileGap = 0.98f;       // fresta fina entre os hexágonos
	const float PortalHeight = 2.8f;
	const float LecternHeight = 1.4f;
	const float GuideHeight = 1.8f;
	const float DummyHeight = 1.7f;
	const float TowerHeight = 5f;

	// O Mobile não desenha SSAO e avisa a cada vez que ele é ligado; só o Forward+ e o Compatibility o têm.
	static bool SsaoAvailable => RenderingServer.GetCurrentRenderingMethod() != "mobile";

	enum Kind { Grass = 0, Flowers = 1, Path = 2, Water = 3 }

	// Vizinhos em coordenadas axiais; a ordem é a mesma dos bits da máscara no shader.
	static readonly Vector2I[] Neighbors = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

	// Grupos de árvores (q, r); ficam fora do caminho central.
	static readonly Vector2I[] TreeSpots = [new(-3, 1), new(-2, 3), new(3, -1), new(2, 2), new(-4, 0), new(1, -4), new(4, -3)];

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
		Grid = new HexGrid(tiles.Keys.Select(c => new HexCell(c.X, c.Y)), BlockedCells(), IsLote02PointFree, CanCrossLote02);
		AddEnvironment();
		AddCamera();
		AddDepthOfField();
		AddTerrain();
		AddRock();
		AddLote02Details();
		AddTrees();
		AddObjects();
		AddPlaces();
		AddConceptWaterfall();
	}

	// Hexágono "flat-top": vértices à esquerda e à direita, como nos mockups.
	public static Vector3 HexToWorld(int q, int r) =>
		new(HexGrid.ToWorld(new HexCell(q, r)).X, 0f, HexGrid.ToWorld(new HexCell(q, r)).Y);

	void BuildMap()
	{
		for (var q = -IslandRadius; q <= IslandRadius; q++)
			for (var r = -IslandRadius; r <= IslandRadius; r++)
			{
				if (Mathf.Abs(q + r) > IslandRadius) continue;
				var onPath = (r == 0 && Mathf.Abs(q) <= 3) || (q == 0 && Mathf.Abs(r) <= 3) || (q == 2 && (r == -3 || r == -1));
				var onPond = (q == 3 && r == -2) || (q == 3 && r == -3) || (q == 2 && r == -2) || (r == -2 && (q == 4 || q == 5));
				var flowery = Hash01(q * 31 + r * 17) < 0.2f;
				tiles[new Vector2I(q, r)] = onPond ? Kind.Water : onPath ? Kind.Path : flowery ? Kind.Flowers : Kind.Grass;
			}
	}

	IEnumerable<HexCell> BlockedCells() =>
		tiles.Where(t => t.Value == Kind.Water && new HexCell(t.Key.X, t.Key.Y) != BridgeCell).Select(t => t.Key).Concat(TreeSpots).Select(c => new HexCell(c.X, c.Y))
			.Concat(Rules.Objects.Select(o => new HexCell(o.Q, o.R)));

	// Luz quente e sombras suaves, como pede a direção de arte. O brilho (glow) só pega o que passa do limite de luz, ou seja, só os
	// itens mágicos que têm emissão (o raio do ataque, por exemplo), e nunca os números nem o pontilhado, que são interface 2D.
	// O SSAO está ligado nos dados do ambiente, mas só o renderizador Forward+ o desenha; no Mobile (o do jogo) ele é ignorado.
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
		AddChild(new DirectionalLight3D
		{
			LightColor = Color.FromHtml("#FFE4BC"),
			LightEnergy = 1.2f,
			ShadowEnabled = true,
			ShadowBlur = 2f,
			RotationDegrees = new Vector3(-50f, -30f, 0f),
		});
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

	// Uma malha só para todos os tiles. O shader desenha grama, flores, água e os caminhos,
	// e cada tile sabe quais vizinhos também são caminho, para os braços se encontrarem na aresta.
	void AddTerrain()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var (cell, kind) in tiles)
		{
			var center = HexToWorld(cell.X, cell.Y);
			if (kind != Kind.Water) { AddLote02Tile(center, PathMask(cell)); continue; }
			center.Y = -.24f;
			var info = new Color((int)kind / 3f, PathMask(cell) / 63f, Hash01(cell.X * 7 + cell.Y * 13), 1f);
			var corners = new Vector3[6];
			for (var i = 0; i < 6; i++)
			{
				var angle = Mathf.DegToRad(60f * i);
				corners[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * TileSize * TileGap;
			}
			for (var i = 0; i < 6; i++)
			{
				var a = corners[i];
				var b = corners[(i + 1) % 6];
				Vertex(st, center, Vector3.Up, info, Vector2.Zero);
				Vertex(st, a, Vector3.Up, info, new Vector2(a.X - center.X, a.Z - center.Z) / (TileSize * TileGap));
				Vertex(st, b, Vector3.Up, info, new Vector2(b.X - center.X, b.Z - center.Z) / (TileSize * TileGap));

				var side = new Color(info.R, info.G, info.B, 0f);
				var outward = ((a + b) / 2f - center).Normalized();
				var down = new Vector3(0f, -TileThickness, 0f);
				Vertex(st, a, outward, side, new Vector2(0f, 0f));
				Vertex(st, a + down, outward, side, new Vector2(0f, 1f));
				Vertex(st, b, outward, side, new Vector2(0f, 0f));
				Vertex(st, b, outward, side, new Vector2(0f, 0f));
				Vertex(st, a + down, outward, side, new Vector2(0f, 1f));
				Vertex(st, b + down, outward, side, new Vector2(0f, 1f));
			}
		}
		var shader = GD.Load<Shader>("res://Lobby/terrain.gdshader");
		AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = new ShaderMaterial { Shader = shader } });
	}

	static void Vertex(SurfaceTool st, Vector3 position, Vector3 normal, Color color, Vector2 uv)
	{
		st.SetNormal(normal);
		st.SetColor(color);
		st.SetUV(uv);
		st.AddVertex(position);
	}

	int PathMask(Vector2I cell)
	{
		if (tiles[cell] != Kind.Path) return 0;
		var mask = 0;
		for (var i = 0; i < Neighbors.Length; i++)
			if (tiles.TryGetValue(cell + Neighbors[i], out var other) && (other == Kind.Path || new HexCell(cell.X + Neighbors[i].X, cell.Y + Neighbors[i].Y) == BridgeCell)) mask |= 1 << i;
		return mask;
	}

	// Base de rocha: um único bloco que afina para baixo, com contorno hexagonal e a superfície
	// irregular, no lugar de uma coluna por tile.
	void AddRockLegacy()
	{
		const int Steps = 48;
		const int Rings = 6;
		const float Depth = 4.4f;
		var apothem = (1.5f * IslandRadius + 0.85f) * TileSize;
		var top = Color.FromHtml("#A99A8C");
		var bottom = Color.FromHtml("#5E5870");

		Vector3 Point(int ring, int step)
		{
			var t = ring / (float)Rings;
			var theta = Mathf.Tau * step / Steps;
			var delta = Mathf.PosMod(theta + Mathf.Pi / 6f, Mathf.Pi / 3f) - Mathf.Pi / 6f;
			// Lateral quase vertical na parte que a câmera enxerga e afunila só mais embaixo.
			var taper = t < 0.6f ? Mathf.Lerp(0.99f, 0.9f, t / 0.6f) : Mathf.Lerp(0.9f, 0.2f, (t - 0.6f) / 0.4f);
			var radius = apothem / Mathf.Cos(delta) * taper;
			radius *= 1f + 0.1f * t * (Hash01(step * 13 + ring * 101) - 0.5f) * 2f;
			var y = -TileThickness - Depth * t + 0.25f * (Hash01(step * 29 + ring * 53) - 0.5f) * t;
			return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
		}

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		for (var ring = 0; ring < Rings; ring++)
			for (var step = 0; step < Steps; step++)
			{
				var next = (step + 1) % Steps;
				var p00 = Point(ring, step);
				var p01 = Point(ring, next);
				var p10 = Point(ring + 1, step);
				var p11 = Point(ring + 1, next);
				var c0 = top.Lerp(bottom, ring / (float)Rings);
				var c1 = top.Lerp(bottom, (ring + 1) / (float)Rings);
				Triangle(st, p00, p10, p01, c0, c1, c0);
				Triangle(st, p01, p10, p11, c0, c1, c1);
			}
		var tip = new Vector3(0f, -TileThickness - Depth - 0.4f, 0f);
		for (var step = 0; step < Steps; step++)
			Triangle(st, Point(Rings, step), tip, Point(Rings, (step + 1) % Steps), bottom, bottom, bottom);

		st.GenerateNormals();
		var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
		var rockMesh = st.Commit();
		AddChild(new MeshInstance3D { Mesh = rockMesh, MaterialOverride = material });
	}

	static void Triangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
	{
		st.SetColor(ca); st.AddVertex(a);
		st.SetColor(cb); st.AddVertex(b);
		st.SetColor(cc); st.AddVertex(c);
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
			else if (item.Id.StartsWith("torre-")) top = AddGate(ground, item.Name, "#D9A64A");
			else if (item.Id == "mestre") top = AddGate(ground, item.Name, "#7A44D1");
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

	// A entrada de uma torre na beira da ilha: duas colunas, uma verga e o nome. Greybox, até a arte definitiva.
	float AddGate(Vector3 ground, string name, string color)
	{
		const float height = 2.4f;
		var material = new StandardMaterial3D { AlbedoColor = Color.FromHtml(color), Roughness = 0.8f };
		var way = new Vector3(-ground.X, 0f, -ground.Z).Normalized();
		var side = new Vector3(-way.Z, 0f, way.X) * 0.8f;
		foreach (var offset in new[] { side, -side })
			AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.35f, height, 0.35f) }, MaterialOverride = material, Position = ground + offset + new Vector3(0f, height / 2f, 0f) });
		AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.1f, 0.35f, 0.4f) }, MaterialOverride = material, Position = ground + new Vector3(0f, height + 0.1f, 0f), Rotation = new Vector3(0f, Mathf.Atan2(side.X, side.Z) - Mathf.Pi / 2f, 0f) });
		var sign = new Label3D { Text = name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 44, PixelSize = 0.012f, OutlineSize = 14, OutlineModulate = Color.FromHtml("#1B1424"), Modulate = Color.FromHtml("#FFF6E0"), NoDepthTest = true, Position = ground + new Vector3(0f, height + 0.9f, 0f) };
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
			AddIslet(center, landmark ? 3.0f : 2.3f);
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
				else top = FitToHeight(model, TowerHeight, center + new Vector3(0f, 0.2f, 0f));
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

	void AddIsletLegacy(Vector3 center)
	{
		var grass = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 2.3f, BottomRadius = 2.3f, Height = 0.4f, RadialSegments = 6, Rings = 0 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#5DB84A"), Roughness = 1f },
			Position = center + new Vector3(0f, -0.2f, 0f),
		};
		var rock = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 2.1f, BottomRadius = 0.5f, Height = 2.8f, RadialSegments = 6, Rings = 0 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#A99A8C"), Roughness = 1f },
			Position = center + new Vector3(0f, -1.6f, 0f),
		};
		AddChild(grass);
		AddChild(rock);
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

	// Mostra o indicador só no objeto ao alcance (ou em nenhum, com null).
	public void Highlight(string? objectId)
	{
		foreach (var (id, marker) in markers) marker.Visible = id == objectId;
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
