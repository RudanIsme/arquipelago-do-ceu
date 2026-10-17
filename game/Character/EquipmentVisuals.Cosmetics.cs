using System.Collections.Generic;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// As peças dos Cosméticos: formas simples e coloridas presas ao corpo. Só aparência, sem colisão nem atributo.
public partial class EquipmentVisuals
{
	void AddCosmetics(IReadOnlyList<CosmeticLook>? looks)
	{
		foreach (var look in looks ?? [])
			AddCosmetic(look);
	}

	void AddCosmetic(CosmeticLook look)
	{
		var material = Material(look.Color, false);
		switch (look.Kind)
		{
			case "bow": Bow(material); break;
			case "tie": Tie(material); break;
			case "scarf": Scarf(material); break;
			case "hat": Hat(material); break;
			case "cap": Cap(material); break;
			case "cape": Mantle(material); break;
			case "backpack": Backpack(material); break;
			case "wings": Wings(material); break;
		}
	}

	void Bow(Material material)
	{
		var chest = Mount("Spine", "CosmeticBow");
		Ellipsoid(chest, "BowLeft", new(-.085f, .77f, .20f), new(.075f, .045f, .03f), material);
		Ellipsoid(chest, "BowRight", new(.085f, .77f, .20f), new(.075f, .045f, .03f), material);
		Ellipsoid(chest, "BowKnot", new(0, .77f, .215f), new(.032f, .032f, .032f), material);
	}

	void Tie(Material material)
	{
		var chest = Mount("Spine", "CosmeticTie");
		Plate(chest, "TieKnot", new(0, .78f, .20f), new(.04f, .035f, .02f), material);
		Plate(chest, "TieBlade", new(0, .70f, .205f), new(.05f, .13f, .014f), material);
	}

	void Scarf(Material material)
	{
		var chest = Mount("Spine", "CosmeticScarf");
		var ring = new MeshInstance3D { Name = "ScarfRing", Mesh = new TorusMesh { InnerRadius = .12f, OuterRadius = .20f }, Position = new(0, .84f, .02f), MaterialOverride = material };
		chest.AddChild(ring);
		VisiblePartCount++;
		Plate(chest, "ScarfTail", new(.07f, .72f, .17f), new(.05f, .16f, .016f), material);
	}

	void Hat(Material material)
	{
		var head = Mount("Head", "CosmeticHat");
		Cylinder(head, "HatBrim", new(0, 1.215f, -.04f), .29f, .02f, material);
		Cylinder(head, "HatCrown", new(0, 1.285f, -.04f), .17f, .14f, material);
	}

	void Cap(Material material)
	{
		var head = Mount("Head", "CosmeticCap");
		Ellipsoid(head, "CapDome", new(0, 1.235f, -.04f), new(.28f, .15f, .28f), material);
		Plate(head, "CapVisor", new(0, 1.20f, .22f), new(.24f, .02f, .14f), material);
	}

	void Mantle(Material material)
	{
		var back = Mount("Spine", "CosmeticMantle");
		Plate(back, "MantleBack", new(0, .68f, -.235f), new(.30f, .36f, .02f), material);
	}

	void Backpack(Material material)
	{
		var back = Mount("Spine", "CosmeticBackpack");
		Plate(back, "PackBody", new(0, .62f, -.29f), new(.22f, .28f, .12f), material);
		Plate(back, "PackFlap", new(0, .72f, -.34f), new(.23f, .08f, .02f), material);
	}

	void Wings(Material material)
	{
		var back = Mount("Spine", "CosmeticWings");
		for (var side = -1; side <= 1; side += 2)
		{
			Ellipsoid(back, "Wing", new(side * .20f, .78f, -.26f), new(.20f, .05f, .02f), material);
			Ellipsoid(back, "WingLow", new(side * .16f, .66f, -.26f), new(.14f, .04f, .02f), material);
		}
	}

	void Cylinder(Node3D parent, string name, Vector3 position, float radius, float height, Material material)
	{
		var node = new MeshInstance3D { Name = name, Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height }, Position = position, MaterialOverride = material };
		parent.AddChild(node);
		VisiblePartCount++;
	}
}
