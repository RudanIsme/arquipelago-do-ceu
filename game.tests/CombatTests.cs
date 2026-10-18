using System.Numerics;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class CombatTests
{
	static readonly CombatRules Rules = GameRules.Parse(TestJson.Rules).Value!.Combat;

	static MathBag Bag(ulong seed = 1) => new(Rules, new Pcg32(seed));

	static Token N(int n) => Token.Number(n);

	// Número, sinal, número... como a criança monta na calculadora.
	static Token[] Sum(params int[] numbers) =>
		numbers.SelectMany((n, i) => i == 0 ? new[] { N(n) } : new[] { Token.Plus, N(n) }).ToArray();

	static int[] HandOf(MathBag bag) => bag.Hand.Select(p => p ?? 0).ToArray();

	// Monta "a + b" com duas peças da mão.
	static void PlaceTwo(Staff staff, int first = 0, int second = 1)
	{
		Assert.True(staff.Place(first));
		Assert.True(staff.PlaceOperator("+"));
		Assert.True(staff.Place(second));
	}

	// --- #44 Avaliador de expressões

	[Fact]
	public void Soma_varias_pecas()
	{
		var result = ExpressionEvaluator.Evaluate(Sum(5, 4, 3), shield: 12);

		Assert.True(result.IsOk);
		Assert.Equal(12, result.Value!.Total);
	}

	[Theory]
	[InlineData(13, Comparison.Below)]
	[InlineData(12, Comparison.Equal)]
	[InlineData(11, Comparison.Above)]
	public void Distingue_abaixo_igual_e_acima_do_escudo(int shield, Comparison expected)
	{
		var result = ExpressionEvaluator.Evaluate(Sum(5, 4, 3), shield);

		Assert.Equal(expected, result.Value!.Comparison);
	}

	[Fact]
	public void Um_numero_sozinho_nao_e_conta()
	{
		// 5 = 5 não faz sentido como resposta: a conta precisa de um sinal e de outro número.
		Assert.False(ExpressionEvaluator.Evaluate([N(5)], 5).IsOk);
		Assert.False(ExpressionEvaluator.Evaluate([N(5), Token.Plus], 5).IsOk);
	}

	[Fact]
	public void Conta_fora_da_ordem_numero_sinal_numero_e_recusada()
	{
		Assert.False(ExpressionEvaluator.Evaluate([Token.Plus, N(1), N(2)], 3).IsOk);
		Assert.False(ExpressionEvaluator.Evaluate([N(1), N(2), N(3)], 6).IsOk);
		Assert.False(ExpressionEvaluator.Evaluate([N(1), Token.Plus, Token.Plus, N(2)], 3).IsOk);
		Assert.False(ExpressionEvaluator.Evaluate([N(1), Token.Plus, N(2), Token.Plus], 3).IsOk);
	}

	[Fact]
	public void Conta_vazia_escudo_invalido_ou_grande_demais_e_erro_sem_excecao()
	{
		Assert.False(ExpressionEvaluator.Evaluate([], 5).IsOk);
		Assert.False(ExpressionEvaluator.Evaluate(Sum(1, 1), 0).IsOk);
		Assert.False(ExpressionEvaluator.Evaluate(Sum(int.MaxValue, int.MaxValue), 5).IsOk);
	}

	// --- #54 Escudo

	[Fact]
	public void Resultado_abaixo_tira_do_escudo_e_igual_dissolve()
	{
		var shield = new Shield(12);

		Assert.Equal(HitOutcome.Reduced, shield.Hit(ExpressionEvaluator.Evaluate(Sum(5, 4), shield.Remaining).Value!));
		Assert.Equal(3, shield.Remaining);
		Assert.Equal(HitOutcome.Dissolved, shield.Hit(ExpressionEvaluator.Evaluate(Sum(1, 2), shield.Remaining).Value!));
		Assert.Equal(0, shield.Remaining);
		Assert.True(shield.IsDissolved);
	}

	[Fact]
	public void Passar_do_valor_nao_mexe_no_escudo()
	{
		var shield = new Shield(5);

		Assert.Equal(HitOutcome.Overshoot, shield.Hit(ExpressionEvaluator.Evaluate(Sum(4, 3), shield.Remaining).Value!));
		Assert.Equal(5, shield.Remaining);
	}

	// --- #43 Bolsa do Matemático

	[Fact]
	public void A_mao_comeca_com_5_pecas_da_bolsa()
	{
		var bag = Bag();

		Assert.Equal(5, bag.Hand.Count);
		Assert.All(bag.Hand, p => Assert.InRange(p!.Value, 1, 5));
	}

	[Fact]
	public void Pecas_usadas_sao_repostas_pela_bolsa()
	{
		var bag = Bag();
		var staff = new Staff(bag, Rules);
		PlaceTwo(staff);

		Assert.True(staff.Confirm(shield: 20).IsOk);

		Assert.Equal(5, bag.Hand.Count(p => p is not null));
	}

	[Fact]
	public void A_bolsa_nunca_acaba_porque_o_descarte_volta_embaralhado()
	{
		var bag = Bag();
		var staff = new Staff(bag, Rules);

		for (var round = 0; round < 40; round++)
		{
			PlaceTwo(staff);
			Assert.True(staff.Confirm(shield: 20).IsOk);
			Assert.Equal(5, bag.Hand.Count(p => p is not null));
		}
	}

	[Fact]
	public void A_mesma_seed_da_a_mesma_ordem_e_outra_seed_outra()
	{
		Assert.Equal(Sequence(Bag(7)), Sequence(Bag(7)));
		Assert.NotEqual(Sequence(Bag(7)), Sequence(Bag(8)));
	}

	static List<int> Sequence(MathBag bag)
	{
		var seen = new List<int>(HandOf(bag));
		var staff = new Staff(bag, Rules);
		for (var i = 0; i < 12; i++)
		{
			PlaceTwo(staff, i % 5, (i + 1) % 5);
			staff.Confirm(shield: 20);
			seen.AddRange(HandOf(bag));
		}
		return seen;
	}

	[Fact]
	public void A_bolsa_tem_so_as_pecas_dos_dados()
	{
		var bag = Bag();
		var staff = new Staff(bag, Rules);
		var seen = new HashSet<int>();

		for (var i = 0; i < 60; i++)
		{
			foreach (var p in bag.Hand) seen.Add(p!.Value);
			PlaceTwo(staff);
			staff.Confirm(shield: 20);
		}

		Assert.Equal(Rules.BagNumbers.Order(), seen.Order());
	}

	// --- #45 e #46 Cajado: a calculadora, desfazer e confirmar

	[Fact]
	public void Montar_mostra_a_conta_antes_de_confirmar_sem_gastar_a_mao()
	{
		var bag = Bag();
		var staff = new Staff(bag, Rules);
		var first = bag.Hand[0]!.Value;
		var second = bag.Hand[1]!.Value;

		PlaceTwo(staff);

		Assert.Equal([N(first), Token.Plus, N(second)], staff.Tokens);
		Assert.Null(bag.Hand[0]);
		Assert.Null(bag.Hand[1]);
	}

	[Fact]
	public void A_calculadora_obriga_a_alternar_numero_e_sinal()
	{
		var staff = new Staff(Bag(), Rules);

		Assert.False(staff.PlaceOperator("+"));
		Assert.True(staff.Place(0));
		Assert.False(staff.Place(1));
		Assert.True(staff.PlaceOperator("+"));
		Assert.False(staff.PlaceOperator("+"));
		Assert.True(staff.Place(1));
		Assert.Equal(3, staff.Tokens.Count);
	}

	[Fact]
	public void So_o_sinal_que_esta_nos_dados_existe()
	{
		var staff = new Staff(Bag(), Rules);
		staff.Place(0);

		Assert.False(staff.PlaceOperator("-"));
		Assert.False(staff.PlaceOperator("*"));
	}

	[Fact]
	public void Um_numero_sozinho_nao_pode_ser_confirmado_e_nao_gasta_a_mao()
	{
		var bag = Bag();
		var staff = new Staff(bag, Rules);
		staff.Place(0);

		Assert.False(staff.Confirm(shield: 5).IsOk);

		Assert.Single(staff.Tokens);
		Assert.Null(bag.Hand[0]);
	}

	[Fact]
	public void Desfazer_devolve_a_peca_ao_mesmo_lugar_da_mao()
	{
		var bag = Bag();
		var before = bag.Hand.ToArray();
		var staff = new Staff(bag, Rules);
		staff.Place(2);
		staff.PlaceOperator("+");
		staff.Place(0);

		Assert.True(staff.Undo());
		Assert.True(staff.Undo());
		Assert.True(staff.Undo());

		Assert.Equal(before, bag.Hand);
		Assert.Empty(staff.Tokens);
	}

	[Fact]
	public void Cancelar_devolve_tudo_e_nao_conta_como_resposta()
	{
		var bag = Bag();
		var before = bag.Hand.ToArray();
		var staff = new Staff(bag, Rules);
		PlaceTwo(staff, 1, 3);

		staff.Cancel();

		Assert.Equal(before, bag.Hand);
		Assert.Empty(staff.Tokens);
	}

	[Fact]
	public void Confirmar_devolve_a_avaliacao_e_limpa_a_montagem()
	{
		var bag = Bag();
		var staff = new Staff(bag, Rules);
		var total = bag.Hand[0]!.Value + bag.Hand[1]!.Value;
		PlaceTwo(staff);

		var result = staff.Confirm(shield: total);

		Assert.Equal(Comparison.Equal, result.Value!.Comparison);
		Assert.Empty(staff.Tokens);
	}

	[Fact]
	public void Passar_do_valor_devolve_as_pecas_para_a_mao_e_nao_gasta_nada()
	{
		var bag = Bag();
		var before = bag.Hand.ToArray();
		var staff = new Staff(bag, Rules);
		PlaceTwo(staff);

		var result = staff.Confirm(shield: 1);

		Assert.Equal(Comparison.Above, result.Value!.Comparison);
		Assert.Equal(before, bag.Hand);
		Assert.Empty(staff.Tokens);
	}

	[Fact]
	public void Peca_ja_usada_ou_slot_inexistente_e_ignorado()
	{
		var staff = new Staff(Bag(), Rules);
		Assert.True(staff.Place(0));
		staff.PlaceOperator("+");

		Assert.False(staff.Place(0));
		Assert.False(staff.Place(9));
		Assert.False(staff.Place(-1));
		Assert.Equal(2, staff.Tokens.Count);
	}

	[Fact]
	public void Desfazer_sem_nada_montado_nao_faz_nada()
	{
		Assert.False(new Staff(Bag(), Rules).Undo());
	}

	// --- #52 e #53 Tempo

	[Fact]
	public void O_tempo_desacelera_na_montagem_e_volta_ao_normal_depois()
	{
		var time = new CombatTime(Rules);
		Assert.Equal(1f, time.Scale);

		time.Begin();
		Assert.Equal(Rules.SlowTimeScale, time.Scale);

		time.End();
		Assert.Equal(1f, time.Scale);
	}

	[Fact]
	public void No_modo_calmo_o_tempo_fica_lento_a_luta_toda_e_volta_ao_normal_ao_sair()
	{
		var time = new CombatTime(Rules);

		time.Calm(true);
		Assert.Equal(Rules.SlowTimeScale, time.Scale);
		time.Begin();
		time.End();
		Assert.Equal(Rules.SlowTimeScale, time.Scale);

		time.Calm(false);
		Assert.Equal(1f, time.Scale);
	}

	[Fact]
	public void A_escala_reduzida_vem_dos_dados()
	{
		var rules = GameRules.Parse(TestJson.Rules.Replace("\"slowTimeScale\": 0.25", "\"slowTimeScale\": 0.5")).Value!.Combat;
		var time = new CombatTime(rules);

		time.Begin();

		Assert.Equal(0.5f, time.Scale);
	}

	// --- Traçado: a trilha e a conferência do desenho

	static readonly TracingRules Tracer = Rules.Tracing;

	// O "L" da trilha numa área de 200 x 200: desce pela esquerda e vai para a direita pela base.
	static TraceGuide GuideOfL() =>
		TraceTrails.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "trilhas.json")), File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "trilhas.schema.json")))
			.Value!.Guide("L", new Vector2(200, 200), Tracer).Value!;

	static IReadOnlyList<IReadOnlyList<Vector2>> Strokes(params Vector2[][] strokes) => strokes;

	static Vector2[] Along(TraceGuide guide) => [.. guide.Paths![0].Points];

	[Fact]
	public void Seguir_a_trilha_passa()
	{
		var guide = GuideOfL();

		var check = Tracing.Check(guide, Strokes(Along(guide)), Tracer);

		Assert.True(check.Passed, $"cobertura {check.Coverage}, fora {check.Stray}");
	}

	[Fact]
	public void Traco_em_varios_pedacos_tambem_passa()
	{
		var guide = GuideOfL();
		var path = Along(guide);

		Assert.True(Tracing.Check(guide, Strokes(path[..2], path[1..]), Tracer).Passed);
	}

	[Fact]
	public void Sem_traco_ou_com_so_um_pedaco_nao_passa()
	{
		var guide = GuideOfL();

		Assert.False(Tracing.Check(guide, Strokes(), Tracer).Passed);
		Assert.False(Tracing.Check(guide, Strokes(Along(guide)[..2]), Tracer).Passed);
	}

	[Fact]
	public void Rabisco_longe_da_trilha_nao_passa()
	{
		var scribble = new[] { new Vector2(10, 10), new Vector2(190, 20), new Vector2(15, 60), new Vector2(180, 90) };

		Assert.False(Tracing.Check(GuideOfL(), Strokes(scribble), Tracer).Passed);
	}

	[Fact]
	public void Tracar_a_trilha_e_encher_a_tela_de_rabisco_nao_passa()
	{
		var guide = GuideOfL();
		var noise = Enumerable.Range(0, 60).Select(i => new Vector2(10 + (i % 3), 10 + i * 3)).ToArray();

		Assert.False(Tracing.Check(guide, Strokes(Along(guide), noise), Tracer).Passed);
	}

	// --- #47 Bolsa do Escriba

	static readonly string[] Pool = ["pa", "li", "ma", "ru", "ne", "ki"];

	[Fact]
	public void A_bolsa_do_Escriba_entrega_as_silabas_do_nome_mais_distratores()
	{
		var pieces = SyllableBag.Deal(["ga", "to"], Pool, 3, new Pcg32(3)).Value!;

		Assert.Equal(5, pieces.Count);
		Assert.Contains("ga", pieces);
		Assert.Contains("to", pieces);
		Assert.Equal(3, pieces.Except(["ga", "to"]).Count());
	}

	[Fact]
	public void Distrator_nunca_repete_silaba_do_nome_nem_a_si_mesmo()
	{
		var pieces = SyllableBag.Deal(["pa", "to"], Pool, 4, new Pcg32(5)).Value!;

		Assert.Single(pieces, "pa");
		Assert.Equal(pieces.Count, pieces.Distinct().Count());
	}

	[Fact]
	public void Silaba_repetida_no_nome_aparece_o_tanto_de_vezes_que_precisa()
	{
		var pieces = SyllableBag.Deal(["ba", "ta", "ta"], Pool, 1, new Pcg32(5)).Value!;

		Assert.Equal(2, pieces.Count(p => p == "ta"));
	}

	[Fact]
	public void Silabas_do_Escriba_seguem_a_seed()
	{
		Assert.Equal(SyllableBag.Deal(["ga", "to"], Pool, 3, new Pcg32(9)).Value, SyllableBag.Deal(["ga", "to"], Pool, 3, new Pcg32(9)).Value);
		Assert.True(Enumerable.Range(1, 10).Select(s => string.Join(",", SyllableBag.Deal(["ga", "to"], Pool, 3, new Pcg32((ulong)s)).Value!)).Distinct().Count() > 1);
	}

	[Fact]
	public void Faltar_distrator_no_banco_e_resultado_de_erro()
	{
		Assert.False(SyllableBag.Deal(["ga", "to"], ["pa"], 3, new Pcg32(1)).IsOk);
		Assert.False(SyllableBag.Deal([], Pool, 1, new Pcg32(1)).IsOk);
	}

	// --- #49 Conferir o nome montado

	[Fact]
	public void Nome_completo_certo_acerta()
	{
		var check = NameChecker.Check(["ga", "to"], ["ga", "to"]);

		Assert.True(check.IsComplete);
		Assert.Equal([true, true], check.Correct);
	}

	[Fact]
	public void Aponta_as_silabas_certas_e_as_erradas()
	{
		var check = NameChecker.Check(["ga", "to", "ne"], ["ga", "pa", "ne"]);

		Assert.False(check.IsComplete);
		Assert.Equal([true, false, true], check.Correct);
	}

	[Fact]
	public void Nome_curto_ou_longo_demais_nao_esta_completo()
	{
		Assert.Equal([true, false], NameChecker.Check(["ga", "to"], ["ga"]).Correct);
		Assert.False(NameChecker.Check(["ga", "to"], ["ga", "to", "pa"]).IsComplete);
	}

	[Fact]
	public void Maiuscula_e_minuscula_valem_igual()
	{
		Assert.True(NameChecker.Check(["ga", "to"], ["GA", "To"]).IsComplete);
	}

	// --- Dados de combate

	[Theory]
	[InlineData("\"handSize\": 5", "\"handSize\": 0", "combat.handSize")]
	[InlineData("\"bagCopies\": 2", "\"bagCopies\": 0", "combat.bagCopies")]
	[InlineData("\"slowTimeScale\": 0.25", "\"slowTimeScale\": 1.5", "combat.slowTimeScale")]
	[InlineData("\"slowTimeScale\": 0.25", "\"slowTimeScale\": 0", "combat.slowTimeScale")]
	[InlineData("\"bagNumbers\": [1, 2, 3, 4, 5]", "\"bagNumbers\": []", "combat.bagNumbers")]
	[InlineData("\"bagNumbers\": [1, 2, 3, 4, 5]", "\"bagNumbers\": [1, 1]", "combat.bagNumbers")]
	[InlineData("\"targetRange\": 5", "\"targetRange\": 0", "combat.targetRange")]
	[InlineData("\"targetRange\": 5", "\"targetRange\": 99", "combat.targetRange")]
	[InlineData("\"operators\": [\"+\"]", "\"operators\": []", "combat.operators")]
	[InlineData("\"operators\": [\"+\"]", "\"operators\": [\"^\"]", "combat.operators")]
	[InlineData("\"dotSpacing\": 16", "\"dotSpacing\": 0", "combat.tracing.dotSpacing")]
	[InlineData("\"minCoverage\": 0.8", "\"minCoverage\": 0", "combat.tracing.minCoverage")]
	[InlineData("\"maxStray\": 0.25", "\"maxStray\": 2", "combat.tracing.maxStray")]
	public void Dados_de_combate_invalidos_sao_recusados_dizendo_o_campo(string from, string to, string field)
	{
		Assert.Contains(from, TestJson.Rules);

		var result = GameRules.Parse(TestJson.Rules.Replace(from, to));

		Assert.False(result.IsOk);
		Assert.Contains(field, result.Error);
	}

	[Fact]
	public void Mao_maior_que_a_bolsa_e_recusada()
	{
		var json = TestJson.Rules.Replace("\"handSize\": 5", "\"handSize\": 9").Replace("\"bagCopies\": 2", "\"bagCopies\": 1");

		var result = GameRules.Parse(json);

		Assert.False(result.IsOk);
		Assert.Contains("combat.handSize", result.Error);
	}

	[Theory]
	[InlineData("\"combat\":")]
	[InlineData("\"tracing\":")]
	[InlineData("\"targetRange\":")]
	[InlineData("\"operators\":")]
	public void Dado_de_combate_ausente_e_recusado(string key)
	{
		var result = GameRules.Parse(TestJson.Rules.Replace(key, "\"outro\":"));

		Assert.False(result.IsOk);
		Assert.Contains("combat", result.Error);
	}
}

