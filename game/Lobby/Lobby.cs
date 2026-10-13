using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// Cena do Lobby: ilha hexagonal flutuante, câmera isométrica e luz quente.
public partial class Lobby : Node3D
{
	const int IslandRadius = 5;
	const float TileSize = 1f;         // do centro ao vértice
	const float TileThickness = 0.5f;
	const float TileGap = 0.98f;       // fresta fina entre os hexágonos

	enum Kind { Grass = 0, Flowers = 1, Path = 2, Water = 3 }

	// Vizinhos em coordenadas axiais; a ordem é a mesma dos bits da máscara no shader.
	static readonly Vector2I[] Neighbors = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

	// Grupos de árvores (q, r); ficam fora do caminho central.
	static readonly Vector2I[] TreeSpots = [new(-3, 1), new(-2, 3), new(3, -1), new(2, 2), new(-4, 0), new(1, -4), new(4, -3)];

	readonly Dictionary<Vector2I, Kind> tiles = new();

	// O chão que o Personagem pode pisar: a ilha, menos a lagoa e as árvores.
	public HexGrid Grid { get; private set; } = null!;
	public Camera3D Camera { get; private set; } = null!;

	public override void _Ready()
	{
		BuildMap();
		Grid = new HexGrid(tiles.Keys.Select(c => new HexCell(c.X, c.Y)), BlockedCells());
		AddEnvironment();
		AddCamera();
		AddTerrain();
		AddRock();
		AddTrees();
	}

	// Hexágono "flat-top": vértices à esquerda e à direita, como nos mockups.
	public static Vector3 HexToWorld(int q, int r) =>
		new(TileSize * 1.5f * q, 0f, TileSize * Mathf.Sqrt(3f) * (r + q / 2f));

	void BuildMap()
	{
		for (var q = -IslandRadius; q <= IslandRadius; q++)
			for (var r = -IslandRadius; r <= IslandRadius; r++)
			{
				if (Mathf.Abs(q + r) > IslandRadius) continue;
				var onPath = (r == 0 && Mathf.Abs(q) <= 3) || (q == 0 && Mathf.Abs(r) <= 3);
				var onPond = (q == 3 && r == -2) || (q == 3 && r == -3) || (q == 2 && r == -2);
				var flowery = Hash01(q * 31 + r * 17) < 0.2f;
				tiles[new Vector2I(q, r)] = onPond ? Kind.Water : onPath ? Kind.Path : flowery ? Kind.Flowers : Kind.Grass;
			}
	}

	IEnumerable<HexCell> BlockedCells() =>
		tiles.Where(t => t.Value == Kind.Water).Select(t => t.Key).Concat(TreeSpots).Select(c => new HexCell(c.X, c.Y));

	void AddEnvironment()
	{
		AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = Color.FromHtml("#A8D1F5"),
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = Color.FromHtml("#CFE0FF"),
				AmbientLightEnergy = 0.7f,
			},
		});
		AddChild(new DirectionalLight3D
		{
			LightColor = Color.FromHtml("#FFF1D6"),
			LightEnergy = 1.1f,
			ShadowEnabled = true,
			RotationDegrees = new Vector3(-50f, -30f, 0f),
		});
	}

	// Ortográfica e isométrica de verdade: a câmera olha a origem a 35,264 graus de elevação.
	void AddCamera()
	{
		Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 18f, Current = true };
		AddChild(Camera);
		Camera.LookAtFromPosition(new Vector3(12f, 12f, 12f), Vector3.Zero, Vector3.Up);
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
			if (tiles.TryGetValue(cell + Neighbors[i], out var other) && other == Kind.Path) mask |= 1 << i;
		return mask;
	}

	// Base de rocha: um único bloco que afina para baixo, com contorno hexagonal e a superfície
	// irregular, no lugar de uma coluna por tile.
	void AddRock()
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

	static float Hash01(int n)
	{
		var x = Mathf.Sin(n * 12.9898f) * 43758.5453f;
		return x - Mathf.Floor(x);
	}
}
