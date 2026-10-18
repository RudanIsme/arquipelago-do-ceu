using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Ferramenta de desenvolvimento: percorre uma tela como uma criança e grava capturas. Só liga com argumentos de usuário:
//   godot --path game -- --driver=criar --saida=<pasta>
public partial class DevDriver : Node
{
	string scenario = "";
	string outputDir = "";

	public PilotPort Port { get; private init; } = null!;

	// Cada verificação do teste de ponta a ponta que falha entra aqui; o jogo sai com código 1 se houver alguma.
	public int Failures { get; private set; }

	public void Expect(bool ok, string what)
	{
		GD.Print($"{(ok ? "OK" : "FALHOU")}: {what}");
		if (!ok) Failures++;
	}

	public string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?[$"--{name}=".Length..] ?? "";

	// Texto na tela, em rótulo da interface ou no balão 3D da criatura.
	public bool HasVisible(string text) =>
		GetTree().Root.FindChildren("*", "Label", true, false).Cast<Label>().Any(l => l.IsVisibleInTree() && l.Text.Contains(text))
		|| GetTree().Root.FindChildren("*", "Label3D", true, false).Cast<Label3D>().Any(l => l.IsVisibleInTree() && l.Text.Contains(text));

	public static void AttachIfRequested(Node parent, PilotPort port)
	{
		var args = OS.GetCmdlineUserArgs();
		var scenario = args.FirstOrDefault(a => a.StartsWith("--driver="))?["--driver=".Length..];
		if (scenario is null || !OS.IsDebugBuild()) return;
		var output = args.FirstOrDefault(a => a.StartsWith("--saida="))?["--saida=".Length..] ?? ".";
		parent.AddChild(new DevDriver { scenario = scenario, outputDir = output, Port = port });
	}

	public override async void _Ready()
	{
		await Wait(0.6);
		try { await Scenarios.Run(this, scenario); }
		catch (System.Exception e)
		{
			GD.PushError($"Piloto: {e}");
			Failures++;
		}
		await GetParent<Main>().QuitAsync(Failures > 0 ? 1 : 0);
	}

	// Em tempo de relógio: o jogo desacelera enquanto a criança monta a resposta, e o piloto não pode esperar quatro vezes mais por isso.
	public async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);

	public async Task Shot(string name)
	{
		await Wait(0.25);
		// Sem janela (--headless) não há imagem para gravar.
		if (DisplayServer.GetName() == "headless") return;
		GetViewport().GetTexture().GetImage().SavePng($"{outputDir}/{name}.png");
	}

	public Button? Button(string text) =>
		GetTree().Root.FindChildren("*", "Button", true, false).Cast<Button>().FirstOrDefault(b => b.Text == text && b.IsVisibleInTree());

	public async Task Press(string text)
	{
		var button = Button(text);
		if (button is null) GD.PushError($"Piloto: botão '{text}' não encontrado.");
		else
		{
			if (button.ToggleMode) button.ButtonPressed = true;
			button.EmitSignal(BaseButton.SignalName.Pressed);
		}
		await Wait(0.3);
	}


	// Clica na criatura, como o mouse faria.
	public async Task ClickCreature()
	{
		var dummy = GetTree().Root.FindChildren("*", "", true, false).OfType<TrainingDummy>().First();
		var screen = GetViewport().GetCamera3D().UnprojectPosition(dummy.Center);
		Click(screen, true);
		Click(screen, false);
		await Wait(0.4);
	}

	public TracePad? Pad => GetTree().Root.FindChildren("*", "", true, false).OfType<TracePad>().FirstOrDefault(p => p.IsVisibleInTree());

	// Passa o dedo pelo trilha, ponto a ponto pelo vizinho mais perto, levantando o dedo nos saltos longos.
	public async Task TraceGuide()
	{
		if (Pad is not { } pad) { GD.PushError("Piloto: não há trilha na tela."); return; }
		var remaining = pad.GuideOnScreen().ToList();
		var current = remaining.OrderBy(p => p.Y).ThenBy(p => p.X).First();
		var down = false;
		while (remaining.Count > 0)
		{
			var next = remaining.OrderBy(p => p.DistanceTo(current)).First();
			remaining.Remove(next);
			if (down && next.DistanceTo(current) > 40f) { Click(current, false); down = false; }
			if (!down) { Click(next, true); down = true; }
			else Input.ParseInputEvent(new InputEventMouseMotion { Position = next, GlobalPosition = next, ButtonMask = MouseButtonMask.Left });
			current = next;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		if (down) Click(current, false);
		await Wait(0.2);
	}

	// Um rabisco que passa longe do trilha.
	public async Task Scribble()
	{
		if (Pad is not { } pad) { GD.PushError("Piloto: não há trilha na tela."); return; }
		var dots = pad.GuideOnScreen();
		var origin = new Vector2(dots.Min(p => p.X) - 60, dots.Min(p => p.Y) - 20);
		Click(origin, true);
		for (var i = 1; i <= 12; i++)
		{
			Input.ParseInputEvent(new InputEventMouseMotion { Position = origin + new Vector2(0, i * 18), GlobalPosition = origin + new Vector2(0, i * 18), ButtonMask = MouseButtonMask.Left });
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		Click(origin + new Vector2(0, 12 * 18), false);
		await Wait(0.3);
	}

	static void Click(Vector2 position, bool pressed) =>
		Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = pressed });


	// Anda até perto do Boneco, como a criança faria.
	public async Task GoNearDummy(int q = 4, int r = 0)
	{
		await ClickHex(q, r);
		await Wait(3.5);
	}

	public Player? Player => GetTree().Root.FindChild("Player", true, false) as Player;

	// Segura uma ação de movimento, como uma tecla pressionada.
	public async Task Hold(string action, double seconds)
	{
		Input.ActionPress(action);
		await Wait(seconds);
		Input.ActionRelease(action);
	}

	// Clica no centro de um hexágono do Lobby, como o mouse faria.
	public async Task ClickHex(int q, int r)
	{
		var camera = GetViewport().GetCamera3D();
		var screen = camera.UnprojectPosition(Lobby.HexToWorld(q, r));
		Input.ParseInputEvent(new InputEventMouseButton { Position = screen, GlobalPosition = screen, ButtonIndex = MouseButton.Left, Pressed = true });
		await Wait(0.1);
	}

	// Aperta e solta uma ação por um instante, como um toque de tecla.
	public async Task Tap(string action)
	{
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
		await Wait(0.1);
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
		await Wait(0.3);
	}

	public async Task PressKey(Key key)
	{
		var unicode = (int)key is >= 32 and <= 126 ? (uint)key : 0u;
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, Pressed = true });
		await Wait(0.1);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
		await Wait(0.3);
	}

	public async Task Type(string text)
	{
		var input = GetTree().Root.FindChildren("*", "LineEdit", true, false).Cast<LineEdit>().FirstOrDefault(l => l.IsVisibleInTree());
		if (input is null) GD.PushError("Piloto: campo de texto não encontrado.");
		else input.Text = text;
		await Wait(0.2);
	}
}
