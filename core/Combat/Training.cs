using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

public sealed record AttackResult(ExpressionResult Expression, HitOutcome Outcome, int Remaining);

// A luta com o Boneco de treino. O escudo é o alvo de uma Missão de compor número; a mão e o cajado
// são os mesmos das Dungeons. Nada aqui toca em save: o treino não conta para o progresso.
public sealed class TrainingBattle
{
	readonly IReadOnlyList<Mission> missions;
	readonly Pcg32 rng;

	TrainingBattle(CombatRules rules, IReadOnlyList<Mission> missions, MathBag bag, Pcg32 rng)
	{
		this.missions = missions;
		this.rng = rng;
		Bag = bag;
		Staff = new Staff(bag, rules);
		Mission = missions[0];
		Shield = new Shield(0);
		Next();
	}

	public MathBag Bag { get; }

	public Staff Staff { get; }

	public Mission Mission { get; private set; }

	public Shield Shield { get; private set; }

	public int StartingShield { get; private set; }

	public static Result<TrainingBattle> Start(CombatRules rules, IEnumerable<Mission> available, Pcg32 rng)
	{
		var usable = available.Where(m => m.Mechanic == "compor_numero").ToList();
		return usable.Count == 0
			? Result<TrainingBattle>.Fail("Não há Missão de compor número para o Boneco.")
			: Result<TrainingBattle>.Ok(new TrainingBattle(rules, usable, new MathBag(rules, rng), rng));
	}

	// Novo escudo, de outra Missão quando houver mais de uma.
	public void Next()
	{
		var others = missions.Where(m => m != Mission).ToList();
		var pool = others.Count > 0 ? others : missions;
		Mission = pool[rng.NextInt(pool.Count)];
		StartingShield = Mission.Params["target"].GetInt32();
		Shield = new Shield(StartingShield);
	}

	public Result<AttackResult> Confirm()
	{
		var result = Staff.Confirm(Shield.Remaining);
		if (!result.IsOk) return Result<AttackResult>.Fail(result.Error!);

		var outcome = Shield.Hit(result.Value!);
		return Result<AttackResult>.Ok(new AttackResult(result.Value!, outcome, Shield.Remaining));
	}
}
