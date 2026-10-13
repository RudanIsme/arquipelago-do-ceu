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

	public static void Register()
	{
		Bind(Up, Key.W, Key.Up);
		Bind(Down, Key.S, Key.Down);
		Bind(Left, Key.A, Key.Left);
		Bind(Right, Key.D, Key.Right);
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
