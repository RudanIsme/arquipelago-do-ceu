using System.Numerics;
using System.Text.Json;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

static class TrainingFixtures
{
	public static readonly GameRules Game = GameRules.Parse(TestJson.Rules).Value!;
	public static CombatRules Rules => Game.Combat;

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	public static Mission MathMission(string id, int target, int year = 1, int difficulty = 1, string hint = "h")
	{
		var json = $$"""
			{ "id": "{{id}}", "version": 1, "discipline": "matematica", "concept": "adicao-ate-10", "bncc": ["EF01MA06"], "year": {{year}},
			  "difficulty": {{difficulty}}, "mechanic": "compor_numero", "params": { "target": {{target}}, "pieces": [1, 2, 3], "maxPieces": 2 },
			  "prompt": "Faça {{target}}.", "hints": ["{{hint}}1", "{{hint}}2"], "explanation": "e", "accessibility": { "timerAllowed": false }, "review": { "status": "pending" } }
			""";
		return JsonSerializer.Deserialize<Mission>(json, Options)!;
	}

	public static Mission NameMission(string id, string[] syllables, int year = 1, int difficulty = 1, int distractors = 2)
	{
		var name = string.Join(",", syllables.Select(s => $"\"{s}\""));
		var json = $$"""
			{ "id": "{{id}}", "version": 1, "discipline": "portugues", "concept": "palavras-dissilabas-grupo-1", "bncc": ["EF01LP08"], "year": {{year}},
			  "difficulty": {{difficulty}}, "mechanic": "formar_palavras",
			  "params": { "syllables": [{{name}}], "pool": ["ma", "me", "mi", "mo", "mu", "pa", "pe", "pi", "po", "pu", "ta", "te"], "distractors": {{distractors}}, "image": "{{id}}" },
			  "prompt": "Monte o nome.", "hints": ["dica um", "dica dois"], "explanation": "e", "accessibility": { "timerAllowed": false }, "review": { "status": "pending" } }
			""";
		return JsonSerializer.Deserialize<Mission>(json, Options)!;
	}

	public static Result<TrainingSession> Start(string discipline, IEnumerable<Mission> missions, int level = 1, int year = 1, int difficulty = 1, ulong seed = 1) =>
		TrainingSession.Start(Rules, Game.MaxEnergy, level, new TrainingSetup(discipline, year, difficulty), missions, new Pcg32(seed));

	public static TrainingSession Math(params Mission[] missions) => Start(TrainingSetup.Mathematics, missions).Value!;

	public static TrainingSession Name(params Mission[] missions) => Start(TrainingSetup.Portuguese, missions).Value!;
}

public class EnergyTests
{
	[Fact]
	public void Comeca_cheia_com_o_maximo_dos_dados()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 9));

		Assert.Equal(5, session.Energy.Current);
		Assert.Equal(TrainingFixtures.Game.MaxEnergy, session.Energy.Max);
	}

	[Fact]
	public void Nunca_passa_do_maximo_nem_fica_abaixo_de_zero()
	{
		var energy = new Energy(5);

		energy.Recover(2);
		Assert.Equal(5, energy.Current);

		energy.Lose(9);
		Assert.Equal(0, energy.Current);
		Assert.True(energy.IsEmpty);
	}

	[Fact]
	public void Acerto_recupera_1_golpe_perfeito_recupera_2_erro_e_bolha_tiram_1()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 9), TrainingFixtures.MathMission("b", 8));

		session.Mistake();
		session.Mistake();
		Assert.Equal(3, session.Energy.Current);

		session.Hit(perfect: false);
		Assert.Equal(4, session.Energy.Current);

		session.BubbleHit();
		Assert.Equal(3, session.Energy.Current);

		session.Hit(perfect: true);
		Assert.Equal(5, session.Energy.Current);
	}

	[Fact]
	public void Zerar_a_Energia_encerra_a_sessao_sem_travessura()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 9), TrainingFixtures.MathMission("b", 8));

		MistakeOutcome last = null!;
		for (var i = 0; i < 5; i++) last = session.Mistake();

		Assert.True(last.Empty);
		Assert.True(session.IsOver);
		Assert.Null(last.Prank);
	}

	[Fact]
	public void Bolha_que_zera_a_Energia_encerra_a_sessao()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 9));

		var empty = false;
		for (var i = 0; i < 5; i++) empty = session.BubbleHit();

		Assert.True(empty);
		Assert.True(session.IsOver);
	}
}

