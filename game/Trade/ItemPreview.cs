using System;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// O quadrado de prévia do Mercador: o item sozinho, girando devagar, num visor pequeno e próprio. É leve: as peças são formas simples,
// o mesmo visor serve a toda oferta (só troca as peças) e nada do Personagem é carregado.
public partial class ItemPreview : PanelContainer
{
	const int Side = 220;
	const float SpinRadiansPerSecond = 0.7f;

	readonly Node3D pivot = new();
	readonly Func<bool> reduceMotion;
	readonly Label empty = BookStyle.Text("Sem modelo ainda", 16);
	EquipmentVisuals? parts;
	Camera3D? camera;

	public ItemPreview(Func<bool> reduceMotion)
	{
		Name = "ItemPreview";
		this.reduceMotion = reduceMotion;
		AddThemeStyleboxOverride("panel", BookStyle.Box(BookStyle.Selected, 3));
		CustomMinimumSize = new Vector2(Side + 12, Side + 12);
	}

	public override void _Ready()
	{
		var container = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(Side, Side) };
		var viewport = new SubViewport { Size = new Vector2I(Side, Side), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible, Msaa3D = Viewport.Msaa.Msaa2X };
		container.AddChild(viewport);
		var world = new Node3D();
		viewport.AddChild(world);
		world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Color.FromHtml("#F6EACB"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.8f } });
		world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -35, 0), LightEnergy = 1.6f });
		world.AddChild(pivot);
		camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 1f, Position = new Vector3(0, 0.3f, 3f), Current = true };
		world.AddChild(camera);
		var holder = new Control { CustomMinimumSize = new Vector2(Side, Side) };
		holder.AddChild(container);
		empty.AutowrapMode = TextServer.AutowrapMode.Off;
		empty.HorizontalAlignment = HorizontalAlignment.Center;
		empty.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
		empty.Position = new Vector2((Side - 150) / 2f, Side / 2f);
		empty.Visible = false;
		holder.AddChild(empty);
		AddChild(holder);
	}

	public override void _Process(double delta)
	{
		if (!reduceMotion()) pivot.RotateY((float)delta * SpinRadiansPerSecond);
	}

	// Troca só as peças do visor; sem nenhuma peça (um item sem modelo), o quadrado diz isso em texto.
	public void Show(EquipmentLoadout loadout)
	{
		parts?.QueueFree();
		parts = new EquipmentVisuals();
		pivot.AddChild(parts);
		parts.ShowAlone(loadout);
		empty.Visible = parts.VisiblePartCount == 0;
		Frame();
	}

	// Sem oferta apontada: o quadrado fica vazio e sem recado.
	public void Clear()
	{
		parts?.QueueFree();
		parts = null;
		empty.Visible = false;
	}

	// Centra as peças no eixo do giro e afasta a câmera até caberem.
	void Frame()
	{
		if (parts is null || camera is null) return;
		var boxes = parts.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Select(m => parts.GlobalTransform.AffineInverse() * m.GlobalTransform * m.GetAabb()).ToList();
		if (boxes.Count == 0) return;
		var box = boxes.Aggregate((a, b) => a.Merge(b));
		parts.Position = -(box.Position + box.Size / 2f);
		// O item gira, então cabe pela diagonal da caixa, e não pela largura de frente.
		camera.Size = Mathf.Max(0.15f, box.Size.Length() * 1.1f);
		camera.Position = new Vector3(0, 0.3f, 3f);
		camera.LookAt(Vector3.Zero);
	}
}
