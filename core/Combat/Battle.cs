using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// O que uma Mecânica de luta sabe fazer dentro de uma sessão: dizer a Missão em jogo, trocar de Desafio e responder às travessuras.
public interface IBattle
{
	Mission Mission { get; }

	void Next();

	void Reveal();

	Prank PlayPrank();
}

// Como uma batalha terminou: o Conceito, quantos acertos e erros teve e se a criatura foi acalmada.
// O Treino também emite o resultado, mas nada o conta; quem conta (Maestria, moedas, repetição) é a Dungeon.
public sealed record BattleResult(string Concept, int Hits, int Mistakes, bool Calmed);

// Uma Mecânica de luta registrada por id (ADR 0006): sabe se reconhece os Desafios de uma dificuldade e como começar a luta.
// Uma Mecânica nova é uma classe aqui e uma linha em BattleMechanics; a sessão de combate não muda.
public interface IBattleMechanic
{
	string Id { get; }

	bool Handles(IReadOnlyList<Mission> pool);

	Result<IBattle> Start(CombatRules rules, IReadOnlyList<Mission> pool, Pcg32 rng, MissionHistory? history);
}

// As Mecânicas que o jogo sabe lutar, na ordem em que reconhecem os Desafios: a primeira que os reconhece vence.
// Escrever a palavra só serve se não houver Missão de nome, sílabas ou frase.
public sealed class BattleMechanics
{
	readonly IReadOnlyList<IBattleMechanic> all;

	BattleMechanics(IReadOnlyList<IBattleMechanic> all) => this.all = all;

	public static BattleMechanics Default { get; } = new([new NameMechanic(), new WordMechanic(), new MathMechanic()]);

	public IEnumerable<string> Ids => all.Select(m => m.Id);

	public IBattleMechanic? For(IReadOnlyList<Mission> pool) => all.FirstOrDefault(m => m.Handles(pool));

	public BattleMechanics With(IBattleMechanic mechanic) => new([mechanic, .. all.Where(m => m.Id != mechanic.Id)]);
}

sealed class NameMechanic : IBattleMechanic
{
	public string Id => "nome";

	public bool Handles(IReadOnlyList<Mission> pool) => pool.Any(m => NameBattle.Mechanics.Contains(m.Mechanic));

	public Result<IBattle> Start(CombatRules rules, IReadOnlyList<Mission> pool, Pcg32 rng, MissionHistory? history) =>
		BattleStart.Wrap(NameBattle.Start(pool, rng, history));
}

sealed class WordMechanic : IBattleMechanic
{
	public string Id => "palavra";

	public bool Handles(IReadOnlyList<Mission> pool) => pool.Any(m => m.Mechanic == "escrever_palavra");

	public Result<IBattle> Start(CombatRules rules, IReadOnlyList<Mission> pool, Pcg32 rng, MissionHistory? history) =>
		BattleStart.Wrap(WordBattle.Start(pool, rng, history));
}

sealed class MathMechanic : IBattleMechanic
{
	public string Id => "conta";

	public bool Handles(IReadOnlyList<Mission> pool) => pool.Any(m => m.Mechanic == "compor_numero");

	public Result<IBattle> Start(CombatRules rules, IReadOnlyList<Mission> pool, Pcg32 rng, MissionHistory? history) =>
		BattleStart.Wrap(TrainingBattle.Start(rules, pool, rng, history));
}

static class BattleStart
{
	public static Result<IBattle> Wrap<T>(Result<T> started) where T : IBattle =>
		started.IsOk ? Result<IBattle>.Ok(started.Value!) : Result<IBattle>.Fail(started.Error!);
}
