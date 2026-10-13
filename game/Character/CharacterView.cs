using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Profiles;
using Godot;

namespace ArquipelagoDoCeu;

// O Personagem na cena: carrega o modelo, aplica as cores escolhidas no catálogo de aparência
// e controla as animações. Serve igual à prévia do editor e ao Lobby.
public partial class CharacterView : Node3D
{
	const string ModelPath = "res://assets/modelos/personagem-andando.glb";
	const float Height = 1.2f;

	// Categoria do catálogo -> parâmetro do shader. Categorias sem entrada aqui (acessórios, por
	// enquanto) são ignoradas até existir arte para elas.
	static readonly Dictionary<string, string> ShaderParams = new()
	{
		["skinTone"] = "skin_color",
		["clothesColor"] = "cloth_color",
	};

	ShaderMaterial? material;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>(ModelPath);
		if (scene is null)
		{
			GD.PushError($"Não foi possível carregar o modelo do Personagem em {ModelPath}.");
			return;
		}
		var model = scene.Instantiate<Node3D>();
		AddChild(model);
		SetUpMaterial(model);
		StandOnGround(model);
	}

	public void Apply(IEnumerable<ChosenOption> chosen)
	{
		if (material is null) return;
		foreach (var item in chosen)
			if (ShaderParams.TryGetValue(item.Category.Id, out var param))
				material.SetShaderParameter(param, Color.FromHtml(item.Option.Color));
	}

	void SetUpMaterial(Node3D model)
	{
		foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			if (mesh.GetActiveMaterial(0) is not BaseMaterial3D original) continue;
			material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://Character/character.gdshader") };
			material.SetShaderParameter("albedo_tex", original.AlbedoTexture);
			mesh.MaterialOverride = material;
		}
	}

	// Os modelos do Meshy têm a origem no centro da malha: escala para a altura do jogo e assenta os pés no chão.
	void StandOnGround(Node3D model)
	{
		var box = BoundingBox(model);
		if (box.Size.Y <= 0.0001f) return;
		var scale = Height / box.Size.Y;
		model.Scale = Vector3.One * scale;
		model.Position = new Vector3(-(box.Position.X + box.Size.X / 2f) * scale, -box.Position.Y * scale, -(box.Position.Z + box.Size.Z / 2f) * scale);
		GD.Print($"Personagem: altura original {box.Size.Y:0.00}, escala {scale:0.00}");
	}

	// A caixa da malha em repouso engana nos modelos rigados (a pele reescala na hora de desenhar),
	// então a altura sai da posição dos ossos, que está no tamanho real.
	static Aabb BoundingBox(Node3D model)
	{
		var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
		if (skeleton is not null && skeleton.GetBoneCount() > 0)
		{
			var toModel = model.GlobalTransform.AffineInverse() * skeleton.GlobalTransform;
			var first = toModel * skeleton.GetBoneGlobalRest(0).Origin;
			var total = new Aabb(first, Vector3.Zero);
			for (var i = 1; i < skeleton.GetBoneCount(); i++)
				total = total.Expand(toModel * skeleton.GetBoneGlobalRest(i).Origin);
			return total;
		}

		Aabb? merged = null;
		foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			var box = model.GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.GetAabb();
			merged = merged is null ? box : merged.Value.Merge(box);
		}
		return merged ?? new Aabb();
	}
}
