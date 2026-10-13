using Godot;

namespace ArquipelagoDoCeu;

// Cena do Lobby em greybox: ilha hexagonal flutuante, câmera isométrica e luz quente.
public partial class Lobby : Node3D
{
	const int IslandRadius = 5;
	const float TileSize = 1f;         // do centro ao vértice
	const float TileThickness = 0.5f;
	const float TileGap = 0.97f;       // deixa uma fresta fina entre os hexágonos

	static readonly Color Grass = Color.FromHtml("#62B83A");
	static readonly Color GrassShade = Color.FromHtml("#4FA235");
	static readonly Color Dirt = Color.FromHtml("#C98A48");
	static readonly Color Water = Color.FromHtml("#34A6E8");
	static readonly Color Rock = Color.FromHtml("#9A8F8A");

	// Posições (q, r) dos grupos de árvores; ficam fora do caminho central.
	static readonly Vector2I[] TreeSpots = [new(-3, 1), new(-2, 3), new(3, -1), new(2, 2), new(-4, 0), new(1, -4), new(4, -3)];

	public override void _Ready()
	{
		AddEnvironment();
		AddCamera();
		AddIsland();
		AddTrees();
	}

	// Hexágono "flat-top": vértices à esquerda e à direita, como nos mockups.
	public static Vector3 HexToWorld(int q, int r) =>
		new(TileSize * 1.5f * q, 0f, TileSize * Mathf.Sqrt(3f) * (r + q / 2f));

	void AddEnvironment()
	{
		var environment = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Color.FromHtml("#A8D1F5"),
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = Color.FromHtml("#CFE0FF"),
			AmbientLightEnergy = 0.65f,
		};
		AddChild(new WorldEnvironment { Environment = environment });

		AddChild(new DirectionalLight3D
		{
			LightColor = Color.FromHtml("#FFF1D6"),
			LightEnergy = 1.15f,
			ShadowEnabled = true,
			RotationDegrees = new Vector3(-50f, -30f, 0f),
		});
	}

	// Ortográfica e isométrica de verdade: a câmera olha a origem a 35,264 graus de elevação.
	void AddCamera()
	{
		var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 18f, Current = true };
		AddChild(camera);
		camera.LookAtFromPosition(new Vector3(12f, 12f, 12f), Vector3.Zero, Vector3.Up);
	}

	// Um MultiMesh para todos os topos e outro para as bases de rocha: dois desenhos, qualquer que seja o tamanho da ilha.
	void AddIsland()
	{
		var tiles = new System.Collections.Generic.List<Vector2I>();
		for (var q = -IslandRadius; q <= IslandRadius; q++)
			for (var r = -IslandRadius; r <= IslandRadius; r++)
				if (Mathf.Abs(q + r) <= IslandRadius) tiles.Add(new Vector2I(q, r));

		// CylinderMesh com 6 segmentos é um prisma hexagonal; o giro de 30 graus põe os vértices nos lados.
		var top = new MultiMeshInstance3D
		{
			Multimesh = BuildMultiMesh(tiles.Count, new CylinderMesh
			{
				TopRadius = TileSize * TileGap, BottomRadius = TileSize * TileGap, Height = TileThickness, RadialSegments = 6, Rings = 1,
			}),
		};
		var below = new MultiMeshInstance3D
		{
			Multimesh = BuildMultiMesh(tiles.Count, new CylinderMesh
			{
				TopRadius = TileSize * TileGap, BottomRadius = TileSize * 0.62f, Height = 1.8f, RadialSegments = 6, Rings = 1, CapTop = false,
			}),
		};

		for (var i = 0; i < tiles.Count; i++)
		{
			var (q, r) = (tiles[i].X, tiles[i].Y);
			var pos = HexToWorld(q, r);
			var turn = new Basis(Vector3.Up, Mathf.DegToRad(30f));
			top.Multimesh.SetInstanceTransform(i, new Transform3D(turn, pos + new Vector3(0f, -TileThickness / 2f, 0f)));
			top.Multimesh.SetInstanceColor(i, TileColor(q, r));
			below.Multimesh.SetInstanceTransform(i, new Transform3D(turn, pos + new Vector3(0f, -TileThickness - 0.9f, 0f)));
			below.Multimesh.SetInstanceColor(i, Rock);
		}
		AddChild(top);
		AddChild(below);
	}

	static MultiMesh BuildMultiMesh(int count, Mesh mesh)
	{
		mesh.SurfaceSetMaterial(0, new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f });
		return new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = count };
	}

	static Color TileColor(int q, int r)
	{
		var onPath = (r == 0 && Mathf.Abs(q) <= 3) || (q == 0 && Mathf.Abs(r) <= 3);
		var onPond = (q == 3 && r == -2) || (q == 3 && r == -3) || (q == 2 && r == -2);
		if (onPond) return Water;
		if (onPath) return Dirt;
		return (q + r) % 2 == 0 ? Grass : GrassShade;
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
			tree.Position = HexToWorld(spot.X, spot.Y);
			tree.Scale = Vector3.One * 1.4f;
		}
	}
}