public class TrainingBattleTests
{
	static readonly CombatRules Rules = GameRules.Parse(TestJson.Rules).Value!.Combat;

	static readonly System.Text.Json.JsonSerializerOptions MissionJson = new() { PropertyNameCaseInsensitive = true };

	static Mission MissionWith(string id, int target, string mechanic = "compor_numero")
	{
		var json = $$"""
			{ "id": "{{id}}", "version": 1, "discipline": "matematica", "concept": "adicao-ate-10", "bncc": ["EF01MA06"], "year": 1,
			  "difficulty": 1, "mechanic": "{{mechanic}}", "params": { "target": {{target}}, "pieces": [1, 2, 3], "maxPieces": 2 },
			  "prompt": "p", "hints": ["h"], "explanation": "e", "accessibility": { "timerAllowed": false }, "review": { "status": "pending" } }
			""";
		return System.Text.Json.JsonSerializer.Deserialize<Mission>(json, MissionJson)!;
	}

	static void Build(TrainingBattle battle)
	{
		battle.Staff.Place(0);
		battle.Staff.PlaceOperator("+");
		battle.Staff.Place(1);
	}

	[Fact]
	public void O_escudo_vem_do_alvo_de_uma_Missao_carregada()
	{
		var battle = TrainingBattle.Start(Rules, [MissionWith("a", 7)], new Pcg32(1)).Value!;

		Assert.Equal(7, battle.Shield.Remaining);
		Assert.Equal("a", battle.Mission.Id);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(7)]
	[InlineData(10)]
	public void A_mao_sempre_forma_o_escudo_e_nenhum_golpe_deixa_a_Sala_sem_saida(int target)
	{
		for (ulong seed = 1; seed <= 150; seed++)
		{
			var battle = TrainingBattle.Start(Rules, [MissionWith("a", target)], new Pcg32(seed)).Value!;
			for (var turn = 0; turn < 6 && !battle.Shield.IsDissolved; turn++)
			{
				Assert.True(HandForms(battle), $"seed {seed}, escudo {battle.Shield.Remaining}");
				var slots = Enumerable.Range(0, battle.Bag.Hand.Count).Where(i => battle.Bag.Hand[i] is not null).Take(2).ToList();
				battle.Staff.Place(slots[0]);
				battle.Staff.PlaceOperator("+");
				battle.Staff.Place(slots[1]);
				battle.Confirm();
				battle.Staff.Cancel();
			}
		}
	}

