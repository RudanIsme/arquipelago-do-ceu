using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// As torres e o prédio do Mestre como pontos de interação: sem portões, o realce vai no próprio modelo quando a criança chega
// perto da base, e tocar ou clicar nele leva até lá (ou entra, se já está ao alcance).
public partial class Lobby
{
	// Os modelos que se escolhem por clique, por id do objeto de interação ("torre-matematica", "mestre").
	readonly Dictionary<string, Node3D> pickable = new();

	StandardMaterial3D? towerGlow;

	StandardMaterial3D Glow => towerGlow ??= new StandardMaterial3D
	{
		AlbedoColor = new Color(0.95f, 0.71f, 0.2f, 0.28f),
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
	};

	// O prédio do Mestre: um salão simples, até a arte definitiva. Fica no lugar do portão antigo, com o nome em cima.
	float AddHall(Vector3 ground, string name)
	{
		const float wall = 1.7f;
		var stone = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#8E7BB5"), Roughness = 0.9f };
		var roof = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#5B3FA0"), Roughness = 0.8f };
		var hall = new Node3D { Name = "SalaoDoMestre", Position = ground };
		hall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.6f, wall, 2.2f) }, MaterialOverride = stone, Position = new Vector3(0f, wall / 2f, 0f) });
		hall.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(3f, 1.1f, 2.6f) }, MaterialOverride = roof, Position = new Vector3(0f, wall + 0.55f, 0f) });
		hall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.7f, 1.2f, 0.12f) }, MaterialOverride = roof, Position = new Vector3(0f, 0.6f, 1.12f) });
		AddChild(hall);
		var sign = new Label3D { Text = name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 44, PixelSize = 0.012f, OutlineSize = 14, OutlineModulate = Color.FromHtml("#1B1424"), Modulate = Color.FromHtml("#FFF6E0"), NoDepthTest = true, Position = ground + new Vector3(0f, wall + 2.1f, 0f) };
		AddChild(sign);
		signs.Add(sign);
		sign.AddToGroup(CameraController.LabelGroup);
		pickable["mestre"] = hall;
		return wall;
	}

	// Onde a criança começa: o caminho ao lado do Guia, nunca dentro da lagoa.
	public System.Numerics.Vector2 StartPoint => plan.Zones.TryGetValue("guia", out var guide)
		? HexGrid.ToWorld(guide.Neighbors().FirstOrDefault(c => plan.Path.Contains(c) && Grid.IsWalkable(c), guide))
		: System.Numerics.Vector2.Zero;

	// Liga ou apaga o realce dourado num modelo inteiro.
	void SetGlow(Node3D? root, bool on)
	{
		if (root is null) return;
		foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
			((MeshInstance3D)node).MaterialOverlay = on ? Glow : null;
	}

	// O realce acompanha o objeto ao alcance; o indicador em seta segue como reforço (a cor nunca é a única pista).
	void GlowOnly(string? objectId)
	{
		foreach (var (id, model) in pickable) SetGlow(model, id == objectId);
	}

	// O objeto de interação cujo modelo o clique ou toque atingiu, se for torre ou o prédio do Mestre.
	public string? PickInteractable(Camera3D camera, Vector2 screenPoint)
	{
		var origin = camera.ProjectRayOrigin(screenPoint);
		var direction = camera.ProjectRayNormal(screenPoint);
		string? best = null;
		var nearest = float.MaxValue;
		foreach (var (id, model) in pickable)
		{
			var box = WorldBox(model);
			if (!box.IntersectsSegment(origin, origin + direction * 500f)) continue;
			var distance = origin.DistanceTo(box.GetCenter());
			if (distance >= nearest) continue;
			nearest = distance;
			best = id;
		}
		return best;
	}
}
