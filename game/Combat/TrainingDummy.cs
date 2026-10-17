using System;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// O que a criança vê e ouve do Boneco: o número do escudo, a névoa que o cobre, o raio do ataque e o aviso
// escrito do resultado. Todo feedback tem texto e forma além de cor e som.
public partial class TrainingDummy : Node3D
{
	const float MistRadius = 1.15f;
	const float PickRadius = 1.3f;
	const float PopupSeconds = 2.8f;
	static readonly Color Gold = Color.FromHtml("#F2B632");

	readonly Label3D shieldLabel = new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 96, PixelSize = 0.009f, OutlineSize = 24, Modulate = Colors.White, NoDepthTest = true };
	readonly Label3D popup = new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 72, PixelSize = 0.009f, OutlineSize = 20, NoDepthTest = true, Visible = false, AutowrapMode = TextServer.AutowrapMode.WordSmart, Width = 640f, VerticalAlignment = VerticalAlignment.Bottom };
	readonly MeshInstance3D mist = new() { Mesh = new SphereMesh { Radius = MistRadius, Height = MistRadius * 2f, RadialSegments = 24, Rings = 12 } };
	readonly StandardMaterial3D mistMaterial = new()
	{
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = new Color(0.72f, 0.65f, 0.9f, 0.5f),
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
	};
	readonly AudioStreamPlayer sound = new();
	Vector3 center;
	// O corpo anda pela Sala; a faixa de aviso e a bolha ficam no mundo.
	readonly Node3D body = new();
	Vector2 home;
	Tween? pulse;
	readonly MeshInstance3D ring = new()
	{
		Mesh = new TorusMesh { InnerRadius = 1.0f, OuterRadius = 1.25f, Rings = 32, RingSegments = 8 },
		MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#F2B632"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = Color.FromHtml("#F2B632"), EmissionEnergyMultiplier = 2f },
		Scale = new Vector3(1f, 0.15f, 1f),
		Visible = false,
	};
	// Como a criatura se apresenta: no Lobby é o Boneco de treino, e na Dungeon é a criatura da Sala.
	public string Title { get; set; } = "Boneco de treino";
	public string RestingHint { get; set; } = "Escolha-o para treinar";
	public string RestingCompact { get; set; } = "Treinar [F]";
	bool compactCaption, captionNear, resting = true;
	int captionValue;
	public void SetCaptionContext(bool compact, bool near)
	{
		if (compactCaption == compact && captionNear == near) return;
		compactCaption = compact; captionNear = near; UpdateCaption();
	}
	void UpdateCaption()
	{
		shieldLabel.Visible = !compactCaption || !resting || captionNear;
		shieldLabel.Text = resting
			? (compactCaption ? RestingCompact : $"{Title}\n{RestingHint}")
			: (compactCaption ? $"{unit}: {captionValue}" : $"{Title}\n{unit}: {captionValue}");
	}
	int startingShield = 1;
	string unit = "Escudo";
	readonly Node3D creature = new() { Visible = false };
	readonly Sprite3D creaturePicture = new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.004f, NoDepthTest = true, Visible = false };
	readonly Label3D creatureMark = new() { Text = "?", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 120, PixelSize = 0.011f, OutlineSize = 24, Modulate = Gold, NoDepthTest = true, RenderPriority = 5, OutlineRenderPriority = 4 };
	readonly MeshInstance3D warning = new()
	{
		Mesh = new BoxMesh { Size = new Vector3(1f, 0.04f, 1f) },
		MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.94f, 0.44f, 0.37f, 0.6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true },
		Visible = false,
	};
	readonly MeshInstance3D bubbleBall = new()
	{
		Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 8 },
		MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.65f, 0.9f, 0.75f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
		Visible = false,
	};
	Tween? tween;
	int popupVersion;

	public bool ReduceMotion { get; set; }

	// O meio do corpo, para o clique escolher o Boneco e o raio chegar nele.
	public Vector3 Center => center + body.Position;

	// Anda com o corpo para `ground` (x, z no mundo), longe ou perto de casa.
	public void MoveTo(Vector2 ground) => body.Position = new Vector3(ground.X - home.X, 0f, ground.Y - home.Y);

	// O anel no chão mostra qual criatura está escolhida; é forma, não só cor.
	public void SetSelected(bool selected)
	{
		pulse?.Kill();
		ring.Visible = selected;
		ring.Scale = new Vector3(1f, 0.15f, 1f);
		if (!selected || ReduceMotion) return;
		pulse = CreateTween().SetLoops();
		pulse.TweenProperty(ring, "scale", new Vector3(1.12f, 0.15f, 1.12f), 0.6f);
		pulse.TweenProperty(ring, "scale", new Vector3(1f, 0.15f, 1f), 0.6f);
	}

	// `ground` é o pé do Boneco e `top` a altura do topo no mundo.
	public void Place(Vector3 ground, float top)
	{
		home = new Vector2(ground.X, ground.Z);
		AddChild(body);
		center = ground + new Vector3(0f, (top - ground.Y) * 0.5f, 0f);
		mist.Position = center;
		ring.Position = ground + new Vector3(0f, 0.08f, 0f);
		body.AddChild(ring);
		mist.MaterialOverride = mistMaterial;
		shieldLabel.Position = new Vector3(ground.X, top + 2.3f, ground.Z);
		popup.Position = new Vector3(ground.X, top + 3.3f, ground.Z);
		body.AddChild(mist);
		body.AddChild(shieldLabel);
		foreach (var label in new Label3D[] { shieldLabel, popup, creatureMark }) label.AddToGroup(CameraController.LabelGroup);
		body.AddChild(popup);
		body.AddChild(sound);
		creature.Position = new Vector3(ground.X, top + 0.5f, ground.Z);
		creature.AddChild(creaturePicture);
		creature.AddChild(creatureMark);
		body.AddChild(creature);
		AddChild(warning);
		AddChild(bubbleBall);
		UiKit.Outline3D(shieldLabel);
		UiKit.Outline3D(popup);
	}

	public void SetFont(Font? font)
	{
		shieldLabel.Font = font;
		popup.Font = font;
	}

	// Novo escudo: a névoa volta cheia.
	public void Reset(int shield, string newUnit = "Escudo")
	{
		tween?.Kill();
		startingShield = Math.Max(1, shield);
		unit = newUnit;
		mist.Visible = true;
		mist.Scale = Vector3.One;
		mistMaterial.AlbedoColor = mistMaterial.AlbedoColor with { A = 0.5f };
		SetShieldText(shield);
	}

	public void Reduced(int remaining)
	{
		SetShieldText(remaining);
		// A névoa fica mais rala conforme o escudo cai; o número é a informação, a transparência é só um reforço.
		mistMaterial.AlbedoColor = mistMaterial.AlbedoColor with { A = 0.18f + 0.32f * remaining / startingShield };
	}

	public void Dissolved()
	{
		SetShieldText(0);
		tween?.Kill();
		if (ReduceMotion)
		{
			mist.Visible = false;
			return;
		}
		tween = CreateTween();
		tween.SetParallel();
		tween.TweenProperty(mist, "scale", Vector3.One * 1.6f, 0.8f);
		tween.TweenProperty(mistMaterial, "albedo_color:a", 0f, 0.8f);
		tween.Chain().TweenCallback(Callable.From(() => mist.Visible = false));
	}

	// A conta confirmada vira um raio de dourado, do Personagem até o Boneco.
	public void Ray(Vector3 from, bool perfect)
	{
		var to = Center;
		var length = from.DistanceTo(to);
		var beam = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = perfect ? 0.4f : 0.25f, BottomRadius = perfect ? 0.4f : 0.25f, Height = length, RadialSegments = 8 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Gold, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, EmissionEnabled = true, Emission = Gold, EmissionEnergyMultiplier = 2.5f },
		};
		AddChild(beam);
		beam.GlobalPosition = (from + to) / 2f;
		beam.LookAt(to, Vector3.Up);
		beam.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);

		var fade = CreateTween();
		if (!ReduceMotion) fade.TweenProperty(beam, "scale", new Vector3(0.1f, 1f, 0.1f), 1.0f);
		else fade.TweenInterval(1.0);
		fade.TweenCallback(Callable.From(beam.QueueFree));

		Play(perfect ? Tones.Perfect : Tones.Hit);
	}

	public void Overshoot() => Play(Tones.Overshoot);

	// Sem treino em andamento: o Boneco descansa, com a névoa leve, esperando a criança escolher.
	public void Idle()
	{
		tween?.Kill();
		HideWarning();
		HideBubble();
		ShowCreature(null);
		mist.Visible = true;
		mist.Scale = Vector3.One;
		mistMaterial.AlbedoColor = mistMaterial.AlbedoColor with { A = 0.3f };
		resting = true; UpdateCaption();
	}

	// A travessura: a névoa chacoalha e um som engraçado toca. Sem animação, só o som e o aviso escrito.
	public void Prank()
	{
		Play(Tones.Prank);
		if (ReduceMotion) return;
		var shake = CreateTween();
		for (var i = 0; i < 4; i++)
		{
			shake.TweenProperty(mist, "position", center + new Vector3(i % 2 == 0 ? 0.25f : -0.25f, 0f, 0f), 0.06);
		}
		shake.TweenProperty(mist, "position", center, 0.06);
	}

	// A criatura do Escriba: a imagem (se existir em assets/criaturas) ou um ponto de interrogação, já que o nome está esquecido.
	public void ShowCreature(string? image)
	{
		creature.Visible = image is not null;
		if (image is null) return;
		var path = $"res://assets/criaturas/{image}.png";
		var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		creaturePicture.Texture = texture;
		creaturePicture.Visible = texture is not null;
		creatureMark.Visible = texture is null;
	}

	// O aviso da bolha: uma faixa no chão, do Boneco até o fim do alcance, que mostra por onde ela vai passar; a criança
	// só precisa sair da faixa. Forma, texto e som: nunca só cor. A faixa se estreita enquanto o aviso corre.
	public void ShowWarning(Vector3 from, Vector3 direction, float length, float width, float progress)
	{
		if (!warning.Visible)
		{
			warning.Visible = true;
			Play(Tones.Warning);
			Say("Cuidado! Saia da faixa.", good: false);
		}
		warning.Position = from + direction * (length / 2f) + new Vector3(0f, 0.08f, 0f);
		warning.Rotation = new Vector3(0f, Mathf.Atan2(direction.X, direction.Z), 0f);
		var narrowing = ReduceMotion ? 1f : 1f - 0.3f * progress;
		warning.Scale = new Vector3(width * narrowing, 1f, length);
	}

	public void HideWarning() => warning.Visible = false;

	public void MoveBubble(Vector3 ground, float radius)
	{
		bubbleBall.Visible = true;
		bubbleBall.Scale = Vector3.One * (radius * 2f);
		bubbleBall.Position = ground + new Vector3(0f, radius + 0.15f, 0f);
	}

	// O raio do clique passa perto do centro do Boneco.
	public bool IsPickedBy(Camera3D camera, Vector2 screenPoint)
	{
		var origin = camera.ProjectRayOrigin(screenPoint);
		var direction = camera.ProjectRayNormal(screenPoint);
		var closest = origin + direction * (Center - origin).Dot(direction);
		return closest.DistanceTo(Center) <= PickRadius;
	}

	// Mostra o Desafio atual: o escudo (Matemático) ou a criatura sem nome (Escriba).
	public void ShowChallenge(CombatSession session)
	{
		if (session.Math is { } math)
		{
			ShowCreature(null);
			Reset(math.Shield.Remaining, "Escudo");
		}
		else if (session.Change is { } change)
		{
			ShowCreature(null);
			Reset(change.Remaining, "Valores que faltam");
		}
		else if (session.Name is { } name)
		{
			ShowCreature(name.Image);
			Reset(name.Remaining, name.Kind switch { NameKind.Sentence => "Palavras que faltam", NameKind.Gap => "Lacunas que faltam", _ => "Sílabas que faltam" });
		}
		else
		{
			ShowCreature(session.Word!.Image);
			Reset(session.Word.Remaining, "Palavras que faltam");
		}
	}

	public void HideBubble() => bubbleBall.Visible = false;

	public void Hurt()
	{
		Play(Tones.Hurt);
		Say("A névoa te alcançou! -1 de Energia.", good: false);
	}

	public void Say(string text, bool good)
	{
		popup.Text = text;
		popup.Modulate = good ? Gold : Color.FromHtml("#C9BFDC");
		popup.Visible = true;
		// Um aviso novo não pode ser escondido pelo temporizador do anterior.
		var version = ++popupVersion;
		GetTree().CreateTimer(PopupSeconds, true, false, true).Timeout += () =>
		{
			if (version == popupVersion) popup.Visible = false;
		};
	}

	void SetShieldText(int value) { resting = false; captionValue = value; UpdateCaption(); }

	void Play(AudioStream stream)
	{
		sound.Stream = stream;
		sound.Play();
	}
}