public class TrainingChoiceTests
{
	static readonly Mission[] All =
	[
		TrainingFixtures.MathMission("m1a", 5, year: 1, difficulty: 1),
		TrainingFixtures.MathMission("m1b", 6, year: 1, difficulty: 2),
		TrainingFixtures.MathMission("m2a", 7, year: 2, difficulty: 1),
		TrainingFixtures.NameMission("p1a", ["pa", "to"], year: 1, difficulty: 1),
		TrainingFixtures.NameMission("p1b", ["ma", "pa"], year: 1, difficulty: 2),
	];

	[Fact]
	public void Perfil_novo_so_ve_o_Nivel_1()
	{
		var path = Path.Combine(Path.GetTempPath(), "arquipelago-treino-" + Guid.NewGuid().ToString("N"), "save.json");
		var profile = new ProfileService(new SaveSession(new SaveStore(path), SaveData.Empty), TrainingFixtures.Game, () => "id-1").Create("Luna").Value!;

		Assert.Equal([1], TrainingSetup.Levels(profile.Level));
	}

	[Theory]
	[InlineData(2, new[] { 1, 2 })]
	[InlineData(3, new[] { 1, 2, 3 })]
	[InlineData(9, new[] { 1, 2, 3 })]
	public void Mostra_so_os_Niveis_liberados(int unlocked, int[] expected) => Assert.Equal(expected, TrainingSetup.Levels(unlocked));

	[Fact]
	public void Nivel_nao_liberado_e_recusado()
	{
		var result = TrainingFixtures.Start(TrainingSetup.Mathematics, All, level: 1, year: 2);

		Assert.False(result.IsOk);
		Assert.Contains("liberado", result.Error);
	}

	[Fact]
	public void Escolher_o_Papel_muda_a_Mecanica_e_as_Missoes()
	{
		var math = TrainingFixtures.Start(TrainingSetup.Mathematics, All).Value!;
		var name = TrainingFixtures.Start(TrainingSetup.Portuguese, All).Value!;

		Assert.NotNull(math.Math);
		Assert.Null(math.Name);
		Assert.Equal("matematica", math.Mission.Discipline);
		Assert.NotNull(name.Name);
		Assert.Null(name.Math);
		Assert.Equal("portugues", name.Mission.Discipline);
	}

	[Fact]
	public void A_dificuldade_escolhida_limita_as_Missoes_sorteadas()
	{
		for (ulong seed = 1; seed <= 20; seed++)
		{
			var session = TrainingFixtures.Start(TrainingSetup.Mathematics, All, difficulty: 2, seed: seed).Value!;
			Assert.Equal("m1b", session.Mission.Id);
			session.NextChallenge();
			Assert.Equal("m1b", session.Mission.Id);
		}
	}

	[Fact]
	public void Sem_Missao_para_a_dificuldade_o_jogo_avisa_em_vez_de_falhar()
	{
		var result = TrainingFixtures.Start(TrainingSetup.Mathematics, All, difficulty: 5);

		Assert.False(result.IsOk);
		Assert.Contains("dificuldade", result.Error);
	}

	[Fact]
	public void O_sorteio_e_reproduzivel_pela_seed()
	{
		var many = Enumerable.Range(1, 12).Select(i => TrainingFixtures.MathMission($"m{i}", i + 5)).ToList();

		List<string> Draw(ulong seed)
		{
			var session = TrainingFixtures.Start(TrainingSetup.Mathematics, many, seed: seed).Value!;
			var ids = new List<string> { session.Mission.Id };
			for (var i = 0; i < 6; i++)
			{
				session.NextChallenge();
				ids.Add(session.Mission.Id);
			}
			return ids;
		}

		Assert.Equal(Draw(7), Draw(7));
		Assert.NotEqual(Draw(7), Draw(8));
	}
}

