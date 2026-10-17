using ArquipelagoDoCeu.Generation;
using Godot;

namespace ArquipelagoDoCeu;

// Cada luta sorteia a partir do relógio do motor: é a única fonte de acaso do jogo fora da seed do mundo.
public sealed class GodotRandomSource : IRandomSource
{
	public Pcg32 NewStream() => new(Time.GetTicksUsec());
}
