using System;
using System.Collections.Generic;
using Godot;

namespace ArquipelagoDoCeu;

/// <summary>Small optional concept accents, wholly inside already blocked tree cells.</summary>
public partial class ConceptAmbience : Node3D
{
	readonly Lobby lobby;
	readonly Func<bool> reduceMotion;
	readonly Func<bool> reduceEffects;
	readonly Dictionary<string, Node3D> groups = new();
	readonly List<(Node3D Node, Vector3 BasePosition, float Phase, bool Floating)> moving = new();
	readonly List<StandardMaterial3D> luminous = new();
	readonly List<(Node3D Node, Vector3 Rotation, float Phase, float Amplitude)> wind = new();
	string variant = "minimal";
	double clock;
	bool? lastReducedEffects;
	public int AccentMeshCount { get; private set; }
	public int AccentTriangleCount { get; private set; }

	public ConceptAmbience(Lobby lobby, Func<bool> reduceMotion, Func<bool> reduceEffects)
	{
		this.lobby = lobby;
		this.reduceMotion = reduceMotion;
		this.reduceEffects = reduceEffects;
		Name = "ConceptAmbience";
	}

	public override void _Ready()
	{
		// These are existing TreeSpots, not new obstacles. The complete objects fit within
		// their blocked hexagon (offset .48 m, footprint radius at most .24 m).
		Vector2I[] spots = [new(-4, 0), new(-2, 3), new(1, -4), new(4, -3)];
		var gold = Material("#D5AE62");
		var parchment = Material("#F3E3B8");
		var green = Material("#527F65");
		var purple = Material("#7754AE");
		var cyan = Material("#6ACFCB", true);
		foreach (var id in new[] { "minimal", "book", "adventure", "crystal" })
		{
			var group = new Node3D { Name = id };
			AddChild(group);
			groups[id] = group;
			for (var i = 0; i < spots.Length; i++)
			{
				if (id == "minimal" && i % 2 != 0) continue;
				var point = Lobby.HexToWorld(spots[i].X, spots[i].Y) + new Vector3(.36f, 0, .32f);
				var root = new Node3D { Name = $"Accent{i + 1}", Position = point };
				group.AddChild(root);
				if (id is "minimal" or "crystal")
				{
					Box(root, new(.25f, .10f, .25f), new(0, .05f, 0), gold);
					var crystal = Diamond(root, id == "minimal" ? .27f : .42f, cyan);
					crystal.Position = new(0, id == "minimal" ? .31f : .43f, 0);
					moving.Add((crystal, crystal.Position, i * 1.7f, true));
					if (id == "crystal")
					{
						var satellite = Diamond(root, .19f, purple);
						satellite.Position = new(-.17f, .19f, .10f);
					}
				}
				else if (id == "book")
				{
					Box(root, new(.14f, .40f, .14f), new(0, .20f, 0), gold);
					var book = new Node3D { Name = "OpenBook", Position = new(0, .45f, 0), RotationDegrees = new(-15, -25, 0) };
					root.AddChild(book);
					Box(book, new(.44f, .035f, .32f), Vector3.Zero, green);
					// Two uninterrupted pages and one spine, never four page quadrants.
					foreach (var side in new[] { -1, 1 })
					{
						var hinge = new Node3D { Name = side < 0 ? "LeftPageHinge" : "RightPageHinge", Position = new(0, .025f, 0), RotationDegrees = new(0, 0, side * 7) };
						book.AddChild(hinge);
						Box(hinge, new(.20f, .016f, .29f), new(side * .11f, 0, 0), parchment);
						wind.Add((hinge, hinge.Rotation, i * 1.7f + side * .7f, .035f));
					}
					Box(book, new(.025f, .033f, .32f), new(0, .022f, 0), gold);
				}
				else
				{
					Box(root, new(.045f, .84f, .045f), new(0, .42f, 0), gold);
					var flag = Box(root, new(.28f, .30f, .012f), new(.135f, .60f, 0), i % 2 == 0 ? green : purple);
					moving.Add((flag, flag.Position, i * 1.3f, false));
					Box(flag, new(.07f, .10f, .016f), new(0, 0, .012f), gold);
				}
			}
		}
		AddGardenClusters(spots);
		SetVariant(variant);
	}