public class MistakeTests
{
	[Fact]
	public void Depois_de_um_erro_o_Desafio_recomeca_com_outros_numeros()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 7), TrainingFixtures.MathMission("b", 9));
		var first = session.Mission.Id;

		session.Mistake();

		Assert.NotEqual(first, session.Mission.Id);
		Assert.Equal(session.Mission.Params["target"].GetInt32(), session.Math!.Shield.Remaining);
	}

	[Fact]
	public void Depois_de_um_erro_o_Escriba_recebe_outro_nome()
	{
		var session = TrainingFixtures.Name(TrainingFixtures.NameMission("a", ["pa", "to"]), TrainingFixtures.NameMission("b", ["ma", "pa"]));
		var first = session.Name!.Word;

		session.Mistake();

		Assert.NotEqual(first, session.Name.Word);
	}

	[Fact]
	public void O_erro_dispara_a_travessura()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 7), TrainingFixtures.MathMission("b", 9));

		var outcome = session.Mistake();

		Assert.NotNull(outcome.Prank);
	}

	[Fact]
	public void Dois_erros_seguidos_oferecem_a_dica_e_acertar_zera_a_conta()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 7), TrainingFixtures.MathMission("b", 9));

		Assert.False(session.Mistake().HintOffered);
		Assert.True(session.Mistake().HintOffered);

		session.Hit(perfect: false);
		Assert.False(session.HintOffered);
		Assert.False(session.Mistake().HintOffered);
	}

	[Fact]
	public void Pedir_a_dica_nunca_custa_Energia_nem_precisa_de_erro()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 7, hint: "dica"));

		Assert.Equal("dica1", session.Hint());
		Assert.Equal("dica2", session.Hint());
		Assert.Equal("dica2", session.Hint());
		Assert.Equal(session.Energy.Max, session.Energy.Current);
	}

	[Fact]
	public void Passar_do_valor_nao_gasta_as_pecas_da_mao()
	{
		var session = TrainingFixtures.Math(TrainingFixtures.MathMission("a", 1), TrainingFixtures.MathMission("b", 1));
		var battle = session.Math!;
		var hand = battle.Bag.Hand.ToArray();
		battle.Staff.Place(0);
		battle.Staff.PlaceOperator("+");
		battle.Staff.Place(1);

		var attack = battle.Confirm().Value!;

		Assert.Equal(HitOutcome.Overshoot, attack.Outcome);
		Assert.Equal(hand, battle.Bag.Hand);
		Assert.Equal($"{hand[0]} + {hand[1]} = {hand[0] + hand[1]}", attack.Equation);
	}
}

public class PrankTests
{
	static MathBag Bag(ulong seed) => new(TrainingFixtures.Rules, new Pcg32(seed));

	[Fact]
	public void Embaralhar_ou_esconder_nunca_perde_peca()
	{
		for (ulong seed = 1; seed <= 30; seed++)
		{
			var bag = Bag(seed);
			var before = bag.Hand.Select(p => p!.Value).OrderBy(p => p).ToList();

			bag.PlayPrank();
			bag.Reveal();

			Assert.Equal(before, bag.Hand.Select(p => p!.Value).OrderBy(p => p).ToList());
			Assert.Equal(0, bag.HiddenCount);
		}
	}

	[Fact]
	public void Peca_escondida_some_da_mao_e_volta_sozinha_no_mesmo_lugar()
	{
		for (ulong seed = 1; seed <= 30; seed++)
		{
			var bag = Bag(seed);
			if (bag.PlayPrank() != Prank.Hidden) continue;

			Assert.Equal(1, bag.HiddenCount);
			var slot = Array.FindIndex(bag.Hand.ToArray(), p => p is null);
			Assert.True(slot >= 0);
			var count = bag.Hand.Count(p => p is not null);
			Assert.Equal(4, count);

			bag.Reveal();

			Assert.NotNull(bag.Hand[slot]);
			return;
		}
		Assert.Fail("Nenhuma seed escondeu uma peça.");
	}

