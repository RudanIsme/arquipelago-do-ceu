namespace ArquipelagoDoCeu.World;

// O ciclo curto de dia e noite do Lobby. É função só do tempo de jogo que quem chama passa (nunca do relógio do aparelho),
// então nenhuma regra, dificuldade ou recompensa pode depender do horário.
public sealed class DayCycle(float seconds, bool fixedDay = false)
{
	const float LampsBelow = 0.4f;
	// Começa de manhã, com a luz já alta (0,8), e dali segue para o meio-dia, o entardecer e a noite.
	const float MorningAngle = 0.6435f;

	// De 0 (noite) a 1 (pleno dia). Fixar o dia deixa sempre 1.
	public float Daylight(float elapsed)
	{
		if (fixedDay) return 1f;
		var angle = MathF.Tau * (elapsed / seconds) + MorningAngle;
		return 0.5f + 0.5f * MathF.Sin(angle);
	}

	public bool LampsOn(float elapsed) => Daylight(elapsed) < LampsBelow;
}