	static bool HandForms(TrainingBattle battle)
	{
		var hand = battle.Bag.Hand.Where(p => p is not null).Select(p => p!.Value).ToList();
		return hand.SelectMany((a, i) => hand.Where((_, j) => j != i).Select(b => a + b)).Contains(battle.Shield.Remaining);
	}

	[Fact]
	public void Sem_Missao_de_compor_numero_nao_ha_luta()
	{
		Assert.False(TrainingBattle.Start(Rules, [], new Pcg32(1)).IsOk);
		Assert.False(TrainingBattle.Start(Rules, [MissionWith("a", 7, "formar_palavras")], new Pcg32(1)).IsOk);
	}

	[Fact]
	public void Confirmar_aplica_a_conta_no_escudo()
	{
		var battle = TrainingBattle.Start(Rules, [MissionWith("a", 30)], new Pcg32(1)).Value!;
		var expected = battle.Bag.Hand[0]!.Value + battle.Bag.Hand[1]!.Value;
		Build(battle);

		var result = battle.Confirm().Value!;

		Assert.Equal(HitOutcome.Reduced, result.Outcome);
		Assert.Equal(30 - expected, result.Remaining);
	}

	[Fact]
	public void Passar_do_valor_nao_gasta_nada_e_o_escudo_fica()
	{
		var battle = TrainingBattle.Start(Rules, [MissionWith("a", 1)], new Pcg32(1)).Value!;
		var before = battle.Bag.Hand.ToArray();
		Build(battle);

		var result = battle.Confirm().Value!;

		Assert.Equal(HitOutcome.Overshoot, result.Outcome);
		Assert.Equal(1, battle.Shield.Remaining);
		Assert.Equal(before, battle.Bag.Hand);
	}

	[Fact]
	public void Next_traz_outro_escudo_de_outra_Missao()
	{
		var battle = TrainingBattle.Start(Rules, [MissionWith("a", 3), MissionWith("b", 9)], new Pcg32(4)).Value!;
		var first = battle.Mission.Id;
		battle.Shield.Hit(new ExpressionResult(battle.Shield.Remaining, Comparison.Equal));

		Assert.True(battle.Shield.IsDissolved);
		battle.Next();

		Assert.NotEqual(first, battle.Mission.Id);
		Assert.False(battle.Shield.IsDissolved);
	}

	[Fact]
	public void Confirmar_um_numero_sozinho_e_erro_sem_excecao()
	{
		var battle = TrainingBattle.Start(Rules, [MissionWith("a", 5)], new Pcg32(1)).Value!;
		battle.Staff.Place(0);

		Assert.False(battle.Confirm().IsOk);
	}
}
