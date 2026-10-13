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
	public const string Interact = "interact";
	public const string Attack = "attack";

	public static void Register()
	{
		Bind(Up, Key.W, Key.Up);
		Bind(Down, Key.S, Key.Down);
		Bind(Left, Key.A, Key.Left);
		Bind(Right, Key.D, Key.Right);
		// Só a tecla E: Espaço e Enter já acionam o botão em foco.
		Bind(Interact, Key.E);
		Bind(Attack, Key.F);
	}

	// x para a direita, y para cima, tamanho no máximo 1.
	public static Vector2 Direction() => Godot.Input.GetVector(Left, Right, Down, Up);

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
		"teclado" => "Use as setas ou W A S D para andar e a tecla E para usar um objeto.",
		"toque" => "Toque no chão para andar. Toque no botão que aparece para usar um objeto.",
		_ => "Clique no chão para andar. Clique no botão que aparece para usar um objeto.",
	};
}
