using Godot;

namespace ArquipelagoDoCeu;

// Camada de entrada: o jogo só conhece ações ("move_up"...), nunca teclas. Trocar ou somar um
// dispositivo (controle, toque) é mexer aqui, sem tocar em quem reage à ação.
public static class Controls
{
	public const string Up = "move_up";
	public const string Down = "move_down";
	public const string Left = "move_left";
	public const string Right = "move_right";
	public const string Run = "run";
	public const string Interact = "interact";
	public const string Target = "target";
	public const string ZoomIn = "zoom_in";
	public const string ZoomOut = "zoom_out";
	public const string ZoomReset = "zoom_reset";
	public const string Inventory = "inventory";

	// Verdadeiro durante o gesto de pinça: os dedos que afastam ou aproximam não mandam o Personagem andar.
	public static bool Pinching { get; set; }

	public static void Register()
	{
		Bind(Up, Key.W, Key.Up);
		Bind(Down, Key.S, Key.Down);
		Bind(Left, Key.A, Key.Left);
		Bind(Right, Key.D, Key.Right);
		// Só a tecla E: Espaço e Enter já acionam o botão em foco.
		Bind(Interact, Key.E);
		Bind(Target, Key.F, Key.Tab);
		Bind(ZoomIn, Key.Pageup);
		Bind(ZoomOut, Key.Pagedown);
		Bind(ZoomReset, Key.Home);
		Bind(Inventory, Key.I);
		Bind(Run, Key.Shift);

	}

	// Um contador, não um sinalizador: se um painel novo abre no quadro em que o velho sai, o aviso não se perde.
	// Com a criança escrevendo letras, W A S D são letras e só as setas andam.
	public static int TypingPanels { get; set; }

	static bool TypingLetters => TypingPanels > 0;

	public static Vector2 Direction()
	{
		if (!TypingLetters) return Godot.Input.GetVector(Left, Right, Down, Up);
		static float Axis(Key positive, Key negative) => (Godot.Input.IsPhysicalKeyPressed(positive) ? 1f : 0f) - (Godot.Input.IsPhysicalKeyPressed(negative) ? 1f : 0f);
		return new Vector2(Axis(Key.Right, Key.Left), Axis(Key.Up, Key.Down)).LimitLength(1f);
	}

	static void Bind(string action, params Key[] keys)
	{
		if (!InputMap.HasAction(action)) InputMap.AddAction(action);
		// Tecla física: WASD continua no lugar certo em teclados que não são QWERTY.
		foreach (var key in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
	}
}

// Dicas de controle que aparecem na tela, conforme a escolha nas configurações. Qualquer forma de
// jogar continua valendo; a escolha só muda o que a tela ensina.
public static class ControlHints
{
	public static string For(string controls) => controls switch
	{
		"teclado" => "Use as setas ou W A S D para andar, Shift para correr, E para usar um objeto, F para escolher a criatura mais perto e Page Up e Page Down para o zoom.",
		"toque" => "Toque no chão para andar. Toque no botão que aparece para usar um objeto. Toque numa criatura para lutar. Use dois dedos para o zoom.",
		_ => "Clique no chão para andar; segure Shift para correr. Clique no botão que aparece para usar um objeto. Clique numa criatura para lutar. A roda do mouse faz o zoom.",

	};
}