	[Fact]
	public void Peca_escondida_nao_e_reposta_por_outra_enquanto_some()
	{
		for (ulong seed = 1; seed <= 30; seed++)
		{
			var bag = Bag(seed);
			if (bag.PlayPrank() != Prank.Hidden) continue;
			var staff = new Staff(bag, TrainingFixtures.Rules);
			int FreeSlot() => Enumerable.Range(0, 5).First(s => bag.Hand[s] is not null);
			staff.Place(FreeSlot());
			staff.PlaceOperator("+");
			staff.Place(FreeSlot());

			staff.Confirm(shield: 99);

			Assert.Equal(1, bag.Hand.Count(p => p is null));
			return;
		}
		Assert.Fail("Nenhuma seed escondeu uma peça.");
	}

	[Fact]
	public void Travessura_nunca_mexe_nas_pecas_que_estao_no_cajado()
	{
		var bag = Bag(3);
		var staff = new Staff(bag, TrainingFixtures.Rules);
		staff.Place(0);

		bag.PlayPrank();
		staff.Undo();

		Assert.NotNull(bag.Hand[0]);
	}

	[Fact]
	public void Travessura_de_nome_embaralha_ou_esconde_e_a_peca_escondida_volta()
	{
		for (ulong seed = 1; seed <= 30; seed++)
		{
			var session = TrainingFixtures.Start(TrainingSetup.Portuguese, [TrainingFixtures.NameMission("a", ["pa", "to"], distractors: 3)], seed: seed).Value!;
			var name = session.Name!;
			var before = name.Pieces.OrderBy(p => p).ToList();

			var prank = name.PlayPrank();
			var hidden = Enumerable.Range(0, name.Pieces.Count).Count(name.IsHidden);
			session.RevealHidden();

			Assert.Equal(before, name.Pieces.OrderBy(p => p).ToList());
			Assert.Equal(prank == Prank.Hidden ? 1 : 0, hidden);
			Assert.Equal(0, Enumerable.Range(0, name.Pieces.Count).Count(name.IsHidden));
		}
	}
}

public class NameBattleTests
{
	static NameBattle Start(params Mission[] missions) =>
		NameBattle.Start(missions, new Pcg32(1)).Value!;

	static int PieceFor(NameBattle battle, string syllable) => battle.Pieces.ToList().IndexOf(syllable);

	static int Wrong(NameBattle battle) => battle.Pieces.ToList().FindIndex(p => !battle.Name.Contains(p));

	[Fact]
	public void O_nome_vem_da_Missao_do_Escriba_com_imagem_e_distratores()
	{
		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"], distractors: 2));

		Assert.Equal("pato", battle.Word);
		Assert.Equal("pato", battle.Image);
		Assert.Equal(4, battle.Pieces.Count);
		Assert.Equal(2, battle.Remaining);
	}

	[Fact]
	public void Cada_silaba_certa_e_um_golpe_e_o_nome_completo_liberta()
	{
		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"]));

		battle.Place(PieceFor(battle, "pa"));
		var first = battle.Check();
		Assert.Equal(1, first.NewStrikes);
		Assert.False(first.Freed);
		Assert.False(first.Mistaken);
		Assert.Equal(1, battle.Remaining);

		battle.Place(PieceFor(battle, "to"));
		var second = battle.Check();
		Assert.Equal(1, second.NewStrikes);
		Assert.True(second.Freed);
		Assert.Equal(0, battle.Remaining);
	}

	[Fact]
	public void Conferir_de_novo_nao_repete_o_golpe()
	{
		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"]));
		battle.Place(PieceFor(battle, "pa"));
		battle.Check();

		Assert.Equal(0, battle.Check().NewStrikes);
	}

	[Fact]
	public void Silaba_errada_e_erro_e_silaba_que_falta_nao_e()
	{
		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"]));

		battle.Place(PieceFor(battle, "pa"));
		Assert.False(battle.Check().Mistaken);

		battle.Place(Wrong(battle));
		var verdict = battle.Check();

		Assert.True(verdict.Mistaken);
		Assert.False(verdict.Freed);
	}

