using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

public partial class MeshyTestGate : Node
{
	public Main Game { get; init; } = null!;
	readonly CanvasLayer tools = new() { Layer = 120 };
	readonly List<CanvasLayer> hidden = new();
	MeshyGallery? gallery;
	Camera3D? previousCamera;
	bool previousVisible;
	ProcessModeEnum previousMode;

	public override void _Ready()
	{
		AddChild(tools);
		var bar = new HBoxContainer { Position = new Vector2(14f, 670f) };
		tools.AddChild(bar);
		Button(bar, "Assets [F7]", Toggle);
		Button(bar, "Torre dos Números", () => Game.OpenMeshyTower("matematica"));
		Button(bar, "Torre das Letras", () => Game.OpenMeshyTower("portugues"));
		bar.AddChild(new Label { Text = "  TESTE • 16 originais provisórios + 2 rascunhos • save separado", Modulate = Color.FromHtml("#FFE0A0") });
		DisplayServer.WindowMoveToForeground();
		Toggle();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F7 })
		{
			Toggle();
			GetViewport().SetInputAsHandled();
		}
	}

	public void Toggle()
	{
		if (gallery is not null)
		{
			gallery.QueueFree();
			gallery = null;
			Game.Visible = previousVisible;
			Game.ProcessMode = previousMode;
			foreach (var layer in hidden) layer.Show();
			hidden.Clear();
			previousCamera?.MakeCurrent();
			tools.Show();
			return;
		}
		previousCamera = GetViewport().GetCamera3D();
		previousVisible = Game.Visible;
		previousMode = Game.ProcessMode;
		Game.Visible = false;
		Game.ProcessMode = ProcessModeEnum.Disabled;
		foreach (var layer in Game.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(c => c.Visible))
		{
			hidden.Add(layer);
			layer.Hide();
		}
		tools.Hide();
		gallery = new MeshyGallery { Closed = Toggle };
		AddChild(gallery);
	}

	public static Button Button(Node parent, string text, System.Action action)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(0f, 38f) };
		button.Pressed += action;
		parent.AddChild(button);
		return button;
	}
}
