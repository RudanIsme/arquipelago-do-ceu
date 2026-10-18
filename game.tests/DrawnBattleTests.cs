using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class DrawnMathBattleTests
{
	static readonly MathLevel Level2 = new(["+", "-"], 2);

	static TrainingBattle Result(int target = 8) => TrainingBattle.Start(TrainingFixtures.Rules, [TrainingFixtures.MathMission("a", target)], new Pcg32(1)).Value!;

	static TrainingBattle Answer() => TrainingBattle.Start(TrainingFixtures.Rules, [TrainingFixtures.MathMission("q", 8, question: "3 + 5")], new Pcg32(1)).Value!;

	[Fact]
	public void Conta_certa_dissolve_o_escudo()
	{
		var battle = Result();

		var attack = battle.Draw("3+5", Level2);

		Assert.True(attack.IsOk);
		Assert.Equal(HitOutcome.Dissolved, attack.Value!.Outcome);
		Assert.True(battle.Shield.IsDissolved);
		Assert.Equal("3 + 5 = 8", attack.Value.Equation);
	}

	[Fact]
	public void Conta_valida_com_resultado_errado_e_resposta_errada_e_o_escudo_fica()
	{
		var battle = Result();

		var attack = battle.Draw("3+4", Level2);

		Assert.Equal(HitOutcome.Wrong, attack.Value!.Outcome);
		Assert.Equal(8, battle.Shield.Remaining);
	}

	[Fact]
	public void Numero_sozinho_e_recusado_e_nada_muda()
	{
		var battle = Result();

		var attack = battle.Draw("8", Level2);

		Assert.False(attack.IsOk);
		Assert.Contains("conta", attack.Error);
		Assert.Equal(8, battle.Shield.Remaining);
	}

	[Fact]
	public void Missao_com_conta_pronta_pede_so_o_numero_e_mostra_a_conta()
	{
		var battle = Answer();

		Assert.Equal(MathAsk.Answer, battle.Ask);
		Assert.Equal("3 + 5", battle.Question);
		Assert.Equal(HitOutcome.Dissolved, battle.Draw("8", Level2).Value!.Outcome);
	}

	[Fact]
	public void Com_conta_pronta_sinal_e_recusado_sem_custo_e_o_numero_errado_custa()
	{
		var battle = Answer();

		Assert.False(battle.Draw("3+5", Level2).IsOk);
		Assert.Equal(HitOutcome.Wrong, battle.Draw("7", Level2).Value!.Outcome);
	}

	[Fact]
	public void Missao_de_resultado_pede_conta_e_a_de_conta_pronta_nao()
	{
		Assert.Equal(MathAsk.Result, Result().Ask);
		Assert.Equal("", Result().Question);
	}

	[Fact]
	public void A_conta_de_exemplo_e_sempre_a_mesma_e_nao_mexe_no_sorteio_da_luta()
	{
		var one = Result();
		var other = Result();

		Assert.Equal(one.Example(Level2), one.Example(Level2));
		one.Example(Level2);
		one.Next();
		other.Next();
		Assert.Equal(other.Mission.Id, one.Mission.Id);
		Assert.Equal(other.StartingShield, one.StartingShield);
	}

	[Fact]
	public void A_Dica_de_exemplo_so_existe_quando_se_pede_um_resultado()
	{
		var example = Result().Example(Level2);

		Assert.Equal(DrawnKind.Right, DrawnMath.Judge(example!, 8, MathAsk.Result, Level2).Kind);
		Assert.Null(Answer().Example(Level2));
	}
}

public class DrawnNameBattleTests
{
	static NameBattle Bola() => NameBattle.Start([TrainingFixtures.NameMission("n", ["bo", "la"])], new Pcg32(1)).Value!;

	[Fact]
	public void Cada_silaba_certa_e_um_golpe_e_o_nome_completo_liberta()
	{
		var battle = Bola();

		var first = battle.Write("BO");
		Assert.Equal(1, first.NewStrikes);
		Assert.False(first.Freed);
		Assert.Equal(1, battle.Remaining);
		Assert.Equal("BO", battle.Written);

		var second = battle.Write("LA");
		Assert.True(second.Freed);
		Assert.Equal(0, battle.Remaining);
	}

	[Fact]
	public void A_palavra_inteira_de_uma_vez_da_dois_golpes_e_liberta()
	{
		var verdict = Bola().Write("BOLA");

		Assert.Equal(2, verdict.NewStrikes);
		Assert.True(verdict.Freed);
	}

	[Fact]
	public void Silaba_errada_e_erro_e_silaba_pela_metade_nao_e()
	{
		var battle = Bola();

		Assert.False(battle.Write("B").Mistaken);
		Assert.Equal(0, battle.Write("B").NewStrikes);
		Assert.True(battle.Write("LA").Mistaken);
	}

	[Fact]
	public void Escrever_depois_do_nome_pronto_nao_e_erro_e_nao_custa()
	{
		var battle = Bola();
		battle.Write("BOLA");

		var again = battle.Write("BO");

		Assert.False(again.Mistaken);
		Assert.Equal(0, again.NewStrikes);
	}

	[Fact]
	public void A_proxima_silaba_e_a_do_pontilhado()
	{
		var battle = Bola();
		Assert.Equal("bo", battle.NextSyllable);

		battle.Write("BO");
		Assert.Equal("la", battle.NextSyllable);
	}

	[Fact]
	public void Outro_nome_comeca_do_zero()
	{
		var battle = NameBattle.Start([TrainingFixtures.NameMission("n", ["bo", "la"]), TrainingFixtures.NameMission("m", ["pa", "to"])], new Pcg32(3)).Value!;
		battle.Write(string.Concat(battle.Name).ToUpperInvariant());

		battle.Next();

		Assert.Equal("", battle.Written);
		Assert.Equal(battle.Name.Count, battle.Remaining);
	}
}

public class DrawnWordBattleTests
{
	static WordBattle Casa() => WordBattle.Start([WordFixtures.Word("a", "casa")], new Pcg32(1)).Value!;

	[Fact]
	public void Pedacos_da_palavra_se_somam_ate_libertar()
	{
		var battle = Casa();

		Assert.Equal(WordResult.Incomplete, battle.Write("CA"));
		Assert.Equal("ca", battle.Typed);
		Assert.Equal(WordResult.Freed, battle.Write("SA"));
		Assert.Equal(0, battle.Remaining);
	}

	[Fact]
	public void A_palavra_inteira_de_uma_vez_liberta()
	{
		Assert.Equal(WordResult.Freed, Casa().Write("CASA"));
	}

	[Fact]
	public void Escrever_depois_da_palavra_pronta_nao_e_erro()
	{
		var battle = Casa();
		battle.Write("CASA");

		Assert.NotEqual(WordResult.Mistaken, battle.Write("CA"));
	}

	[Fact]
	public void Letra_fora_do_lugar_e_erro()
	{
		var battle = Casa();
		battle.Write("C");

		Assert.Equal(WordResult.Mistaken, battle.Write("S"));
	}
}
