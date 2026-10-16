using System;
using System.Collections.Generic;
using System.IO;
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

	// Cada verificação do teste de ponta a ponta que falha entra aqui; o jogo sai com código 1 se houver alguma.
	public int Failures { get; private set; }

	public void Expect(bool ok, string what)
	{
		GD.Print($"{(ok ? "OK" : "FALHOU")}: {what}");
		if (!ok) Failures++;
	}

	public string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?[$"--{name}=".Length..] ?? "";

	public bool HasVisible(string text) => GetTree().Root.FindChildren("*", "Label", true, false).Cast<Label>().Any(l => l.IsVisibleInTree() && l.Text.Contains(text));

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
		try { await Scenarios.Run(this, scenario); }
		catch (System.Exception e)
		{
			GD.PushError($"Piloto: {e}");
			Failures++;
		}
		GetTree().Quit(Failures > 0 ? 1 : 0);
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
			case "andar":
				await FloorShots.Run(d);
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
			case "boneco-dislexia":
				if (name == "boneco-dislexia")
				{
					await d.Press("Configurações");
					await d.Press("Acessibilidade");
					await d.Press("Muito grande");
					await d.Press("Fonte para dislexia: Desligado");
					await d.Press("Fechar");
				}
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Shot("01-escolha-do-treino");
				await d.Press("Começar");
				GD.Print($"Tempo com o alvo escolhido: {Engine.TimeScale}");
				await d.Shot("02-alvo-escolhido");
				await d.PressNumberKey();
				await d.Wait(1.0);
				GD.Print($"Tempo desenhando: {Engine.TimeScale}");
				await d.Shot("03-pontilhado");
				await d.TraceGuide();
				await d.Wait(0.8);
				await d.Shot("04-depois-de-desenhar");
				break;
			case "erro":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				await d.Shot("01-comeco");
				await d.OvershootWithKeys();
				await d.Shot("02-passou-do-valor-travessura");
				await d.Wait(5.0);
				await d.Shot("03-peca-voltou");
				await d.OvershootWithKeys();
				await d.Shot("04-segundo-erro-com-dica");
				await d.Press("Dica");
				await d.Shot("05-pediu-a-dica");
				break;
			case "energia":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				for (var i = 0; i < 5; i++)
				{
					await d.OvershootWithKeys();
					await d.Shot($"erro-{i + 1}");
				}
				await d.Wait(0.5);
				await d.Shot("saiu-do-treino");
				break;
			case "bolha":
				await d.GoNearDummy(4, -2);
				await d.ClickCreature();
				await d.Press("Começar");
				await d.PressKey(Key.Escape);
				await d.Wait(7.7);
				await d.Shot("01-aviso");
				await d.Wait(1.7);
				await d.Shot("02-bolha-no-ar");
				await d.Wait(6.5);
				await d.Shot("03-depois-da-bolha");
				break;
			case "calmo":
				await d.Press("Configurações");
				await d.Press("Acessibilidade");
				await d.Press("Modo calmo: Desligado");
				await d.Press("Fechar");
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				await d.PressKey(Key.Escape);
				await d.Wait(9.5);
				await d.Shot("01-sem-bolha-no-modo-calmo");
				break;
			case "nome":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Shot("01-escolha-do-treino");
				await d.Press("Começar");
				await d.Shot("02-montagem-do-nome");
				await d.TypeName();
				await d.Shot("03-nome-montado");
				await d.Press("Conferir");
				await d.Shot("04-nome-certo");
				await d.Wait(3.5);
				await d.Shot("05-novo-nome");
				break;
			case "nome-erro":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Press("Começar");
				await d.TypeName(wrong: true);
				await d.Press("Conferir");
				await d.Shot("01-silaba-errada-travessura");
				await d.Wait(5.0);
				await d.Shot("02-peca-voltou");
				break;
			case "treino-nivel":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Shot("01-so-o-nivel-1");
				await d.Press("5 · Muito difícil");
				await d.Press("Começar");
				await d.Shot("02-sem-desafios-nessa-dificuldade");
				break;
			case "nivel2-matematica":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Nível 2");
				await d.Shot("01-escolha-nivel-2");
				await d.Press("Começar");
				await d.Shot("02-dezenas-na-mao");
				await d.PressKey(Key.Key1);
				await d.PressKey(Key.Key0);
				await d.Shot("03-dez-na-conta");
				await d.PressKey(Key.Minus);
				await d.Shot("04-sinal-de-menos");
				break;
			case "nivel2-escrita":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Press("Nível 2");
				await d.Press("Começar");
				await d.Shot("01-teclado-na-tela");
				await d.Press("Sugerir letra");
				await d.Shot("02-letra-sugerida");
				await d.TypeWord();
				await d.Shot("03-palavra-escrita");
				await d.PressKey(Key.Enter);
				await d.Shot("04-palavra-certa");
				break;
			case "nivel3-frase":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Press("Nível 3");
				await d.Press("Começar");
				await d.Shot("01-frase");
				await d.TypeName();
				await d.Shot("02-frase-montada");
				await d.PressKey(Key.Enter);
				await d.Shot("03-frase-certa");
				break;
			case "camera":
				await d.Shot("01-zoom-padrao");
				for (var i = 0; i < 3; i++) await d.PressKey(Key.Pageup);
				await d.Wait(1.2);
				await d.Shot("02-zoom-perto");
				await d.ClickHex(-4, 3);
				await d.Wait(4.5);
				await d.Shot("03-camera-seguiu-o-personagem");
				for (var i = 0; i < 8; i++) await d.PressKey(Key.Pagedown);
				await d.Wait(1.2);
				await d.Shot("04-zoom-longe");
				await d.PressKey(Key.Home);
				await d.Wait(1.2);
				await d.Shot("05-zoom-reiniciado");
				break;
			case "efeitos":
				await d.Shot("01-com-efeitos");
				await d.Press("Configurações");
				await d.Press("Acessibilidade");
				await d.Press("Efeitos visuais reduzidos: Desligado");
				await d.Press("Fechar");
				await d.Shot("02-efeitos-reduzidos");
				break;
			case "e2e-1":
				await E2E.First(d);
				break;
			case "e2e-2":
				await E2E.Reopen(d);
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

