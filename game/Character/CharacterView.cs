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
	const float Height = 1.5f;

	// Categoria do catálogo -> parâmetro do shader. Categorias sem entrada aqui (acessórios, por
	// enquanto) são ignoradas até existir arte para elas.
	static readonly Dictionary<string, string> ShaderParams = new()
	{
		["skinTone"] = "skin_color",
		["clothesColor"] = "cloth_color",
	};

	const float BreathAmount = 0.015f;
	const float BreathSpeed = 2f;

	ShaderMaterial? material;
	Node3D? model;
	Skeleton3D? skeleton;
	AnimationPlayer? animations;
	string? walkClip;
	Vector3 restScale = Vector3.One;
	Tween? attackTween;
	bool moving;
	double clock;

	// Redução de animações: o Personagem fica na pose de repouso, sem respirar e sem balançar os braços e pernas ao andar.
	public bool ReduceMotion { get; set; }

	public bool IsWalkingAnimation => animations?.IsPlaying() == true;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>(ModelPath);
		if (scene is null)
		{
			GD.PushError($"Não foi possível carregar o modelo do Personagem em {ModelPath}.");
			return;
		}
		model = scene.Instantiate<Node3D>();
		AddChild(model);
		SetUpMaterial(model);
		StandOnGround(model);
		SetUpAnimations(model);
	}

	public void Apply(IEnumerable<ChosenOption> chosen)
	{
		if (material is null) return;
		foreach (var item in chosen)
			if (ShaderParams.TryGetValue(item.Category.Id, out var param))
				material.SetShaderParameter(param, Color.FromHtml(item.Option.Color));
	}

	// O modelo traz só o ciclo de andar. Parado é a pose de repouso com uma respiração leve, feita no código.
	void SetUpAnimations(Node3D root)
	{
		skeleton = root.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
		animations = root.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
		restScale = root.Scale;
		walkClip = animations?.GetAnimationList().FirstOrDefault(name => name != "RESET");
		if (animations is null || walkClip is null)
		{
			GD.PushWarning("O modelo do Personagem não trouxe animação de andar.");
			return;
		}
		animations.GetAnimation(walkClip).LoopMode = Animation.LoopModeEnum.Linear;
	}

	public void SetMoving(bool isMoving)
	{
		if (moving != isMoving)
		{
			moving = isMoving;
			if (!moving) BackToRest();
		}
		if (moving && !ReduceMotion && animations is not null && walkClip is not null && !animations.IsPlaying())
			animations.Play(walkClip);
		if ((!moving || ReduceMotion) && animations is { } player && player.IsPlaying()) BackToRest();
	}

	public override void _Process(double delta)
	{
		if (model is null) return;
		clock += delta;
		// Respirar é um vai e vem da altura; com a redução de animações o corpo fica parado.
		var breath = moving || ReduceMotion ? 0f : Mathf.Sin((float)clock * BreathSpeed) * BreathAmount;
		model.Scale = new Vector3(restScale.X, restScale.Y * (1f + breath), restScale.Z);
	}

	// Ataque provisório (até existir a animação do modelo): inclina o corpo para a frente e volta, como quem lança o cajado.
	// Com a redução de animações o Personagem não se mexe; o raio e o aviso escrito já mostram o ataque.
	public void PlayAttack()
	{
		if (ReduceMotion || model is null) return;
		attackTween?.Kill();
		Rotation = Vector3.Zero;
		attackTween = CreateTween();
		attackTween.TweenProperty(this, "rotation:x", 0.5f, 0.12).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		attackTween.TweenProperty(this, "rotation:x", -0.15f, 0.1);
		attackTween.TweenProperty(this, "rotation:x", 0f, 0.18);
	}

	public bool IsAttacking => attackTween?.IsRunning() == true;

	void BackToRest()
	{
		animations?.Stop();
		skeleton?.ResetBonePoses();
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
