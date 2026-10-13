using Godot;

namespace ArquipelagoDoCeu;

// O que a criança vê e ouve do Boneco: o número do escudo, a névoa que o cobre, o raio do ataque e o aviso
// escrito do resultado. Todo feedback tem texto e forma além de cor e som.
public partial class TrainingDummy : Node3D
{
	const float MistRadius = 1.15f;
	const float PopupSeconds = 2.8f;
	static readonly Color Gold = Color.FromHtml("#F2B632");

	readonly Label3D shieldLabel = new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 96, PixelSize = 0.009f, OutlineSize = 24, Modulate = Colors.White, NoDepthTest = true };
	readonly Label3D popup = new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 72, PixelSize = 0.009f, OutlineSize = 20, NoDepthTest = true, Visible = false };
	readonly MeshInstance3D mist = new() { Mesh = new SphereMesh { Radius = MistRadius, Height = MistRadius * 2f, RadialSegments = 24, Rings = 12 } };
	readonly StandardMaterial3D mistMaterial = new()
	{
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = new Color(0.72f, 0.65f, 0.9f, 0.5f),
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
	};
	readonly AudioStreamPlayer sound = new();
	Vector3 center;
	int startingShield = 1;
	Tween? tween;
	int popupVersion;

	public bool ReduceMotion { get; set; }

	// `ground` é o pé do Boneco e `top` a altura do topo no mundo.
	public void Place(Vector3 ground, float top)
	{
		center = ground + new Vector3(0f, (top - ground.Y) * 0.5f, 0f);
		mist.Position = center;
		mist.MaterialOverride = mistMaterial;
		shieldLabel.Position = new Vector3(ground.X, top + 1.5f, ground.Z);
		popup.Position = new Vector3(ground.X, top + 2.4f, ground.Z);
		AddChild(mist);
		AddChild(shieldLabel);
		AddChild(popup);
		AddChild(sound);
		UiKit.Outline3D(shieldLabel);
		UiKit.Outline3D(popup);
	}

	public void SetFont(Font? font)
	{
		shieldLabel.Font = font;
		popup.Font = font;
	}

	// Novo escudo: a névoa volta cheia.
	public void Reset(int shield)
	{
		tween?.Kill();
		startingShield = shield;
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
		shieldLabel.Text = "Escudo 0";
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
		var to = center;
		var length = from.DistanceTo(to);
		var beam = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = perfect ? 0.4f : 0.25f, BottomRadius = perfect ? 0.4f : 0.25f, Height = length, RadialSegments = 8 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Gold, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true },
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

	void SetShieldText(int value) => shieldLabel.Text = $"Escudo {value}";

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
