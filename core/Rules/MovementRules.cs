namespace ArquipelagoDoCeu.Rules;

// A velocidade de caminhada da criança: nenhuma criatura anda mais depressa que isto.
public static class ChildSpeed
{
	public const float Walk = 1.1f;
}

// Como as criaturas e o Boneco se movem pela Sala, em dados: patrulham perto de casa, se aproximam devagar quando a criança chega
// perto, desistem depois de `GiveUpSeconds` e só voltam a se aproximar depois de `CooldownSeconds`. Sempre mais lentas que a criança.
public sealed record MovementRules(float PatrolSpeed, float ApproachSpeed, float PatrolRadius, float ApproachRadius, float StopDistance,
	float GiveUpSeconds, float PauseSeconds, float CooldownSeconds);

sealed record MovementDto(float? PatrolSpeed, float? ApproachSpeed, float? PatrolRadius, float? ApproachRadius, float? StopDistance,
	float? GiveUpSeconds, float? PauseSeconds, float? CooldownSeconds);

static class MovementChecks
{
	public static string? Check(MovementDto? movement)
	{
		if (movement is null) return "Regras inválidas: o campo 'combat.movement' é obrigatório.";
		var fields = new (string Name, float? Value, float Min, float Max)[]
		{
			("patrolSpeed", movement.PatrolSpeed, 0.05f, ChildSpeed.Walk),
			("approachSpeed", movement.ApproachSpeed, 0.05f, ChildSpeed.Walk),
			("patrolRadius", movement.PatrolRadius, 0f, 6f),
			("approachRadius", movement.ApproachRadius, 1f, 15f),
			("stopDistance", movement.StopDistance, 1f, 15f),
			("giveUpSeconds", movement.GiveUpSeconds, 2f, 60f),
			("pauseSeconds", movement.PauseSeconds, 0f, 20f),
			("cooldownSeconds", movement.CooldownSeconds, 0f, 60f),
		};
		foreach (var (name, value, min, max) in fields)
		{
			if (value is null) return $"Regras inválidas: o campo 'combat.movement.{name}' é obrigatório.";
			if (value < min || value > max) return $"Regras inválidas: 'combat.movement.{name}' deve estar entre {min} e {max} (veio {value}).";
		}
		// A criatura é sempre mais lenta que a criança: nunca alcança quem anda, e a criança sai quando quiser.
		if (movement.PatrolSpeed >= ChildSpeed.Walk || movement.ApproachSpeed >= ChildSpeed.Walk) return $"Regras inválidas: 'combat.movement' precisa de velocidades menores que a da criança ({ChildSpeed.Walk}).";
		if (movement.PatrolSpeed > movement.ApproachSpeed) return "Regras inválidas: 'combat.movement.patrolSpeed' não pode passar de 'approachSpeed'.";
		return movement.StopDistance > movement.ApproachRadius ? "Regras inválidas: 'combat.movement.stopDistance' não pode passar de 'approachRadius'." : null;
	}

	public static MovementRules Build(MovementDto m) =>
		new(m.PatrolSpeed!.Value, m.ApproachSpeed!.Value, m.PatrolRadius!.Value, m.ApproachRadius!.Value, m.StopDistance!.Value,
			m.GiveUpSeconds!.Value, m.PauseSeconds!.Value, m.CooldownSeconds!.Value);
}