// Sons curtos gerados em código: não dependem de arquivo de áudio e nenhum é a única pista (sempre há texto e forma).
static class Tones
{
	public static readonly AudioStreamWav Hit = Make((660f, 0.12f), (880f, 0.16f));
	public static readonly AudioStreamWav Perfect = Make((660f, 0.1f), (880f, 0.1f), (1175f, 0.2f));
	public static readonly AudioStreamWav Overshoot = Make((330f, 0.14f), (247f, 0.2f));
	public static readonly AudioStreamWav Warning = Make((523f, 0.1f), (0f, 0.05f), (523f, 0.1f), (0f, 0.05f), (698f, 0.18f));
	public static readonly AudioStreamWav Prank = Make((880f, 0.07f), (660f, 0.07f), (990f, 0.07f), (440f, 0.12f));
	public static readonly AudioStreamWav Hurt = Make((300f, 0.12f), (220f, 0.18f));

	const int Rate = 22050;

	static AudioStreamWav Make(params (float Frequency, float Seconds)[] notes)
	{
		var samples = new System.Collections.Generic.List<byte>();
		foreach (var (frequency, seconds) in notes)
		{
			var count = (int)(seconds * Rate);
			for (var i = 0; i < count; i++)
			{
				// Entrada e saída suaves para não estalar.
				var envelope = Mathf.Min(1f, Mathf.Min(i, count - i) / (Rate * 0.01f));
				var value = (short)(Mathf.Sin(Mathf.Tau * frequency * i / Rate) * 9000f * envelope);
				samples.Add((byte)(value & 0xFF));
				samples.Add((byte)((value >> 8) & 0xFF));
			}
		}
		return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = samples.ToArray() };
	}
}