// Roteiro de ponta a ponta, feito para rodar no executável do build (CI): cria o Perfil, anda, treina nos dois Papéis e
// confere que o save não mudou; depois um segundo processo reabre o jogo e confere que o Perfil e o progresso continuam iguais.
// Uso: <jogo> --headless -- --driver=e2e-1 --save=<arquivo>, e depois o mesmo com --driver=e2e-2.
static class E2E
{
	public static async Task First(DevDriver d)
	{
		var save = d.Arg("save");
		d.Expect(save != "", "o roteiro recebeu --save=<arquivo>");

		await d.Press("Criar");
		await d.Type("Luna");
		await d.Press("Criar");
		d.Expect(d.Player is { Enabled: true } && d.Button("Comparar") is not null, "o Perfil foi criado e o Lobby abriu");
		d.Expect(File.Exists(save), "o Perfil foi gravado no save");
		var before = File.ReadAllBytes(save);

		var start = d.Player!.GroundPosition;
		// Sem mouse: o teste anda e escolhe a criatura só com ações, que funcionam também sem janela (--headless).
		await d.Hold(Controls.Down, 1.5);
		d.Expect(System.Numerics.Vector2.Distance(start, d.Player.GroundPosition) > 0.5f, "o Personagem andou");

		await d.Tap(Controls.Target);
		d.Expect(d.Button("Começar") is not null, "a escolha do treino abriu");
		await d.Press("Começar");
		d.Expect(d.Button("Atacar") is not null, "o treino de Matemática começou");
		await d.PressNumberKey();
		await d.PressKey(Key.Escape);
		await d.Press("Sair do treino");

		await d.Tap(Controls.Target);
		await d.Press("Escriba");
		await d.Press("Começar");
		d.Expect(d.Button("Conferir") is not null, "o treino de Português começou");
		await d.TypeName();
		await d.PressKey(Key.Enter);
		d.Expect(d.HasVisible("libertada") || d.HasVisible("certa"), "o nome certo libertou a criatura");
		await d.Press("Sair do treino");

		d.Expect(File.ReadAllBytes(save).AsSpan().SequenceEqual(before), "treinar nos dois Papéis não mudou o save");
		File.WriteAllBytes(save + ".snapshot", File.ReadAllBytes(save));
	}

	public static async Task Reopen(DevDriver d)
	{
		var save = d.Arg("save");
		await d.Wait(0.5);
		d.Expect(d.Player is { Enabled: true } && d.Button("Comparar") is not null, "o Perfil foi aberto sozinho ao reabrir o jogo");
		d.Expect(d.HasVisible("Luna"), "o apelido do Perfil continua o mesmo");
		d.Expect(File.Exists(save + ".snapshot") && File.ReadAllBytes(save).AsSpan().SequenceEqual(File.ReadAllBytes(save + ".snapshot")), "o progresso continua idêntico depois de fechar e reabrir");
	}
}