	public void SetVariant(string value)
	{
		variant = value;
		foreach (var (id, group) in groups) group.Visible = id == value || (id == "garden" && value != "base");
		Visible = value != "base";
	}

	public override void _Process(double delta)
	{
		if (!Visible) return;
		var reduced = reduceEffects();
		if (lastReducedEffects != reduced)
		{
			foreach (var material in luminous) material.EmissionEnabled = !reduced;
			lastReducedEffects = reduced;
		}
		var still = reduceMotion();
		foreach (var (node, rotation, phase, amplitude) in wind)
		{
			if (!node.IsVisibleInTree()) continue;
			var flutter = still ? 0 : Mathf.Sin((float)clock * 1.4f + phase) * amplitude;
			node.Rotation = rotation + new Vector3(0, 0, flutter);
		}
		if (!still) clock += delta;
		foreach (var (node, rest, phase, floating) in moving)
		{
			if (!node.IsVisibleInTree()) continue;
			var wave = still ? 0 : Mathf.Sin((float)clock * 1.25f + phase);
			node.Position = rest + (floating ? new Vector3(0, wave * .025f, 0) : Vector3.Zero);
			node.Rotation = floating ? new Vector3(0, wave * .14f, 0) : new Vector3(0, wave * .08f, 0);
		}
	}


