namespace ArquipelagoDoCeu.World;

// O ciclo curto de dia e noite do Lobby. É função só do tempo de jogo que quem chama passa (nunca do relógio do aparelho),
// então nenhuma regra, dificuldade ou recompensa pode depender do horário. Começa no amanhecer.
public sealed class DayCycle(float seconds, bool fixedDay = false)
{
	const float LampsBelow = 0.4f;
	const float DawnShift = MathF.PI / 4f;

	// De 0 (noite) a 1 (pleno dia). Fixar o dia deixa sempre 1.
	public float Daylight(float elapsed)
	{
		if (fixedDay) return 1f;
		var angle = MathF.Tau * (elapsed / seconds) - DawnShift;
		return 0.5f + 0.5f * MathF.Sin(angle);
	}

	public bool LampsOn(float elapsed) => Daylight(elapsed) < LampsBelow;
}
