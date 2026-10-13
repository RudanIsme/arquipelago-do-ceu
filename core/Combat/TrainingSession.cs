using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// O que a criança escolhe antes de treinar: o Papel (pela disciplina das Missões), o Nível (ano) e a dificuldade.
public sealed record TrainingSetup(string Discipline, int Year, int Difficulty)
{
	public const string Mathematics = "matematica";
	public const string Portuguese = "portugues";
	public const int LastLevel = 3;
	public const int LastDifficulty = 5;

	// Só os Níveis a que o Perfil já tem acesso: o treino nunca libera um Nível.
	public static IReadOnlyList<int> Levels(int unlockedLevel) => Enumerable.Range(1, Math.Clamp(unlockedLevel, 1, LastLevel)).ToList();
}

// O que um erro causou. `Empty` é Energia zerada: a criança sai da Área de treino.
public sealed record MistakeOutcome(bool Empty, bool HintOffered, Prank? Prank);

// Uma sessão de treino contra o Boneco: Energia própria, erros seguidos e dicas. Nada aqui toca em save,
// porque o treino não conta para o progresso (e `ArquiteturaTests` garante que o combate não conhece o save).
public sealed class TrainingSession
{
	readonly CombatRules rules;
	int hintIndex;

	TrainingSession(CombatRules rules, int maxEnergy, TrainingSetup setup, TrainingBattle? math, NameBattle? name)
	{
		this.rules = rules;
		Energy = new Energy(maxEnergy);
		Setup = setup;
		Math = math;
		Name = name;
	}

	public Energy Energy { get; }

	public TrainingSetup Setup { get; }

	// Só um dos dois existe, conforme o Papel escolhido.
	public TrainingBattle? Math { get; }

	public NameBattle? Name { get; }

	public Mission Mission => Math?.Mission ?? Name!.Mission;

	public int MistakeStreak { get; private set; }

	public bool HintOffered => MistakeStreak >= rules.MistakesForHint;

	public bool IsOver => Energy.IsEmpty;

	public static Result<TrainingSession> Start(CombatRules rules, int maxEnergy, int unlockedLevel, TrainingSetup setup, IEnumerable<Mission> available, Pcg32 rng)
	{
		if (setup.Year < 1 || setup.Year > unlockedLevel)
			return Result<TrainingSession>.Fail("Esse Nível ainda não está liberado para treinar.");

		var pool = available
			.Where(m => m.Discipline == setup.Discipline && m.Year == setup.Year && m.Difficulty == setup.Difficulty)
			.ToList();
		if (pool.Count == 0)
			return Result<TrainingSession>.Fail("Ainda não há Desafios para essa dificuldade. Escolha outra.");

		if (setup.Discipline == TrainingSetup.Portuguese)
		{
			var name = NameBattle.Start(pool, rng);
			return name.IsOk
				? Result<TrainingSession>.Ok(new TrainingSession(rules, maxEnergy, setup, null, name.Value))
				: Result<TrainingSession>.Fail(name.Error!);
		}

		var math = TrainingBattle.Start(rules, pool, rng);
		return math.IsOk
			? Result<TrainingSession>.Ok(new TrainingSession(rules, maxEnergy, setup, math.Value, null))
			: Result<TrainingSession>.Fail(math.Error!);
	}

	// Acerto recupera Energia; golpe perfeito recupera mais.
	public void Hit(bool perfect)
	{
		Energy.Recover(perfect ? rules.Energy.Perfect : rules.Energy.Hit);
		MistakeStreak = 0;
	}

	// Erro tira Energia, o Desafio recomeça com outros números ou outro nome e o Boneco faz uma travessura.
	// Dois erros seguidos oferecem a dica.
	public MistakeOutcome Mistake()
	{
		Energy.Lose(rules.Energy.Mistake);
		MistakeStreak++;
		if (Energy.IsEmpty) return new MistakeOutcome(true, false, null);

		Math?.Next();
		Name?.Next();
		hintIndex = 0;
		var prank = Math?.Bag.PlayPrank() ?? Name!.PlayPrank();
		return new MistakeOutcome(false, HintOffered, prank);
	}

	public bool BubbleHit()
	{
		Energy.Lose(rules.Energy.Bubble);
		return Energy.IsEmpty;
	}

	// Pedir dica nunca custa Energia, nem antes dos erros. Cada pedido mostra a próxima dica da Missão.
	public string Hint()
	{
		var hints = Mission.Hints;
		var hint = hints[System.Math.Min(hintIndex, hints.Count - 1)];
		hintIndex++;
		return hint;
	}

	// A peça escondida volta sozinha.
	public void RevealHidden()
	{
		Math?.Bag.Reveal();
		Name?.Reveal();
	}

	// Depois de dissolver o escudo ou libertar a criatura, vem o próximo Desafio.
	public void NextChallenge()
	{
		Math?.Next();
		Name?.Next();
		hintIndex = 0;
	}
}
