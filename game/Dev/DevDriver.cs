using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Ferramenta de desenvolvimento: percorre uma tela como uma criança faria (clica e digita) e grava
// capturas, para conferir fluxos de interface sem teste manual. Só liga com argumentos de usuário:
//   godot --path game -- --driver=criar --saida=<pasta>
public partial class DevDriver : Node
{
	string scenario = "";
	string outputDir = "";

	public static void AttachIfRequested(Node parent)
	{
		var args = OS.GetCmdlineUserArgs();
		var scenario = args.FirstOrDefault(a => a.StartsWith("--driver="))?["--driver=".Length..];
		if (scenario is null || !OS.IsDebugBuild()) return;
		var output = args.FirstOrDefault(a => a.StartsWith("--saida="))?["--saida=".Length..] ?? ".";
		parent.AddChild(new DevDriver { scenario = scenario, outputDir = output });
	}

	public override async void _Ready()
	{
		await Wait(0.6);
		await Scenarios.Run(this, scenario);
		GetTree().Quit();
	}

	public async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	public async Task Shot(string name)
	{
		await Wait(0.25);
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

static class Scenarios
{
	public static async Task Run(DevDriver d, string name)
	{
		switch (name)
		{
			case "criar":
				await d.Shot("01-criar");
				await d.Press("Criar");
				await d.Shot("02-apelido-vazio");
				await d.Type("Lu@na");
				await d.Press("Criar");
				await d.Shot("03-apelido-invalido");
				await d.Type("Luna");
				await d.Press("Criar");
				await d.Shot("04-lobby");
				break;
			case "escolher":
				await d.Shot("01-lista");
				await d.Press("Editar");
				await d.Shot("02-editar");
				await d.Type("Luninha");
				await d.Press("Salvar");
				await d.Shot("03-lista-depois");
				await d.Press("Theo");
				await d.Shot("04-lobby");
				break;
			case "personagem":
				await d.Shot("01-lobby");
				await d.Press("Personagem");
				await d.Wait(0.8);
				await d.Shot("02-editor-padrao");
				await d.Press("Café");
				await d.Press("Azul");
				await d.Wait(0.5);
				await d.Shot("03-editor-cafe-azul");
				await d.Press("Pronto");
				await d.Shot("04-fechado");
				break;
			case "movimento":
				await d.Shot("01-parado");
				await d.Hold(Controls.Right, 0.8);
				await d.Shot("02-andou-para-a-direita");
				await d.Hold(Controls.Left, 2.6);
				await d.Shot("03-contra-a-arvore");
				await d.Hold(Controls.Up, 4.0);
				await d.Shot("04-contra-a-borda");
				break;
			case "lobby":
				await d.Shot("01-visao-geral");
				break;
			case "album":
				await d.ClickHex(-2, -2);
				await d.Wait(3.2);
				await d.Shot("01-perto-do-album");
				await d.Press("Abrir o Álbum");
				await d.Shot("02-album-criaturas");
				await d.Press("Cosméticos");
				await d.Shot("03-album-cosmeticos");
				await d.Press("Fechar");
				await d.Shot("04-de-volta-ao-lobby");
				break;
			case "portal":
				await d.ClickHex(-1, 4);
				await d.Wait(3.5);
				await d.Shot("01-perto-do-portal");
				await d.Press("Usar o portal");
				await d.Shot("02-portal");
				await d.Press("Fechar");
				await d.Shot("03-de-volta-ao-lobby");
				break;
			case "tecla":
				await d.ClickHex(-2, -2);
				await d.Wait(3.2);
				await d.Tap(Controls.Interact);
				await d.Shot("01-abriu-com-a-tecla");
				await d.PressKey(Key.Escape);
				await d.Shot("02-fechou-com-esc");
				break;
			case "config":
				await d.Press("Configurações");
				await d.Shot("01-jogo");
				await d.Press("Acessibilidade");
				await d.Shot("02-acessibilidade");
				await d.Press("Muito grande");
				await d.Shot("03-texto-muito-grande");
				await d.Press("Fonte para dislexia: Desligado");
				await d.Shot("04-fonte-dislexia");
				await d.Press("Normal");
				await d.Press("Redução de animações: Desligado");
				await d.Press("Modo calmo: Desligado");
				await d.Shot("05-opcoes-ligadas");
				await d.Press("Controles");
				await d.Press("Teclado");
				await d.Shot("06-controles-teclado");
				await d.Press("Fechar");
				await d.Shot("07-lobby-com-dica-de-teclado");
				break;
			case "animacao":
				GD.Print($"Parado: andando={d.Player!.IsWalkingAnimation}");
				Input.ActionPress(Controls.Right);
				await d.Wait(0.5);
				GD.Print($"Andando: andando={d.Player.IsWalkingAnimation}");
				Input.ActionRelease(Controls.Right);
				await d.Wait(0.4);
				GD.Print($"Parou: andando={d.Player.IsWalkingAnimation}");
				break;
			case "boneco":
				await d.ClickHex(4, 0);
				await d.Wait(3.5);
				await d.Shot("01-perto-do-boneco");
				await d.Press("Atacar o Boneco");
				GD.Print($"Tempo na montagem: {Engine.TimeScale}");
				await d.Shot("02-calculadora");
				await d.PressNumberKey();
				await d.Wait(0.8);
				await d.Shot("03-pontilhado");
				await d.Scribble();
				await d.Shot("04-rabisco-nao-passa");
				await d.Press("Apagar");
				await d.TraceGuide();
				await d.Wait(0.8);
				await d.Shot("05-depois-de-desenhar");
				await d.Press("+");
				await d.PressNumberKey();
				await d.Wait(0.8);
				await d.Press("Pular o desenho");
				await d.Wait(0.5);
				await d.Shot("06-conta-montada-sem-resultado");
				await d.Press("Confirmar");
				GD.Print($"Tempo depois de confirmar: {Engine.TimeScale}");
				await d.Wait(0.3);
				await d.Shot("07-resultado");
				break;
			case "boneco-teclado":
				await d.ClickHex(4, 0);
				await d.Wait(3.5);
				await d.PressKey(Key.F);
				GD.Print($"Tempo na montagem: {Engine.TimeScale}");
				var digits = d.NumberKeyDigits();
				await d.PressKey(digits[0]);
				await d.PressKey(Key.Enter);
				await d.Shot("01-numero-sozinho-nao-vale");
				await d.PressKey(Key.Plus);
				await d.PressKey(digits[1]);
				await d.Shot("02-conta-pelo-teclado");
				await d.PressKey(Key.Enter);
				GD.Print($"Tempo depois de confirmar: {Engine.TimeScale}");
				await d.Wait(0.4);
				await d.Shot("03-depois-do-ataque");
				await d.PressKey(Key.F);
				await d.PressKey(Key.Escape);
				GD.Print($"Tempo depois de cancelar: {Engine.TimeScale}");
				break;
			case "boneco-longe":
				await d.PressKey(Key.F);
				GD.Print("Longe do Boneco, F não abre nada.");
				await d.Shot("01-longe");
				break;
			case "nome":
				await d.PressKey(Key.F2);
				await d.Shot("01-montagem-do-nome");
				await d.Press("GA");
				await d.Wait(0.8);
				await d.Shot("02-pontilhado-da-silaba");
				await d.TraceGuide();
				await d.Wait(0.8);
				await d.Press("TO");
				await d.Wait(0.8);
				await d.TraceGuide();
				await d.Wait(0.8);
				await d.Press("Conferir");
				await d.Shot("03-nome-certo");
				break;
			case "mouse":
				await d.ClickHex(3, -4);
				await d.Wait(0.9);
				await d.Shot("01-a-caminho");
				await d.Wait(2.5);
				await d.Shot("02-chegou");
				await d.ClickHex(-3, 1);
				await d.Wait(4.5);
				await d.Shot("03-clique-na-arvore");
				break;
			default:
				GD.PushError($"Piloto: cenário '{name}' não existe.");
				break;
		}
	}
}
