using Godot;
using ArquipelagoDoCeu.Equipment;
namespace ArquipelagoDoCeu;

/// <summary>Isolated portrait: shares cached model resources, never the game actor or world.</summary>
public partial class EquipmentPreview : PanelContainer
{
	SpectrumCharacter? character; Camera3D? camera; SubViewport? viewport;
	EquipmentLoadout pending = new();
	readonly string variant;
	public EquipmentPreview(string variant) { this.variant = variant; Name = "EquipmentPreview"; MouseFilter = MouseFilterEnum.Stop; }
	public override void _Ready()
	{
		AddThemeStyleboxOverride("panel", JourneyStyle.Box(variant));
		var column = new VBoxContainer(); AddChild(column);
		column.AddChild(JourneyStyle.Text(variant, "Prévia 3D", 20));
		var container = new SubViewportContainer { Stretch = true, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore }; column.AddChild(container);
		viewport = new SubViewport { Size = new Vector2I(280, 440), OwnWorld3D = true, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa2X }; container.AddChild(viewport);
		var world = new Node3D(); viewport.AddChild(world);
		var environment = new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Color.FromHtml("#263544"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.75f } }; world.AddChild(environment);
		var key = new DirectionalLight3D { RotationDegrees = new Vector3(-35, -35, 0), LightEnergy = 1.7f }; world.AddChild(key);
		var fill = new DirectionalLight3D { RotationDegrees = new Vector3(-20, 145, 0), LightEnergy = 0.6f }; world.AddChild(fill);
		character = new SpectrumCharacter { Name = "PreviewWarrior", ReduceMotion = true }; world.AddChild(character); character.ApplyEquipment(pending); character.SetLocomotion(0, false);
		camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.05f, Position = new Vector3(1.8f, 1.15f, 3.5f), Current = true }; world.AddChild(camera); camera.LookAt(new Vector3(0, 0.8f, 0));
		var turn = new HBoxContainer(); var left = JourneyStyle.Button(variant, "Girar ←", () => Rotate(-35), "PreviewTurnLeft"); left.SizeFlagsHorizontal = SizeFlags.ExpandFill; turn.AddChild(left); var right = JourneyStyle.Button(variant, "→", () => Rotate(35), "PreviewTurnRight"); right.SizeFlagsHorizontal = SizeFlags.ExpandFill; turn.AddChild(right); column.AddChild(turn);
		column.AddChild(JourneyStyle.Text(variant, "Só Aplicar salva.", 16));
	}
	public override void _Process(double delta) { if (camera != null && viewport != null && viewport.Size.X > 0) camera.Size = Mathf.Max(2.05f, 1.25f * viewport.Size.Y / viewport.Size.X); }
	void Rotate(float degrees) { if (character != null) character.RotateY(Mathf.DegToRad(degrees)); }
	public void Apply(EquipmentLoadout value) { pending = EquipmentCatalog.Normalize(value); character?.ApplyEquipment(pending); }
}
