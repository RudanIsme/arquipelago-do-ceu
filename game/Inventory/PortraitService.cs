using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// O retrato do Personagem, um só para o jogo todo: montado em segundo plano ao entrar no Lobby (o modelo carrega em outra thread e
// o primeiro quadro compila os shaders), guardado e ligado só enquanto algum modal o mostra. Abrir o Inventário não monta nada.
public partial class PortraitService : Node
{
	const string IdleModel = "res://assets/modelos/spectrum-cape-body-v2/guerreiro-parado.glb";
	static readonly Vector2I Size = new(280, 440);

	SubViewport? viewport;
	SpectrumCharacter? character;
	Camera3D? camera;
	Node3D? world;
	EquipmentLoadout pending = new();
	bool loading;
	bool primed;
	int viewers;

	// Verdadeiro quando o retrato está montado e pronto para aparecer.
	public bool IsBuilt => character is not null;

	public Texture2D? Texture => viewport?.GetTexture();

	public event System.Action? Built;

	// Pede o modelo em outra thread; quando chega, monta o retrato com o que a criança veste.
	public void Warm()
	{
		if (loading || viewport is not null) return;
		loading = true;
		ResourceLoader.LoadThreadedRequest(IdleModel);
		SetProcess(true);
	}

	public override void _Ready() => SetProcess(false);

	public override void _Process(double delta)
	{
		if (viewport is null && loading)
		{
			var status = ResourceLoader.LoadThreadedGetStatus(IdleModel);
			if (status == ResourceLoader.ThreadLoadStatus.Loaded) Build();
			else if (status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource) GiveUp();
			return;
		}
		// O primeiro quadro desenhado compila os shaders; depois o retrato dorme até alguém precisar dele.
		if (viewport is null || primed) return;
		primed = true;
		Sleep();
	}

	// Uma tela mostra o retrato: liga a renderização (e, se ainda nada foi montado, pede a montagem já).
	public void Acquire()
	{
		viewers++;
		if (viewport is null) Warm();
		Wake();
	}

	// A tela fechou: sem ninguém olhando, o retrato para de renderizar.
	public void Release()
	{
		viewers = System.Math.Max(0, viewers - 1);
		if (viewers == 0) Sleep();
	}

	public void Turn(float degrees) => character?.RotateY(Mathf.DegToRad(degrees));

	public void Show(EquipmentLoadout loadout)
	{
		pending = loadout;
		character?.ApplyEquipment(loadout);
	}

	void Build()
	{
		loading = false;
		viewport = new SubViewport { Size = Size, OwnWorld3D = true, TransparentBg = false, Msaa3D = Viewport.Msaa.Msaa2X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		AddChild(viewport);
		world = new Node3D();
		viewport.AddChild(world);
		world.AddChild(Background());
		world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -35, 0), LightEnergy = 1.7f });
		world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 145, 0), LightEnergy = 0.6f });
		character = new SpectrumCharacter { Name = "PortraitCharacter", ReduceMotion = true, IdleOnly = true };
		world.AddChild(character);
		character.ApplyEquipment(pending);
		character.SetLocomotion(0, false);
		camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.05f, Position = new Vector3(1.8f, 1.15f, 3.5f), Current = true };
		world.AddChild(camera);
		camera.LookAt(new Vector3(0, 0.8f, 0));
		camera.Size = Mathf.Max(2.05f, 1.25f * Size.Y / Size.X);
		Built?.Invoke();
	}

	void Wake()
	{
		if (viewport is null) return;
		world!.ProcessMode = ProcessModeEnum.Inherit;
		viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
	}

	// Sem o modelo o retrato fica sem imagem, e o jogo segue: o Inventário abre do mesmo jeito.
	void GiveUp()
	{
		loading = false;
		SetProcess(false);
		GD.PushWarning($"Não foi possível carregar o modelo do retrato em {IdleModel}.");
	}

	void Sleep()
	{
		if (viewport is null || viewers > 0) return;
		viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
		world!.ProcessMode = ProcessModeEnum.Disabled;
	}

	// Verdadeiro quando a renderização está desligada (ninguém olhando): é o que o piloto confere ao fechar um modal.
	public bool Sleeping => viewport?.RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled;

	static WorldEnvironment Background() => new()
	{
		Environment = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = Color.FromHtml("#E4D2A6"),
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = Colors.White,
			AmbientLightEnergy = 0.75f,
		},
	};
}
