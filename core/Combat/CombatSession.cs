using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// O que a criança escolhe antes de treinar: o Papel (pela disciplina das Missões), o Nível (ano) e a dificuldade.
public sealed record TrainingSetup(string Discipline, int Year, int Difficulty)
{
	public const int LastDifficulty = 5;

	// Só os Níveis a que o Perfil já tem acesso, até o último do jogo: o treino nunca libera um Nível.
	public static IReadOnlyList<int> Levels(int unlockedLevel, int maxLevel) => Enumerable.Range(1, Math.Clamp(unlockedLevel, 1, Math.Max(1, maxLevel))).ToList();
}

// O que um erro causou. `Empty` é Energia zerada: a criança sai da Área de treino.
public sealed record MistakeOutcome(bool Empty, bool HintOffered, Prank? Prank);

// Uma sessão de treino contra o Boneco: Energia própria, erros seguidos e dicas. Nada aqui toca em save,
// porque o treino não conta para o progresso (e `ArquiteturaTests` garante que o combate não conhece o save).
public sealed class CombatSession
{
	readonly CombatRules rules;

	// As regras do ano escolhido: a bolsa e os sinais mudam com o Nível, o resto (tempo, Energia, bolha) é igual.
	public CombatRules Rules => rules;
	int hintIndex;

	CombatSession(CombatRules rules, Energy energy, TrainingSetup setup, IBattle battle)
	{
		this.rules = rules;
		Energy = energy;
		Setup = setup;
		Battle = battle;
	}

	public Energy Energy { get; }

	public TrainingSetup Setup { get; }

	// A luta da Mecânica que reconheceu os Desafios; os atalhos abaixo servem a quem desenha cada Mecânica.
	public IBattle Battle { get; }

	public TrainingBattle? Math => Battle as TrainingBattle;

	public NameBattle? Name => Battle as NameBattle;

	public WordBattle? Word => Battle as WordBattle;

	public ChangeBattle? Change => Battle as ChangeBattle;

	public Mission Mission => Battle.Mission;

	// Os acertos e erros do Desafio de agora, para o resultado da batalha.
	int hits;
	int mistakes;

	public int MistakeStreak { get; private set; }

	public bool HintOffered => MistakeStreak >= rules.MistakesForHint;

	public bool IsOver => Energy.IsEmpty;

	// O treino começa cheio; a Expedição passa a mesma Energia de uma luta para a outra.
	public static Result<CombatSession> Start(CombatRules baseRules, int maxEnergy, int unlockedLevel, TrainingSetup setup, IEnumerable<Mission> available, Pcg32 rng) =>
		Start(baseRules, new Energy(maxEnergy), unlockedLevel, setup, available, rng);

	// Sem Energia de fora, a máxima é a dos dados.
	public static Result<CombatSession> Start(CombatRules baseRules, int unlockedLevel, TrainingSetup setup, IEnumerable<Mission> available, Pcg32 rng) =>
		Start(baseRules, new Energy(baseRules.DefaultMaxEnergy), unlockedLevel, setup, available, rng);

	// Com `history` (a Expedição livre) a luta evita os problemas que já apareceram.
	public static Result<CombatSession> Start(CombatRules baseRules, Energy energy, int unlockedLevel, TrainingSetup setup, IEnumerable<Mission> available, Pcg32 rng,
		MissionHistory? history = null, BattleMechanics? mechanics = null)
	{
		var rules = baseRules.ForYear(setup.Year);
		if (setup.Year < 1 || setup.Year > unlockedLevel)
			return Result<CombatSession>.Fail("Esse Nível ainda não está liberado para treinar.");

		var pool = available
			.Where(m => m.Discipline == setup.Discipline && m.Year == setup.Year && m.Difficulty == setup.Difficulty)
			.ToList();
		if (pool.Count == 0)
			return Result<CombatSession>.Fail("Ainda não há Desafios para essa dificuldade. Escolha outra.");

		var mechanic = (mechanics ?? BattleMechanics.Default).For(pool);
		if (mechanic is null) return Result<CombatSession>.Fail("Ainda não há Desafios para essa dificuldade. Escolha outra.");

		var battle = mechanic.Start(rules, pool, rng, history);
		return battle.IsOk
			? Result<CombatSession>.Ok(new CombatSession(rules, energy, setup, battle.Value!))
			: Result<CombatSession>.Fail(battle.Error!);
	}

	// Acerto recupera Energia; golpe perfeito recupera mais.
	public void Hit(bool perfect)
	{
		Energy.Recover(perfect ? rules.Energy.Perfect : rules.Energy.Hit);
		MistakeStreak = 0;
		hits++;
	}

	// Erro tira Energia, o Desafio recomeça com outros números ou outro nome e o Boneco faz uma travessura.
	// Dois erros seguidos oferecem a dica.
	public MistakeOutcome Mistake()
	{
		Energy.Lose(rules.Energy.Mistake);
		MistakeStreak++;
		mistakes++;
		if (Energy.IsEmpty) return new MistakeOutcome(true, false, null);

		Battle.Next();
		hintIndex = 0;
		var prank = Battle.PlayPrank();
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
		Battle.Reveal();
	}

	// Depois de dissolver o escudo ou libertar a criatura, vem o próximo Desafio.
	public void NextChallenge()
	{
		Battle.Next();
		hintIndex = 0;
		hits = 0;
		mistakes = 0;
	}

	// Fecha a batalha do Desafio de agora e diz como foi; o próximo Desafio começa a contar do zero.
	public BattleResult Conclude(bool calmed)
	{
		var result = new BattleResult(Mission.Concept, hits, mistakes, calmed);
		hits = 0;
		mistakes = 0;
		return result;
	}
}