	// Approved sheets 05 (rounded rocks/canopy) and 30 (colorful flowers).
	// Four already blocked tree cells; three combined mesh draws per garden.
	void AddGardenClusters(Vector2I[] spots)
	{
		var garden = new Node3D { Name = "garden" };
		AddChild(garden); groups["garden"] = garden;
		var vertexMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = .92f };
		for (var i = 0; i < spots.Length; i++)
		{
			var root = new Node3D { Name = $"Garden{i + 1}", Position = Lobby.HexToWorld(spots[i].X, spots[i].Y) + new Vector3(.05f, 0, .55f) };
			garden.AddChild(root);
			var rocks = new SurfaceTool(); rocks.Begin(Mesh.PrimitiveType.Triangles);
			RoundVolume(rocks, new(-.12f, .085f, .03f), new(.17f, .12f, .14f), new("#A89D7F"));
			RoundVolume(rocks, new(.04f, .055f, .14f), new(.12f, .08f, .10f), new("#C5B699"));
			RoundVolume(rocks, new(.16f, .04f, -.04f), new(.085f, .06f, .075f), new("#968976"));
			FinishCluster(root, rocks, vertexMaterial, "RoundedRocks");
			var leaves = new SurfaceTool(); leaves.Begin(Mesh.PrimitiveType.Triangles);
			for (var leaf = 0; leaf < 5; leaf++)
			{
				var angle = leaf * Mathf.Tau / 5;
				var center = new Vector3(Mathf.Cos(angle) * .115f, .14f + (leaf % 2) * .045f, Mathf.Sin(angle) * .115f);
				RoundVolume(leaves, center, new(.13f, .105f, .12f), new(leaf % 2 == 0 ? "#74AB48" : "#98C35A"));
			}
			var foliage = FinishCluster(root, leaves, vertexMaterial, "RoundedFoliage");
			wind.Add((foliage, Vector3.Zero, i * 1.9f, .025f));
			var flowers = new SurfaceTool(); flowers.Begin(Mesh.PrimitiveType.Triangles);
			for (var f = 0; f < 2; f++)
			{
				var center = new Vector3(f == 0 ? -.12f : .13f, f == 0 ? .36f : .29f, .12f);
				RoundVolume(flowers, new(center.X, center.Y * .5f, center.Z), new(.012f, center.Y * .5f, .012f), new("#567F3D"));
				var petalColor = new Color((i + f) % 3 == 0 ? "#F6D56F" : (i + f) % 3 == 1 ? "#E69CAB" : "#87C7DA");
				for (var petal = 0; petal < 5; petal++)
				{
					var angle = petal * Mathf.Tau / 5;
					RoundVolume(flowers, center + new Vector3(Mathf.Cos(angle) * .062f, 0, Mathf.Sin(angle) * .062f), new(.049f, .022f, .049f), petalColor);
				}
				RoundVolume(flowers, center + new Vector3(0, .014f, 0), new(.034f, .026f, .034f), new("#E8AF45"));
			}
			var stems = FinishCluster(root, flowers, vertexMaterial, "Flowers");
			wind.Add((stems, Vector3.Zero, i * 1.9f + .8f, .045f));
		}
	}

	MeshInstance3D FinishCluster(Node3D parent, SurfaceTool surface, Material material, string name)
	{
		var mesh = surface.Commit();
		var node = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material };
		parent.AddChild(node); AccentMeshCount++; AccentTriangleCount += mesh.GetFaces().Length / 3;
		return node;
	}

	static void RoundVolume(SurfaceTool surface, Vector3 center, Vector3 scale, Color color)
	{
		const int Segments = 8, Rings = 4;
		Vector3 Point(int ring, int segment)
		{
			var latitude = ring * Mathf.Pi / Rings;
			var longitude = segment * Mathf.Tau / Segments;
			return new(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
		}
		void Vertex(Vector3 point)
		{
			surface.SetColor(color.SrgbToLinear());
			surface.SetNormal(new Vector3(point.X / scale.X, point.Y / scale.Y, point.Z / scale.Z).Normalized());
			surface.AddVertex(center + point * scale);
		}
		for (var ring = 0; ring < Rings; ring++)
			for (var segment = 0; segment < Segments; segment++)
			{
				var a = Point(ring, segment); var b = Point(ring, segment + 1);
				var c = Point(ring + 1, segment); var d = Point(ring + 1, segment + 1);
				if (ring > 0) { Vertex(a); Vertex(c); Vertex(b); }
				if (ring < Rings - 1) { Vertex(b); Vertex(c); Vertex(d); }
			}
	}

	StandardMaterial3D Material(string color, bool emission = false)
	{
		var material = new StandardMaterial3D { AlbedoColor = Color.FromHtml(color), Roughness = .78f };
		if (emission)
		{
			material.Emission = Color.FromHtml(color);
			material.EmissionEnergyMultiplier = .32f;
			material.EmissionEnabled = true;
			luminous.Add(material);
		}
		return material;
	}

	MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 position, Material material)
	{
		var node = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = material };
		parent.AddChild(node);
		AccentMeshCount++;
		AccentTriangleCount += 12;
		return node;
	}

	MeshInstance3D Diamond(Node3D parent, float height, Material material)
	{
		var mesh = new SurfaceTool();
		mesh.Begin(Mesh.PrimitiveType.Triangles);
		for (var i = 0; i < 6; i++)
		{
			var a = new Vector3(Mathf.Cos(i * Mathf.Tau / 6) * height * .28f, 0, Mathf.Sin(i * Mathf.Tau / 6) * height * .28f);
			var b = new Vector3(Mathf.Cos((i + 1) * Mathf.Tau / 6) * height * .28f, 0, Mathf.Sin((i + 1) * Mathf.Tau / 6) * height * .28f);
			mesh.AddVertex(new(0, height * .6f, 0)); mesh.AddVertex(a); mesh.AddVertex(b);
			mesh.AddVertex(new(0, -height * .4f, 0)); mesh.AddVertex(b); mesh.AddVertex(a);
		}
		mesh.GenerateNormals();
		var node = new MeshInstance3D { Mesh = mesh.Commit(), MaterialOverride = material };
		parent.AddChild(node);
		AccentMeshCount++;
		AccentTriangleCount += 12;
		return node;
	}
}