	[Fact]
	public void Desfazer_antes_de_conferir_nao_custa_nada()
	{
		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"]));
		battle.Place(Wrong(battle));

		Assert.True(battle.Undo());

		Assert.Empty(battle.Chosen);
		Assert.False(battle.Check().Mistaken);
	}

	[Fact]
	public void O_novo_nome_e_outro_e_recomeca_do_zero()
	{
		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"]), TrainingFixtures.NameMission("mapa", ["ma", "pa"]));
		var first = battle.Word;
		battle.Place(PieceFor(battle, battle.Name[0]));
		battle.Check();

		battle.Next();

		Assert.NotEqual(first, battle.Word);
		Assert.Empty(battle.Chosen);
		Assert.Equal(2, battle.Remaining);
	}

	[Fact]
	public void Missao_mal_escrita_e_pulada_sem_travar_a_luta()
	{
		var bad = TrainingFixtures.NameMission("ruim", ["pa", "to"], distractors: 50);

		Assert.False(NameBattle.Start([bad], new Pcg32(1)).IsOk);

		var battle = Start(TrainingFixtures.NameMission("pato", ["pa", "to"]), TrainingFixtures.NameMission("mapa", ["ma", "pa"]));
		battle.Next();
		Assert.NotNull(battle.Word);
	}


	[Fact]
	public void Se_nenhuma_outra_Missao_serve_o_mesmo_nome_recomeca_do_zero()
	{
		var good = TrainingFixtures.NameMission("pato", ["pa", "to"]);
		var bad = TrainingFixtures.NameMission("ruim", ["ma", "pa"], distractors: 50);
		for (ulong seed = 1; seed <= 20; seed++)
		{
			if (NameBattle.Start([good, bad], new Pcg32(seed)) is not { IsOk: true, Value: { } battle }) continue;
			battle.Place(Wrong(battle));
			battle.Check();

			battle.Next();

			Assert.Equal("pato", battle.Word);
			Assert.Empty(battle.Chosen);
			Assert.Equal(2, battle.Remaining);
			return;
		}
		Assert.Fail("Nenhuma seed começou com a Missão boa.");
	}
	[Fact]
	public void Sem_Missao_de_formar_palavras_nao_ha_luta()
	{
		Assert.False(NameBattle.Start([], new Pcg32(1)).IsOk);
		Assert.False(NameBattle.Start([TrainingFixtures.MathMission("a", 5)], new Pcg32(1)).IsOk);
	}
}

public class BubbleTests
{
	static readonly BubbleRules Rules = TrainingFixtures.Rules.Bubble;
	static readonly Vector2 Dummy = Vector2.Zero;

	// Avança o relógio em passos de 0,1 s até o evento pedido; devolve o tempo gasto.
	static float Until(BubbleAttack bubble, BubbleEvent wanted, Func<float, Vector2> player)
	{
		var t = 0f;
		for (var i = 0; i < 2000; i++)
		{
			t += 0.1f;
			if (bubble.Update(0.1f, Dummy, player(t)) == wanted) return t;
		}
		throw new Xunit.Sdk.XunitException($"O evento {wanted} não aconteceu.");
	}

	static Func<float, Vector2> Still(Vector2 at) => _ => at;

	[Fact]
	public void A_bolha_so_sai_depois_de_um_aviso()
	{
		var bubble = new BubbleAttack(Rules);
		var player = Still(new Vector2(5, 0));

		var warned = Until(bubble, BubbleEvent.Warned, player);
		Assert.Equal(BubblePhase.Warning, bubble.Phase);

		var launched = Until(bubble, BubbleEvent.Launched, player);
		Assert.InRange(warned, Rules.IntervalSeconds - 0.15f, Rules.IntervalSeconds + 0.15f);
		Assert.InRange(launched, Rules.WarningSeconds - 0.15f, Rules.WarningSeconds + 0.15f);
	}

