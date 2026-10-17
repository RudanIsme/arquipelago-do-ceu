using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class BattleExtensionTests
{
	static readonly TrainingSetup Math1 = new("matematica", 1, 1);

	static readonly IReadOnlyList<Mission> Missions = [TrainingFixtures.MathMission("a", 9), TrainingFixtures.MathMission("b", 8)];

	// Uma Mecânica de teste: entra só com a classe e o registro, sem tocar na sessão de combate.
	sealed class EchoBattle(Mission mission) : IBattle
	{
		public Mission Mission { get; } = mission;

		public int Nexts { get; private set; }

		public void Next() => Nexts++;

		public void Reveal()
		{
		}

		public Prank PlayPrank() => Prank.Shuffled;
	}

	sealed class EchoMechanic : IBattleMechanic
	{
		public string Id => "eco";

		public bool Handles(IReadOnlyList<Mission> pool) => pool.Any(m => m.Mechanic == "eco");

		public Result<IBattle> Start(CombatRules rules, IReadOnlyList<Mission> pool, Pcg32 rng, MissionHistory? history) =>
			Result<IBattle>.Ok(new EchoBattle(pool.First(m => m.Mechanic == "eco")));
	}

	[Fact]
	public void A_Energia_maxima_e_um_parametro_e_o_padrao_vem_dos_dados()
	{
		var withBonus = CombatSession.Start(TrainingFixtures.Rules, 7, 1, Math1, Missions, new Pcg32(1)).Value!;
		var byDefault = CombatSession.Start(TrainingFixtures.Rules, 1, Math1, Missions, new Pcg32(1)).Value!;

		Assert.Equal(7, withBonus.Energy.Max);
		Assert.Equal(TrainingFixtures.Game.MaxEnergy, byDefault.Energy.Max);
		Assert.Equal(TrainingFixtures.Game.MaxEnergy, TrainingFixtures.Rules.DefaultMaxEnergy);
	}

	[Fact]
	public void O_fim_da_batalha_diz_Conceito_acertos_erros_e_se_a_criatura_foi_acalmada()
	{
		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, Math1, Missions, new Pcg32(1)).Value!;
		fight.Hit(perfect: false);
		fight.Mistake();
		fight.Hit(perfect: true);

		var result = fight.Conclude(calmed: true);

		Assert.Equal(fight.Mission.Concept, result.Concept);
		Assert.Equal(2, result.Hits);
		Assert.Equal(1, result.Mistakes);
		Assert.True(result.Calmed);
	}

	[Fact]
	public void Cada_Desafio_comeca_o_resultado_do_zero()
	{
		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, Math1, Missions, new Pcg32(1)).Value!;
		fight.Hit(perfect: false);
		fight.Conclude(calmed: true);
		fight.NextChallenge();

		var second = fight.Conclude(calmed: false);

		Assert.Equal(0, second.Hits);
		Assert.Equal(0, second.Mistakes);
		Assert.False(second.Calmed);
	}

	[Fact]
	public void A_bolha_nao_conta_como_erro_da_resposta()
	{
		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, Math1, Missions, new Pcg32(1)).Value!;

		fight.BubbleHit();

		Assert.Equal(0, fight.Conclude(calmed: false).Mistakes);
	}

	[Fact]
	public void Uma_Mecanica_nova_entra_so_com_a_classe_e_o_registro()
	{
		var mechanics = BattleMechanics.Default.With(new EchoMechanic());
		var echo = TrainingFixtures.MathMission("eco-1", 3) with { Mechanic = "eco" };

		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, Math1, [echo], new Pcg32(1), mechanics: mechanics).Value!;
		fight.NextChallenge();

		Assert.Equal("eco-1", fight.Mission.Id);
		Assert.Null(fight.Math);
		Assert.Equal(1, ((EchoBattle)fight.Battle).Nexts);
		Assert.Equal(Prank.Shuffled, fight.Mistake().Prank);
	}

	[Fact]
	public void Sem_nenhuma_Mecanica_que_conheca_os_Desafios_a_luta_nao_comeca()
	{
		var other = TrainingFixtures.MathMission("x", 3) with { Mechanic = "desconhecida" };

		var started = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, Math1, [other], new Pcg32(1));

		Assert.False(started.IsOk);
	}

	[Fact]
	public void As_Mecanicas_do_jogo_seguem_a_ordem_nome_palavra_e_conta()
	{
		Assert.Equal(["nome", "palavra", "conta"], BattleMechanics.Default.Ids);
	}

	[Fact]
	public void A_conta_de_ataque_e_numero_sinal_e_numero_e_o_terceiro_traco_a_completa()
	{
		var rules = TrainingFixtures.Rules;
		var math = CombatSession.Start(rules, new Energy(5), 1, Math1, Missions, new Pcg32(1)).Value!.Math!;

		Assert.False(math.Staff.IsComplete);
		math.Staff.Place(0);
		math.Staff.PlaceOperator("+");
		Assert.False(math.Staff.IsComplete);
		math.Staff.Place(1);

		Assert.True(math.Staff.IsComplete);
		Assert.Equal(Staff.AttackPieces, math.Staff.Tokens.Count);
	}

	[Fact]
	public void O_nome_e_a_palavra_se_completam_na_ultima_peca()
	{
		var name = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, new TrainingSetup("portugues", 1, 1), [TrainingFixtures.NameMission("n", ["ga", "to"])], new Pcg32(2)).Value!.Name!;
		Assert.False(name.IsComplete);
		name.Place(0);
		Assert.False(name.IsComplete);
		name.Place(1);
		Assert.True(name.IsComplete);
	}
}
