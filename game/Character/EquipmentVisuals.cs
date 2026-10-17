using System.Collections.Generic;
using Godot;
using ArquipelagoDoCeu.Equipment;
namespace ArquipelagoDoCeu;

// Reversible accessories. Bind offsets are captured once, so changing loadouts while
// walking never bakes the current animation pose into the next equipment set.
public partial class EquipmentVisuals : Node3D
{
	Skeleton3D rig = null!;
	Node3D model = null!;
	readonly Dictionary<string, Transform3D> bind = new();
	readonly List<(Node3D Node, int Bone, Transform3D Offset)> mounts = new();
	public int VisiblePartCount { get; private set; }
	public string ArmorId { get; private set; } = "original";
	public string HelmetId { get; private set; } = "none";
	public void Initialize(Node3D source, Skeleton3D skeleton)
	{
		model = source; rig = skeleton; Name = "EquipmentVisuals";
		foreach (var name in new[] { "Spine", "Head", "LeftArm", "RightArm" })
			bind[name] = (model.GlobalTransform.AffineInverse() * rig.GlobalTransform * rig.GetBoneGlobalPose(rig.FindBone(name))).AffineInverse();
	}
	public override void _Process(double delta)
	{
		foreach (var mount in mounts)
			mount.Node.Transform = model.GlobalTransform.AffineInverse() * rig.GlobalTransform * rig.GetBoneGlobalPose(mount.Bone) * mount.Offset;
	}
	public void Apply(EquipmentLoadout loadout)
	{
		foreach (var mount in mounts) { RemoveChild(mount.Node); mount.Node.QueueFree(); }
		mounts.Clear(); VisiblePartCount = 0;
		ArmorId = loadout.Armor; HelmetId = loadout.Helmet;
		var cloth = Material(loadout.ClothColor, false);
		var metal = Material(loadout.MetalColor, true);
		var trim = Material(loadout.AccentColor, true);
		if (ArmorId != "original")
		{
			var chest = Mount("Spine", "Chest");
			if (ArmorId == "guardian")
			{
				Breastplate(chest, metal, trim);
				for (int side = -1; side <= 1; side += 2)
				{
					var shoulder = Mount(side < 0 ? "RightArm" : "LeftArm", "Pauldron");
					Ellipsoid(shoulder, "ShoulderShell", new(side * .18f, .777f, .052f), new(.082f, .042f, .075f), metal);
					Ellipsoid(shoulder, "ShoulderRim", new(side * .18f, .804f, .06f), new(.078f, .010f, .071f), trim);
				}
				Plate(chest, "Heraldry", new(0, .70f, .208f), new(.065f, .1f, .006f), cloth);
				Gem(chest, new(0, .735f, .22f), .030f, trim);
			}
			else if (ArmorId == "scout")
			{
				var strap = Plate(chest, "ScoutSash", new(0, .698f, .157f), new(.075f, .255f, .022f), cloth);
				strap.Rotation = new Vector3(0, 0, -.48f);
				var shoulder = Mount("LeftArm", "ScoutShoulder");
				Ellipsoid(shoulder, "LeatherGuard", new(.18f, .794f, .054f), new(.085f, .046f, .08f), cloth);
				Gem(chest, new(.034f, .72f, .18f), .035f, metal);
				Plate(chest, "BeltPouch", new(-.105f, .562f, .13f), new(.08f, .09f, .045f), cloth);
				Plate(chest, "PouchClasp", new(-.105f, .58f, .157f), new(.03f, .018f, .012f), trim);
			}
			else if (ArmorId == "scholar")
			{
				for (int side = -1; side <= 1; side += 2)
				{
					Plate(chest, "EmbroideredStole", new(side * .073f, .68f, .167f), new(.062f, .265f, .018f), cloth);
					Plate(chest, "StoleHem", new(side * .073f, .55f, .179f), new(.066f, .022f, .012f), trim);
					Gem(chest, new(side * .073f, .72f, .188f), .025f, trim);
				}
				Gem(chest, new(0, .77f, .17f), .041f, metal);
			}
		}
		if (HelmetId != "none")
		{
			var head = Mount("Head", "Headgear");
			// A rear horseshoe leaves the entire fringe and face open. Its inner
			// envelope clears the original hair; there is no front band through locks.
			OpenHelmetBand(head, metal);
			for (int side = -1; side <= 1; side += 2)
			{
				Ellipsoid(head, "TempleGuard", new(side < 0 ? -.313f : .295f, 1.17f, .08f), new(.016f, .03f, .034f), metal);
				if (HelmetId == "crystal")
					Gem(head, new(side < 0 ? -.313f : .295f, 1.20f, .12f), .055f, trim);
				else
					Gem(head, new(side < 0 ? -.313f : .295f, 1.17f, .118f), .026f, trim);
			}
		}
		AddCosmetics(loadout.Cosmetics);
		_Process(0);
	}
	void OpenHelmetBand(Node3D parent, Material mat)
	{
		// Convex outline measured from original head vertices at bind height 1.10–1.23.
		// Open at Z=.08: no strap crosses forehead/fringe. Inner clearance 12 mm.
		Vector2[] outline = [new(-0.2933366f, 0.0800000f), new(-0.2806860f, -0.0194160f), new(-0.2799260f, -0.0212230f), new(-0.2421790f, -0.0633180f), new(-0.0258620f, -0.1721170f), new(-0.0250300f, -0.1722780f), new(0.0889400f, -0.1394900f), new(0.1485640f, -0.1223200f), new(0.1503190f, -0.1214130f), new(0.1791290f, -0.0868070f), new(0.2146940f, -0.0369440f), new(0.2480830f, 0.0206290f), new(0.2749435f, 0.0800000f)];
		var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
		Vector3 Point(int i, int corner)
		{
			var previous = (outline[i] - outline[System.Math.Max(0, i - 1)]).Normalized();
			var next = (outline[System.Math.Min(outline.Length - 1, i + 1)] - outline[i]).Normalized();
			var normal = new Vector2(previous.Y + next.Y, -previous.X - next.X).Normalized();
			var v = outline[i] + normal * (corner < 2 ? .024f : .012f);
			return new Vector3(v.X, corner == 1 || corner == 2 ? 1.19f : 1.16f, v.Y);
		}
		void Tri(Vector3 a, Vector3 b, Vector3 c) { surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c); }
		for (int i = 0; i < outline.Length - 1; i++) for (int k = 0; k < 4; k++)
			{
				int n = (k + 1) % 4; Tri(Point(i, k), Point(i + 1, n), Point(i, n)); Tri(Point(i, k), Point(i + 1, k), Point(i + 1, n));
			}
		surface.GenerateNormals();
		parent.AddChild(new MeshInstance3D { Name = "FittedOpenHelmet", Mesh = surface.Commit(), MaterialOverride = mat }); VisiblePartCount++;
	}
	Node3D Mount(string bone, string label)
	{
		var node = new Node3D { Name = label + "_" + bone }; AddChild(node);
		mounts.Add((node, rig.FindBone(bone), bind[bone])); return node;
	}
	static StandardMaterial3D Material(string html, bool metallic) => new() { AlbedoColor = Color.FromHtml(html), Metallic = metallic ? .48f : 0, Roughness = metallic ? .38f : .86f };
	MeshInstance3D Plate(Node3D parent, string name, Vector3 pos, Vector3 size, Material mat)
	{
		var node = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = mat }; parent.AddChild(node); VisiblePartCount++; return node;
	}
	void Ellipsoid(Node3D parent, string name, Vector3 pos, Vector3 scale, Material mat)
	{
		var node = new MeshInstance3D { Name = name, Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 20, Rings = 10 }, Position = pos, Scale = scale, MaterialOverride = mat }; parent.AddChild(node); VisiblePartCount++;
	}
	void Breastplate(Node3D parent, Material metal, Material trim)
	{
		Vector2[] outline = [new(-.094f, .09f), new(.094f, .09f), new(.115f, .04f), new(.092f, -.067f), new(0, -.117f), new(-.092f, -.067f), new(-.115f, .04f)];
		var face = new SurfaceTool(); face.Begin(Mesh.PrimitiveType.Triangles);
		var border = new SurfaceTool(); border.Begin(Mesh.PrimitiveType.Triangles);
		for (int i = 0; i < outline.Length; i++)
		{
			var a = outline[i]; var b = outline[(i + 1) % outline.Length];
			var va = new Vector3(a.X, a.Y, .175f); var vb = new Vector3(b.X, b.Y, .175f);
			var ia = new Vector3(a.X * .88f, a.Y * .88f, .19f); var ib = new Vector3(b.X * .88f, b.Y * .88f, .19f);
			// Front points towards +Z. Clockwise Godot winding.
			face.AddVertex(new Vector3(0, 0, .205f)); face.AddVertex(ia); face.AddVertex(ib);
			border.AddVertex(va); border.AddVertex(ib); border.AddVertex(vb);
			border.AddVertex(va); border.AddVertex(ia); border.AddVertex(ib);
		}
		face.GenerateNormals(); border.GenerateNormals();
		parent.AddChild(new MeshInstance3D { Name = "FacetedBreastplate", Mesh = face.Commit(), Position = new Vector3(0, .699f, 0), MaterialOverride = metal });
		parent.AddChild(new MeshInstance3D { Name = "BreastplateRim", Mesh = border.Commit(), Position = new Vector3(0, .699f, 0), MaterialOverride = trim });
		VisiblePartCount += 2;
	}
	void Gem(Node3D parent, Vector3 pos, float size, Material mat)
	{
		var node = Plate(parent, "Crest", pos, new(size, size, .022f), mat); node.Rotation = new Vector3(0, 0, Mathf.Pi / 4);
	}
}