	[Fact]
	public void A_bolha_e_lenta_e_da_tempo_de_desviar()
	{
		var bubble = new BubbleAttack(Rules);
		var player = Still(new Vector2(5, 0));
		Until(bubble, BubbleEvent.Launched, player);

		var flight = Until(bubble, BubbleEvent.Hit, player);

		Assert.InRange(flight, (5f - Rules.Radius) / Rules.Speed - 0.2f, (5f - Rules.Radius) / Rules.Speed + 0.2f);
		Assert.True(flight > 2f);
	}

	[Fact]
	public void Sair_do_caminho_evita_a_bolha()
	{
		var bubble = new BubbleAttack(Rules);
		Until(bubble, BubbleEvent.Launched, Still(new Vector2(5, 0)));

		Until(bubble, BubbleEvent.Missed, Still(new Vector2(5, Rules.Radius * 3)));

		Assert.Equal(BubblePhase.Waiting, bubble.Phase);
	}

	[Fact]
	public void A_mira_trava_quando_o_aviso_comeca_e_a_bolha_nao_persegue_a_crianca()
	{
		var bubble = new BubbleAttack(Rules);
		Until(bubble, BubbleEvent.Warned, Still(new Vector2(5, 0)));
		Until(bubble, BubbleEvent.Launched, Still(new Vector2(5, 9)));

		bubble.Update(0.1f, Dummy, new Vector2(5, 9));

		Assert.Equal(0f, bubble.Aim.Y);
		Assert.Equal(0f, bubble.Position.Y);
		Assert.Equal(Vector2.UnitX, bubble.DirectionFrom(Dummy));
	}

	[Fact]
	public void Sair_do_caminho_durante_o_aviso_ja_basta_para_desviar()
	{
		var bubble = new BubbleAttack(Rules);
		Until(bubble, BubbleEvent.Warned, Still(new Vector2(5, 0)));
		var aside = Still(new Vector2(5, Rules.Radius * 3));

		Until(bubble, BubbleEvent.Launched, aside);
		Until(bubble, BubbleEvent.Missed, aside);
	}


	[Fact]
	public void A_mesma_regra_gera_sempre_o_mesmo_tempo()
	{
		var player = Still(new Vector2(5, 0));
		var first = Until(new BubbleAttack(Rules), BubbleEvent.Hit, player);
		var second = Until(new BubbleAttack(Rules), BubbleEvent.Hit, player);

		Assert.Equal(first, second);
	}

	[Fact]
	public void Reset_desfaz_a_bolha_no_ar()
	{
		var bubble = new BubbleAttack(Rules);
		Until(bubble, BubbleEvent.Launched, Still(new Vector2(5, 0)));

		bubble.Reset();

		Assert.Equal(BubblePhase.Waiting, bubble.Phase);
	}
}

public class CombatRulesDataTests
{
	[Theory]
	[InlineData("\"mistakesForHint\": 2", "\"mistakesForHint\": 0")]
	[InlineData("\"prankSeconds\": 4", "\"prankSeconds\": 99")]
	[InlineData("\"hit\": 1,", "\"hit\": 0,")]
	[InlineData("\"speed\": 1.5", "\"speed\": 9")]
	[InlineData("\"warningSeconds\": 1.6", "\"warningSeconds\": 0.1")]
	public void Valor_fora_do_limite_e_recusado(string from, string to)
	{
		var result = GameRules.Parse(TestJson.Rules.Replace(from, to));

		Assert.False(result.IsOk);
		Assert.Contains("combat", result.Error);
	}

	[Theory]
	[InlineData("\"energy\":")]
	[InlineData("\"bubble\":")]
	[InlineData("\"mistakesForHint\":")]
	public void Dado_ausente_e_recusado(string key)
	{
		var result = GameRules.Parse(TestJson.Rules.Replace(key, "\"outro\":"));

		Assert.False(result.IsOk);
		Assert.Contains("combat", result.Error);
	}
}

