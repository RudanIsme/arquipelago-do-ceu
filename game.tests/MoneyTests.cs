using System.Text.Json;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class MoneyTests
{
	static string Data(string name) => File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", name));

	static readonly MoneyCatalog Money = MoneyCatalog.Parse(Data("dinheiro.json")).Value!;

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	static Mission MoneyMission(string id, string task, int price, int? given = null, int year = 1, int[]? pieces = null)
	{
		var extra = (given is null ? "" : $", \"given\": {given}") + (pieces is null ? "" : $", \"pieces\": [{string.Join(",", pieces)}]");
		var json = $$"""
			{ "id": "{{id}}", "version": 1, "discipline": "matematica", "concept": "sistema-monetario", "bncc": ["EF01MA19"], "year": {{year}},
			  "difficulty": 5, "mechanic": "pagar_troco", "params": { "task": "{{task}}", "price": {{price}}{{extra}} },
			  "prompt": "Pague.", "hints": ["h1", "h2"], "explanation": "e", "accessibility": { "timerAllowed": false }, "review": { "status": "pending" } }
			""";
		return JsonSerializer.Deserialize<Mission>(json, Options)!;
	}

	static ChangeBattle Start(params Mission[] missions) => ChangeBattle.Start(missions, Money, new Pcg32(1)).Value!;

	[Fact]
	public void O_dinheiro_segue_o_Real_e_vem_dos_dados()
	{
		Assert.Equal([5, 10, 25, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000], Money.Denominations.Select(d => d.Value));
		Assert.All(Money.Denominations.Where(d => d.Value <= 100), d => Assert.Equal("moeda", d.Kind));
		Assert.All(Money.Denominations.Where(d => d.Value >= 200), d => Assert.Equal("nota", d.Kind));
	}

	[Theory]
	[InlineData(5, "R$ 0,05")]
	[InlineData(350, "R$ 3,50")]
	[InlineData(1000, "R$ 10,00")]
	[InlineData(12345, "R$ 123,45")]
	public void O_valor_escrito_segue_o_formato_do_Real(int cents, string expected)
	{
		Assert.Equal(expected, MoneyCatalog.Format(cents));
	}

	[Theory]
	[InlineData(100, "1 real")]
	[InlineData(250, "2 reais e 50 centavos")]
	[InlineData(5, "5 centavos")]
	[InlineData(0, "zero")]
	public void O_valor_falado_diz_reais_e_centavos(int cents, string expected)
	{
		Assert.Equal(expected, MoneyCatalog.Spoken(cents));
	}

	[Fact]
	public void O_catalogo_de_dinheiro_recusa_dados_quebrados()
	{
		Assert.False(MoneyCatalog.Parse("{").IsOk);
		Assert.False(MoneyCatalog.Parse("""{"denominations":[]}""").IsOk);
		Assert.False(MoneyCatalog.Parse("""{"denominations":[{"value":0,"kind":"moeda","name":"x"}]}""").IsOk);
		Assert.False(MoneyCatalog.Parse("""{"denominations":[{"value":5,"kind":"ficha","name":"x"}]}""").IsOk);
		Assert.False(MoneyCatalog.Parse("""{"denominations":[{"value":5,"kind":"moeda","name":"a"},{"value":5,"kind":"moeda","name":"b"}]}""").IsOk);
	}

	[Fact]
	public void Pagar_pede_montar_o_valor_exato_e_o_jogo_confere()
	{
		var battle = Start(MoneyMission("p", "pagar", 350));
		Assert.Equal(350, battle.Target);

		battle.Add(PieceOf(battle, 100));
		battle.Add(PieceOf(battle, 100));
		battle.Add(PieceOf(battle, 100));
		battle.Add(PieceOf(battle, 50));

		Assert.True(battle.Confirm().Correct);
		Assert.Equal(0, battle.Remaining);
	}

	[Fact]
	public void Dar_troco_pede_o_que_sobra_da_nota_entregue_e_quem_calcula_e_o_jogo()
	{
		var battle = Start(MoneyMission("t", "troco", 350, given: 500));

		Assert.Equal(150, battle.Target);
		battle.Add(PieceOf(battle, 100));
		battle.Add(PieceOf(battle, 50));
		Assert.True(battle.Confirm().Correct);
	}

	[Fact]
	public void Valor_a_menos_ou_a_mais_nao_vale_e_diz_o_quanto_passou_ou_faltou()
	{
		var battle = Start(MoneyMission("p", "pagar", 350));
		battle.Add(PieceOf(battle, 100));
		var short_ = battle.Confirm();
		battle.Clear();
		battle.Add(PieceOf(battle, 500));
		var over = battle.Confirm();

		Assert.False(short_.Correct);
		Assert.Equal(100, short_.Total);
		Assert.True(short_.Total < short_.Target);
		Assert.False(over.Correct);
		Assert.True(over.Total > over.Target);
		Assert.NotEqual(0, battle.Remaining);
	}

	[Fact]
	public void Desfazer_tira_a_ultima_peca_e_limpar_tira_todas()
	{
		var battle = Start(MoneyMission("p", "pagar", 350));
		battle.Add(PieceOf(battle, 100));
		battle.Add(PieceOf(battle, 50));

		Assert.True(battle.Undo());
		Assert.Single(battle.Table);
		battle.Clear();
		Assert.Empty(battle.Table);
		Assert.False(battle.Undo());
	}

	[Theory]
	[InlineData(350)]
	[InlineData(775)]
	[InlineData(5)]
	[InlineData(9995)]
	public void A_dica_nunca_trava_a_crianca_seguir_as_sugestoes_sempre_chega_ao_valor(int price)
	{
		var battle = Start(MoneyMission("p", "pagar", price));

		for (var step = 0; step < 40 && !battle.Confirm().Correct; step++)
		{
			var (piece, undo) = battle.Suggest();
			if (undo) battle.Undo();
			else battle.Add(piece!.Value);
		}

		Assert.Equal(0, battle.Remaining);
	}

	[Fact]
	public void A_sugestao_depois_de_passar_do_valor_manda_desfazer()
	{
		var battle = Start(MoneyMission("p", "pagar", 100));
		battle.Add(PieceOf(battle, 200));

		var (_, undo) = battle.Suggest();

		Assert.True(undo);
	}

	[Fact]
	public void Missao_mal_escrita_vira_falha_e_nunca_excecao()
	{
		Assert.False(ChangeBattle.Start([MoneyMission("a", "pagar", 7)], Money, new Pcg32(1)).IsOk);
		Assert.False(ChangeBattle.Start([MoneyMission("a", "troco", 350)], Money, new Pcg32(1)).IsOk);
		Assert.False(ChangeBattle.Start([MoneyMission("a", "troco", 350, given: 300)], Money, new Pcg32(1)).IsOk);
		Assert.False(ChangeBattle.Start([MoneyMission("a", "dobrar", 350)], Money, new Pcg32(1)).IsOk);
		Assert.False(ChangeBattle.Start([MoneyMission("a", "pagar", 350, pieces: [3])], Money, new Pcg32(1)).IsOk);
		Assert.False(ChangeBattle.Start([], Money, new Pcg32(1)).IsOk);
	}

	[Fact]
	public void A_Missao_pode_limitar_as_pecas_de_dinheiro_que_aparecem()
	{
		var battle = Start(MoneyMission("p", "pagar", 150, pieces: [50, 100]));

		Assert.Equal([50, 100], battle.Pieces.Select(p => p.Value));
	}

	[Fact]
	public void A_luta_de_dinheiro_entra_pelo_registro_de_Mecanicas()
	{
		var mechanics = BattleMechanics.Default.With(new ChangeMechanic(Money));
		var missions = new[] { MoneyMission("p", "pagar", 150) };

		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, new TrainingSetup("matematica", 1, 5), missions, new Pcg32(1), mechanics: mechanics).Value!;

		Assert.NotNull(fight.Change);
		Assert.Equal("p", fight.Mission.Id);
		Assert.Equal(Prank.Shuffled, fight.Mistake().Prank);
	}

	[Fact]
	public void Uma_fila_de_Missoes_de_Matematica_e_de_dinheiro_continua_com_a_conta()
	{
		var mixed = new[] { TrainingFixtures.MathMission("m", 9, difficulty: 5), MoneyMission("p", "pagar", 150) };
		var mechanics = BattleMechanics.Default.With(new ChangeMechanic(Money));

		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, new TrainingSetup("matematica", 1, 5), mixed, new Pcg32(1), mechanics: mechanics).Value!;

		Assert.NotNull(fight.Math);
		Assert.Null(fight.Change);
	}

	[Fact]
	public void O_desafio_de_compra_do_Mercador_tem_o_troco_da_menor_nota_que_cobre_o_preco()
	{
		var withChange = ChangeChallenge.ForPurchase(650, Money);
		var exact = ChangeChallenge.ForPurchase(1000, Money);

		Assert.Equal(MoneyTask.Change, withChange.Task);
		Assert.Equal(1000, withChange.Given);
		Assert.Equal(350, withChange.Target);
		Assert.Equal(MoneyTask.Pay, exact.Task);
		Assert.Equal(1000, exact.Target);
	}

	[Fact]
	public void O_desafio_confere_pela_soma_das_pecas()
	{
		var challenge = ChangeChallenge.ForPurchase(650, Money);

		Assert.True(challenge.Check([200, 100, 50]).Correct);
		Assert.False(challenge.Check([200, 100]).Correct);
		Assert.False(challenge.Check([]).Correct);
	}

	static int PieceOf(ChangeBattle battle, int value) => battle.Pieces.ToList().FindIndex(p => p.Value == value);
}
