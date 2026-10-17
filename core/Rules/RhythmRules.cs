namespace ArquipelagoDoCeu.Rules;

// O ritmo de ataque de um tipo de criatura: de quanto em quanto tempo o golpe sai e quanto dura o aviso antes dele.
public sealed record CreatureRhythm(string Kind, float IntervalSeconds, float WarningSeconds);

// O ritmo do combate, em dados: a recarga curta do jogador entre uma resposta e a próxima e o ritmo de cada tipo de criatura.
public sealed record RhythmRules(float PlayerRechargeSeconds, IReadOnlyList<CreatureRhythm> Creatures)
{
	public const string DefaultKind = "dummy";

	// O ritmo do tipo pedido; um tipo sem linha própria segue o do Boneco.
	public CreatureRhythm For(string kind) =>
		Creatures.FirstOrDefault(c => c.Kind == kind) ?? Creatures.First(c => c.Kind == DefaultKind);

	// A bolha do tipo: o intervalo e o aviso são os dele; velocidade, raio e alcance são iguais para todos.
	public BubbleRules BubbleFor(string kind, BubbleRules shared)
	{
		var rhythm = For(kind);
		return shared with { IntervalSeconds = rhythm.IntervalSeconds, WarningSeconds = rhythm.WarningSeconds };
	}
}

sealed record PlayerRhythmDto(float? RechargeSeconds);

sealed record CreatureRhythmDto(string? Kind, float? IntervalSeconds, float? WarningSeconds);

sealed record RhythmDto(PlayerRhythmDto? Player, CreatureRhythmDto[]? Creatures);

static class RhythmChecks
{
	const float MaxRecharge = 5f;

	public static string? Check(RhythmDto? rhythm)
	{
		if (rhythm is null) return "Regras inválidas: o campo 'combat.rhythm' é obrigatório.";
		if (rhythm.Player?.RechargeSeconds is not { } recharge) return "Regras inválidas: o campo 'combat.rhythm.player.rechargeSeconds' é obrigatório.";
		if (recharge < 0f || recharge > MaxRecharge) return $"Regras inválidas: 'combat.rhythm.player.rechargeSeconds' deve estar entre 0 e {MaxRecharge} (veio {recharge}).";
		if (rhythm.Creatures is not { Length: > 0 } creatures) return "Regras inválidas: 'combat.rhythm.creatures' precisa de ao menos um tipo de criatura.";
		return creatures.Select(Problem).FirstOrDefault(p => p is not null) ?? Whole(creatures);
	}

	static string? Whole(CreatureRhythmDto[] creatures)
	{
		if (creatures.Select(c => c.Kind).Distinct().Count() != creatures.Length) return "Regras inválidas: 'combat.rhythm.creatures' repete um tipo de criatura.";
		return creatures.Any(c => c.Kind == RhythmRules.DefaultKind) ? null : $"Regras inválidas: 'combat.rhythm.creatures' precisa do tipo '{RhythmRules.DefaultKind}', que vale para os outros.";
	}

	static string? Problem(CreatureRhythmDto creature)
	{
		if (string.IsNullOrWhiteSpace(creature.Kind)) return "Regras inválidas: um tipo de 'combat.rhythm.creatures' está sem 'kind'.";
		var where = $"combat.rhythm.creatures[{creature.Kind}]";
		var interval = creature.IntervalSeconds ?? 0f;
		var warning = creature.WarningSeconds ?? 0f;
		if (interval is < 2f or > 60f) return $"Regras inválidas: '{where}' precisa de 'intervalSeconds' entre 2 e 60 (combat.rhythm.creatures).";
		if (warning is < 0.5f or > 5f) return $"Regras inválidas: '{where}' precisa de 'warningSeconds' entre 0,5 e 5 (combat.rhythm.creatures).";
		return warning >= interval ? $"Regras inválidas: o aviso de '{where}' precisa ser menor que o intervalo (combat.rhythm.creatures)." : null;
	}

	public static RhythmRules Build(RhythmDto rhythm) =>
		new(rhythm.Player!.RechargeSeconds!.Value, [.. rhythm.Creatures!.Select(c => new CreatureRhythm(c.Kind!, c.IntervalSeconds!.Value, c.WarningSeconds!.Value))]);
}
