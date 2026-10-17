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

	public bool HasVisible(string text) => GetTree().Root.FindChildren("*", "Label", true, false).Cast<Label>().Any(l => l.IsVisibleInTree() && l.Text.Contains(text));

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

	public async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

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

	// Aperta a primeira tecla de número habilitada da calculadora (o texto é "3" e os pontos, em duas linhas).
	public async Task PressNumberKey()
	{
		var key = Buttons().FirstOrDefault(b => !b.Disabled && b.Text.Length > 2 && char.IsDigit(b.Text[0]) && b.Text.Contains('\n'));
		if (key is null) GD.PushError("Piloto: nenhuma tecla de número habilitada.");
		else key.EmitSignal(BaseButton.SignalName.Pressed);
		await Wait(0.3);
	}

	// Os números das teclas habilitadas, como teclas do teclado, na ordem da mão.
	public Key[] NumberKeyDigits() =>
		Buttons().Where(b => !b.Disabled && b.Text.Length > 2 && char.IsDigit(b.Text[0]) && b.Text.Contains('\n'))
			.Select(b => (Key)(int)b.Text[0]).ToArray();

	IEnumerable<Button> Buttons() => GetTree().Root.FindChildren("*", "Button", true, false).Cast<Button>().Where(b => b.IsVisibleInTree());

	TracePad? Pad => GetTree().Root.FindChildren("*", "", true, false).OfType<TracePad>().FirstOrDefault(p => p.IsVisibleInTree());

	// Passa o dedo pelo pontilhado, ponto a ponto pelo vizinho mais perto, levantando o dedo nos saltos longos.
	public async Task TraceGuide()
	{
		if (Pad is not { } pad) { GD.PushError("Piloto: não há pontilhado na tela."); return; }
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

	// Um rabisco que passa longe do pontilhado.
	public async Task Scribble()
	{
		if (Pad is not { } pad) { GD.PushError("Piloto: não há pontilhado na tela."); return; }
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

	NamePanel? Names => GetTree().Root.FindChildren("*", "", true, false).OfType<NamePanel>().FirstOrDefault();

	WordPanel? Words => GetTree().Root.FindChildren("*", "", true, false).OfType<WordPanel>().FirstOrDefault();

	// Escreve a palavra do Desafio no teclado, letra por letra (só letras sem acento).
	public async Task TypeWord()
	{
		if (Words is not { } panel) { GD.PushError("Piloto: não há escrita de palavra na tela."); return; }
		foreach (var letter in panel.ExpectedWord) await PressKey((Key)char.ToUpperInvariant(letter));
	}

	// Escolhe as sílabas do nome pelas teclas 1 a 9; com `wrong`, troca a última por uma que não é do nome.
	public async Task TypeName(bool wrong = false)
	{
		if (Names is not { } panel) { GD.PushError("Piloto: não há montagem de nome na tela."); return; }
		var keys = panel.ExpectedName.Select(s => panel.PieceNames.ToList().IndexOf(s)).ToList();
		if (wrong) keys[^1] = panel.PieceNames.ToList().FindIndex(p => !panel.ExpectedName.Contains(p));
		foreach (var index in keys) await PressKey((Key)((int)Key.Key1 + index));
	}

	// Monta uma conta com as quatro maiores peças da mão e ataca: passa do valor de qualquer escudo do 1º ano.
	public async Task OvershootWithKeys()
	{
		var digits = NumberKeyDigits().OrderByDescending(k => (int)k).Take(4).ToList();
		for (var i = 0; i < digits.Count; i++)
		{
			if (i > 0) await PressKey(Key.Plus);
			await PressKey(digits[i]);
		}
		await PressKey(Key.Enter);
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
