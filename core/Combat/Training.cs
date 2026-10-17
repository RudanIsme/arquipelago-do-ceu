using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// `Equation` é a conta como a criança a montou, com o resultado: aparece depois de confirmar, mesmo quando passa do valor.
public sealed record AttackResult(ExpressionResult Expression, HitOutcome Outcome, int Remaining, string Equation);

// A luta com o Boneco de treino. O escudo é o alvo de uma Missão de compor número; a mão e o cajado
// são os mesmos das Dungeons. Nada aqui toca em save: o treino não conta para o progresso.
public sealed class TrainingBattle : IBattle
{
	readonly IReadOnlyList<Mission> missions;
	readonly Pcg32 rng;
	readonly MissionHistory? history;

	TrainingBattle(CombatRules rules, IReadOnlyList<Mission> missions, MathBag bag, Pcg32 rng, MissionHistory? history)
	{
		this.missions = missions;
		this.rng = rng;
		this.history = history;
		Bag = bag;
		Staff = new Staff(bag, rules);
		Mission = missions[0];
		Shield = new Shield(0);
		Choose(missions);
	}

	public MathBag Bag { get; }

	public Staff Staff { get; }

	public Mission Mission { get; private set; }

	public Shield Shield { get; private set; }

	public int StartingShield { get; private set; }

	public static Result<TrainingBattle> Start(CombatRules rules, IEnumerable<Mission> available, Pcg32 rng, MissionHistory? history = null)
	{
		var usable = available.Where(m => m.Mechanic == "compor_numero").ToList();
		return usable.Count == 0
			? Result<TrainingBattle>.Fail("Não há Missão de compor número para o Boneco.")
			: Result<TrainingBattle>.Ok(new TrainingBattle(rules, usable, new MathBag(rules, rng), rng, history));
	}

	public void Reveal() => Bag.Reveal();

	public Prank PlayPrank() => Bag.PlayPrank();

	// Novo escudo, de outra Missão quando houver mais de uma.
	public void Next()
	{
		var others = missions.Where(m => m != Mission).ToList();
		Choose(others.Count > 0 ? others : missions);
	}

	void Choose(IReadOnlyList<Mission> candidates)
	{
		var pool = MissionHistory.Narrow(history, candidates);
		Mission = pool[rng.NextInt(pool.Count)];
		history?.Add(Mission.Id);
		StartingShield = Mission.Params["target"].GetInt32();
		Shield = new Shield(StartingShield);
	}

	public Result<AttackResult> Confirm()
	{
		// Confirmar passando do valor devolve as peças ao cajado, então a conta é guardada antes.
		var assembled = string.Join(" ", Staff.Tokens);
		var result = Staff.Confirm(Shield.Remaining);
		if (!result.IsOk) return Result<AttackResult>.Fail(result.Error!);

		var outcome = Shield.Hit(result.Value!);
		return Result<AttackResult>.Ok(new AttackResult(result.Value!, outcome, Shield.Remaining, $"{assembled} = {result.Value!.Total}"));
	}
}
