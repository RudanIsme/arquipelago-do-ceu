using System;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ArquipelagoDoCeu;

public partial class MeshyGallery : Node3D
{
	public sealed record Entry(string Asset, string Name, string Use, string Status, string Note, bool Animated);
	public Action Closed { get; init; } = null!;
	readonly Node3D models = new();
	readonly Camera3D camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Size = 6.8f };
	readonly CanvasLayer ui = new() { Layer = 121 };
	readonly HBoxContainer captions = new() { Position = new Vector2(24, 476), Size = new Vector2(1232, 150) };
	readonly Label pageLabel = new();
	Entry[] entries = [];
	int page;
	bool walking;
	float angle;

	public override void _Ready()
	{
		Position = new Vector3(1000f, 0f, 0f);
		entries = JsonSerializer.Deserialize<Entry[]>(FileAccess.GetFileAsString(MeshyAssets.Folder + "catalogo.json"))!;
		AddChild(models);
		AddChild(camera);
		camera.Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Color.FromHtml("#121D30"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Color.FromHtml("#D8E1F3"), AmbientLightEnergy = 0.7f };
		camera.LookAtFromPosition(GlobalPosition + new Vector3(0f, 4.2f, 10f), GlobalPosition + new Vector3(0f, 1f, 0f), Vector3.Up);
		camera.MakeCurrent();
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45f, -25f, 0f), LightEnergy = 1.5f, ShadowEnabled = true });
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-30f, 145f, 0f), LightEnergy = 0.5f, LightColor = Color.FromHtml("#9EDDDD") });
		AddChild(ui);
		var title = new Label { Text = "ARQUIPÉLAGO DO CÉU  /  ASSETS", Position = new Vector2(28, 20) };
		title.AddThemeFontSizeOverride("font_size", 27);
		ui.AddChild(title);
		ui.AddChild(new Label { Text = "Lote 20 • integração local reversível • 2 versões aprovadas, 2 rascunhos corrigidos e 16 originais provisórios", Position = new Vector2(28, 58), Modulate = Color.FromHtml("#B9C9DC") });
		var tabs = new HBoxContainer { Position = new Vector2(28, 96) };
		ui.AddChild(tabs);
		foreach (var (text, index) in new[] { ("Corrigidos", 0), ("Outras criaturas", 1), ("Cenário I", 2), ("Cenário II", 3), ("Obstáculos", 4), ("Itens", 5) })
			MeshyTestGate.Button(tabs, text, () => ShowPage(index));
		ui.AddChild(captions);
		var controls = new HBoxContainer { Position = new Vector2(28, 650) };
		ui.AddChild(controls);
		MeshyTestGate.Button(controls, "← Anterior", () => ShowPage((page + 5) % 6));
		MeshyTestGate.Button(controls, "Próximo →", () => ShowPage((page + 1) % 6));
		MeshyTestGate.Button(controls, "Idle / caminhada [Espaço]", ToggleWalk);
		MeshyTestGate.Button(controls, "Girar [Q / E]", () => RotateModels(0.4f));
		MeshyTestGate.Button(controls, "Jogar integrado [F7]", Closed);
		controls.AddChild(pageLabel);
		ShowPage(0);
		CaptureIfRequested();
	}

	public void ShowPage(int value)
	{
		page = value;
		angle = 0f;
		foreach (var child in models.GetChildren()) { models.RemoveChild(child); child.QueueFree(); }
		foreach (var child in captions.GetChildren()) { captions.RemoveChild(child); child.QueueFree(); }
		var ranges = new[] { (0, 4), (4, 4), (8, 4), (12, 2), (14, 3), (17, 3) };
		var (start, count) = ranges[page];
		for (var i = 0; i < count; i++) AddEntry(entries[start + i], i, count);
		pageLabel.Text = $"  {page + 1} / 6";
	}

	void AddEntry(Entry entry, int index, int count)
	{
		var x = (index - (count - 1) / 2f) * 3.05f;
		var stand = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.25f, BottomRadius = 1.3f, Height = 0.18f, RadialSegments = 6 }, Position = new Vector3(x, -0.09f, 0f), MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#294357"), Roughness = 0.85f } };
		models.AddChild(stand);
		if (entry.Animated)
			models.AddChild(new MeshyCreature(entry.Asset) { Position = new Vector3(x, 0f, 0f), PreviewWalk = walking });
		else
			MeshyAssets.Add(models, entry.Asset, GlobalPosition + new Vector3(x, 0f, 0f), 2.1f, 2.3f);
		var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(1232f / count - 8f, 0f) };
		captions.AddChild(card);
		var name = new Label { Text = entry.Name, HorizontalAlignment = HorizontalAlignment.Center };
		name.AddThemeFontSizeOverride("font_size", 21);
		card.AddChild(name);
		card.AddChild(new Label { Text = entry.Status, HorizontalAlignment = HorizontalAlignment.Center, Modulate = Color.FromHtml(entry.Status.StartsWith("APROVADO") ? "#90E8CB" : "#FFD293") });
		card.AddChild(new Label { Text = entry.Use + "\n" + entry.Note, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 70), Modulate = Color.FromHtml("#BCCBDC") });
	}

	void ToggleWalk()
	{
		walking = !walking;
		foreach (var model in models.GetChildren().OfType<MeshyCreature>()) model.PreviewWalk = walking;
	}

	void RotateModels(float amount)
	{
		angle += amount;
		foreach (var model in models.GetChildren().OfType<Node3D>().Where(m => m is not MeshInstance3D)) model.Rotation = new Vector3(0f, angle, 0f);
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode == Key.Space) ToggleWalk();
		if (key.Keycode == Key.Q) RotateModels(-0.3f);
		if (key.Keycode == Key.E) RotateModels(0.3f);
		if (key.Keycode == Key.Right) ShowPage((page + 1) % 6);
		if (key.Keycode == Key.Left) ShowPage((page + 5) % 6);
	}

	async void CaptureIfRequested()
	{
		var folder = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--meshy-shots="))?["--meshy-shots=".Length..];
		if (folder is null || DisplayServer.GetName() == "headless") return;
		for (var i = 0; i < 6; i++)
		{
			ShowPage(i);
			await ToSignal(GetTree().CreateTimer(1.4), SceneTreeTimer.SignalName.Timeout);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			GetViewport().GetTexture().GetImage().SavePng($"{folder}/meshy-{i + 1}.png");
		}
		ShowPage(0);
		GD.Print("MESHY_SCREENSHOTS concluídas; jogo continua aberto.");
	}
}
