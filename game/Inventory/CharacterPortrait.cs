using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// O Personagem num visor próprio, para a prévia do Inventário. Usa um mundo 3D à parte: nunca mexe no Personagem do jogo.
public partial class CharacterPortrait : VBoxContainer
{
	const float TurnDegrees = 35f;
	const int TargetHeight = 72;

	SpectrumCharacter? character;
	Camera3D? camera;
	SubViewport? viewport;
	EquipmentLoadout pending = new();

	public CharacterPortrait()
	{
		Name = "CharacterPortrait";
	}

	public override void _Ready()
	{
		var container = new SubViewportContainer { Stretch = true, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(240, 300) };
		AddChild(container);
		viewport = new SubViewport { Size = new Vector2I(280, 440), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa2X };
		container.AddChild(viewport);
		BuildScene(viewport);
		AddChild(BuildTurnButtons());
	}

	public override void _Process(double delta)
	{
		if (camera is null || viewport is null || viewport.Size.X <= 0) return;
		camera.Size = Mathf.Max(2.05f, 1.25f * viewport.Size.Y / viewport.Size.X);
	}

	public void Show(EquipmentLoadout loadout)
	{
		pending = loadout;
		character?.ApplyEquipment(pending);
	}

	void BuildScene(SubViewport parent)
	{
		var world = new Node3D();
		parent.AddChild(world);
		world.AddChild(Background());
		world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -35, 0), LightEnergy = 1.7f });
		world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 145, 0), LightEnergy = 0.6f });
		character = new SpectrumCharacter { Name = "PortraitCharacter", ReduceMotion = true };
		world.AddChild(character);
		character.ApplyEquipment(pending);
		character.SetLocomotion(0, false);
		camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.05f, Position = new Vector3(1.8f, 1.15f, 3.5f), Current = true };
		world.AddChild(camera);
		camera.LookAt(new Vector3(0, 0.8f, 0));
	}

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

	HBoxContainer BuildTurnButtons()
	{
		var row = new HBoxContainer();
		row.AddChild(Turn("Girar ←", -TurnDegrees, "PortraitTurnLeft"));
		row.AddChild(Turn("→", TurnDegrees, "PortraitTurnRight"));
		return row;
	}

	Button Turn(string text, float degrees, string name)
	{
		var button = BookStyle.Button(text, () => character?.RotateY(Mathf.DegToRad(degrees)), name, TargetHeight);
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return button;
	}
}