// O treino não conta para o progresso (#69): o combate não conhece o save e um treino completo deixa o arquivo idêntico.
public sealed class TrainingProgressTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-treino-save-" + Guid.NewGuid().ToString("N"));

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	[Fact]
	public void Um_treino_completo_nao_muda_nada_do_save()
	{
		var path = Path.Combine(folder, "save.json");
		var store = new SaveStore(path);
		var session = new SaveSession(store, SaveData.Empty);
		var profiles = new ProfileService(session, TrainingFixtures.Game, () => "id-1");
		Assert.True(profiles.Create("Luna").IsOk);
		var dataBefore = session.Data;
		var fileBefore = File.ReadAllBytes(path);

		PlayCompleteTraining(TrainingSetup.Mathematics);
		PlayCompleteTraining(TrainingSetup.Portuguese);

		Assert.Same(dataBefore, session.Data);
		Assert.Equal(fileBefore, File.ReadAllBytes(path));
		Assert.Equal(dataBefore.Profiles, store.Load().Value!.Data.Profiles);
	}

	[Fact]
	public void O_combate_do_treino_nao_referencia_o_save_nem_o_perfil()
	{
		var combat = Path.Combine(Root(), "core", "Combat");
		var offenders = Directory.EnumerateFiles(combat, "*.cs")
			.Where(f => File.ReadAllText(f) is var text && (text.Contains("ArquipelagoDoCeu.Saves") || text.Contains("ArquipelagoDoCeu.Profiles")))
			.ToList();

		Assert.Empty(offenders);
	}

	static void PlayCompleteTraining(string discipline)
	{
		var missions = new[]
		{
			TrainingFixtures.MathMission("a", 3), TrainingFixtures.MathMission("b", 4),
			TrainingFixtures.NameMission("p", ["pa", "to"]), TrainingFixtures.NameMission("q", ["ma", "pa"]),
		};
		var training = TrainingFixtures.Start(discipline, missions).Value!;
		training.Mistake();
		training.Hit(perfect: false);
		training.Hint();
		training.Hit(perfect: true);
		training.BubbleHit();
		training.NextChallenge();
		while (!training.IsOver) training.Mistake();
	}

	static string Root()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
			dir = dir.Parent;
		return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
	}
}

// As Missões do Escriba escritas à mão (#70): cada Conceito tem Missões em várias dificuldades e todas viram uma luta jogável.
public class PortugueseContentTests
{
	static readonly MissionLoad Loaded = MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!.Load(MissionFiles.All());

	static IEnumerable<Mission> Words => Loaded.Missions.Where(m => m.Mechanic == "formar_palavras");

	[Theory]
	[InlineData("segmentacao-silabica-oral")]
	[InlineData("vogais-letra-som")]
	[InlineData("nome-e-som-das-letras")]
	[InlineData("palavras-dissilabas-grupo-1")]
	public void Conceito_do_1o_ano_tem_Missoes_em_varias_dificuldades(string concept)
	{
		var missions = Words.Where(m => m.Concept == concept).ToList();

		Assert.True(missions.Count >= 6);
		Assert.True(missions.Select(m => m.Difficulty).Distinct().Count() >= 3);
		Assert.All(missions, m =>
		{
			Assert.Equal("portugues", m.Discipline);
			Assert.Equal(1, m.Year);
		});
	}

	[Fact]
	public void Toda_Missao_de_palavras_vira_uma_luta_com_imagem_e_o_nome_certo_na_bolsa()
	{
		foreach (var mission in Words)
		{
			var battle = NameBattle.Start([mission], new Pcg32(1));

			Assert.True(battle.IsOk, $"{mission.Id}: {battle.Error}");
			Assert.False(string.IsNullOrWhiteSpace(battle.Value!.Image));
			Assert.All(battle.Value.Name, s => Assert.Contains(s, battle.Value.Pieces));
		}
	}

	[Fact]
	public void As_palavras_so_usam_letras_do_primeiro_grupo_e_as_vogais()
	{
		var allowed = "mptn" + "aeiou" + "v";
		Assert.All(Words, m =>
			Assert.All(m.Params["syllables"].EnumerateArray().Select(s => s.GetString()!), s => Assert.All(s, c => Assert.Contains(c, allowed))));
	}
}
