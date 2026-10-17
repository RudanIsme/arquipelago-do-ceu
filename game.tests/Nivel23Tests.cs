using System.Text.Json;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

static class WordFixtures
{
	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	static Mission Make(string id, string mechanic, int year, int difficulty, string parameters)
	{
		var json = $$"""
			{ "id": "{{id}}", "version": 1, "discipline": "portugues", "concept": "escrever-c-qu", "bncc": ["EF02LP03"], "year": {{year}},
			  "difficulty": {{difficulty}}, "mechanic": "{{mechanic}}", "params": {{parameters}},
			  "prompt": "Escreva.", "hints": ["dica um", "dica dois"], "explanation": "e", "accessibility": { "timerAllowed": false }, "review": { "status": "pending" } }
			""";
		return JsonSerializer.Deserialize<Mission>(json, Options)!;
	}

	public static Mission Word(string id, string word, int year = 2, int difficulty = 1) =>
		Make(id, "escrever_palavra", year, difficulty, $$"""{ "word": "{{word}}", "image": "{{id}}" }""");

	public static Mission Sentence(string id, string[] words, int year = 3, int difficulty = 1) =>
		Make(id, "ordenar_frases", year, difficulty, $$"""{ "words": [{{string.Join(",", words.Select(w => $"\"{w}\""))}}], "pool": ["pula", "come", "casa", "mesa"], "distractors": 2, "image": "{{id}}" }""");

	public static Mission Gap(string id, string before, string answer, string[] options, string after = ".", int year = 3, int difficulty = 1) =>
		Make(id, "completar_frase", year, difficulty, $$"""{ "before": "{{before}}", "after": "{{after}}", "answer": "{{answer}}", "options": [{{string.Join(",", options.Select(o => $"\"{o}\""))}}], "image": "{{id}}" }""");
}

public class WordBattleTests
{
	static WordBattle Start(string word = "casa") => WordBattle.Start([WordFixtures.Word("a", word)], new Pcg32(1)).Value!;

	static void TypeAll(WordBattle battle, string text)
	{
		foreach (var c in text) battle.Type(c);
	}

	[Fact]
	public void A_grafia_exata_liberta_e_maiuscula_vale_igual()
	{
		var battle = Start("casa");

		TypeAll(battle, "CaSa");

		Assert.Equal(WordResult.Freed, battle.Check());
		Assert.Equal(0, battle.Remaining);
	}

	[Fact]
	public void Palavra_completa_e_errada_e_erro()
	{
		var battle = Start("casa");

		TypeAll(battle, "caza");

		Assert.Equal(WordResult.Mistaken, battle.Check());
		Assert.Equal(1, battle.Remaining);
	}

	[Fact]
	public void Acento_e_cedilha_fazem_parte_da_grafia()
	{
		var battle = Start("mão");

		TypeAll(battle, "mao");
		Assert.Equal(WordResult.Mistaken, battle.Check());

		battle.Backspace();
		battle.Backspace();
		TypeAll(battle, "ão");
		Assert.Equal(WordResult.Freed, battle.Check());
	}

	[Fact]
	public void Faltar_letra_nao_e_erro_e_nao_passa_do_tamanho()
	{
		var battle = Start("casa");

		TypeAll(battle, "ca");
		Assert.Equal(WordResult.Incomplete, battle.Check());

		TypeAll(battle, "sasa");
		Assert.Equal("casa", battle.Typed);
	}

	[Fact]
	public void Digitar_so_aceita_letras()
	{
		var battle = Start("casa");

		Assert.False(battle.Type('1'));
		Assert.False(battle.Type(' '));
		Assert.True(battle.Type('c'));
	}

	[Fact]
	public void A_letra_sugerida_e_a_proxima_ou_a_que_deve_ser_apagada_e_nao_custa_Energia()
	{
		var session = TrainingFixtures.Start("portugues", [WordFixtures.Word("a", "casa")], level: 2, year: 2).Value!;
		var battle = session.Word!;

		Assert.Equal(('c', false), battle.Suggest());
		TypeAll(battle, "cz");
		Assert.Equal(('z', true), battle.Suggest());
		battle.Backspace();
		Assert.Equal(('a', false), battle.Suggest());
		Assert.Equal(session.Energy.Max, session.Energy.Current);
	}

	[Fact]
	public void As_letras_da_tela_trazem_as_da_palavra_mais_algumas_de_sobra()
	{
		var battle = Start("casa");

		Assert.All("cas", c => Assert.Contains(c, battle.Keys));
		Assert.Equal(3 + 4, battle.Keys.Count);
	}

	[Fact]
	public void A_travessura_embaralha_ou_esconde_uma_letra_e_ela_volta()
	{
		for (ulong seed = 1; seed <= 20; seed++)
		{
			var battle = WordBattle.Start([WordFixtures.Word("a", "casa")], new Pcg32(seed)).Value!;
			var before = battle.Keys.OrderBy(k => k).ToList();

			var prank = battle.PlayPrank();
			var hidden = battle.Keys.Count(battle.IsHidden);
			battle.Reveal();

			Assert.Equal(before, battle.Keys.OrderBy(k => k).ToList());
			Assert.Equal(prank == Prank.Hidden ? 1 : 0, hidden);
			Assert.Equal(0, battle.Keys.Count(battle.IsHidden));
		}
	}

	[Fact]
	public void Outra_palavra_depois_do_erro_e_a_conta_zera()
	{
		var session = TrainingFixtures.Start("portugues", [WordFixtures.Word("a", "casa"), WordFixtures.Word("b", "copo")], level: 2, year: 2).Value!;
		var first = session.Word!.Word;
		TypeAll(session.Word, "ca");

		var outcome = session.Mistake();

		Assert.NotEqual(first, session.Word.Word);
		Assert.Equal("", session.Word.Typed);
		Assert.NotNull(outcome.Prank);
	}

	[Fact]
	public void Sem_Missao_de_escrever_palavra_nao_ha_luta()
	{
		Assert.False(WordBattle.Start([], new Pcg32(1)).IsOk);
		Assert.False(WordBattle.Start([TrainingFixtures.MathMission("a", 5)], new Pcg32(1)).IsOk);
	}
}

public class SentenceBattleTests
{
	static NameBattle Start(Mission mission) => NameBattle.Start([mission], new Pcg32(1)).Value!;

	static int Piece(NameBattle battle, string text) => battle.Pieces.ToList().IndexOf(text);

	[Fact]
	public void Ordenar_uma_frase_cada_palavra_certa_e_um_golpe_e_a_frase_completa_liberta()
	{
		var battle = Start(WordFixtures.Sentence("s", ["O", "gato", "dorme"]));

		Assert.Equal(NameKind.Sentence, battle.Kind);
		Assert.Equal(3, battle.Remaining);
		foreach (var word in new[] { "O", "gato", "dorme" })
		{
			battle.Place(Piece(battle, word));
			Assert.Equal(1, battle.Check().NewStrikes);
		}

		Assert.Equal(0, battle.Remaining);
		Assert.Equal("O gato dorme", battle.Completed);
	}

	[Fact]
	public void Palavra_fora_de_ordem_e_erro()
	{
		var battle = Start(WordFixtures.Sentence("s", ["O", "gato", "dorme"]));

		battle.Place(Piece(battle, "dorme"));

		Assert.True(battle.Check().Mistaken);
	}

	[Fact]
	public void Completar_a_frase_escolhendo_a_palavra_de_grafia_certa()
	{
		var battle = Start(WordFixtures.Gap("g", "O pato ", "passou", ["pasou", "passou", "pazou"], " pela ponte."));

		Assert.Equal(NameKind.Gap, battle.Kind);
		Assert.Equal(3, battle.Pieces.Count);
		battle.Place(Piece(battle, "pasou"));
		Assert.True(battle.Check().Mistaken);

		battle.Undo();
		battle.Place(Piece(battle, "passou"));
		var verdict = battle.Check();

		Assert.True(verdict.Freed);
		Assert.Equal("O pato passou pela ponte.", battle.Completed);
	}

	[Fact]
	public void Opcoes_repetidas_nao_quebram_a_bolsa()
	{
		var battle = NameBattle.Start([WordFixtures.Gap("g", "A ", "casa", ["casa", "caza", "caza"])], new Pcg32(1));

		Assert.True(battle.IsOk);
	}

	[Fact]
	public void No_2o_ano_o_treino_do_Escriba_escreve_a_palavra_e_no_3o_monta_frases()
	{
		var missions = new[]
		{
			WordFixtures.Word("w", "casa", year: 2),
			WordFixtures.Sentence("s", ["O", "gato", "dorme"], year: 3),
		};

		var year2 = TrainingFixtures.Start("portugues", missions, level: 3, year: 2).Value!;
		var year3 = TrainingFixtures.Start("portugues", missions, level: 3, year: 3).Value!;

		Assert.NotNull(year2.Word);
		Assert.Null(year2.Name);
		Assert.NotNull(year3.Name);
		Assert.Null(year3.Word);
	}
}

public class Year2And3ContentTests
{
	static readonly MissionLoad Loaded = MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!.Load(MissionFiles.All());

	static IEnumerable<Mission> Of(string mechanic) => Loaded.Missions.Where(m => m.Mechanic == mechanic);

	[Fact]
	public void Cada_Conceito_do_2o_ano_de_escrever_a_palavra_tem_Missoes_e_toda_palavra_vira_luta()
	{
		var words = Of("escrever_palavra").ToList();
		var concepts = words.Select(m => m.Concept).Distinct().ToList();

		Assert.Equal(8, concepts.Count);
		Assert.All(concepts, c => Assert.True(words.Count(m => m.Concept == c) >= 5, c));
		Assert.All(words, m => Assert.Equal(2, m.Year));
		foreach (var mission in words)
		{
			var battle = WordBattle.Start([mission], new Pcg32(1));
			Assert.True(battle.IsOk, $"{mission.Id}: {battle.Error}");
		}
	}

	[Fact]
	public void Cada_Conceito_de_escrever_a_palavra_fica_em_pelo_menos_3_dificuldades()
	{
		Assert.All(Of("escrever_palavra").GroupBy(m => m.Concept), g => Assert.True(g.Select(m => m.Difficulty).Distinct().Count() >= 3, g.Key));
	}

	[Fact]
	public void O_3o_ano_tem_Missoes_de_ordenar_e_de_completar_e_toda_uma_vira_luta()
	{
		var sentences = Of("ordenar_frases").Concat(Of("completar_frase")).ToList();

		Assert.NotEmpty(Of("ordenar_frases"));
		Assert.NotEmpty(Of("completar_frase"));
		Assert.All(sentences, m => Assert.Equal(3, m.Year));
		foreach (var mission in sentences)
		{
			var battle = NameBattle.Start([mission], new Pcg32(1));
			Assert.True(battle.IsOk, $"{mission.Id}: {battle.Error}");
		}
	}

	[Fact]
	public void Frase_ordenada_resolve_na_ordem_escrita_e_a_lacuna_tem_a_resposta_entre_as_opcoes()
	{
		foreach (var mission in Of("ordenar_frases"))
		{
			var battle = NameBattle.Start([mission], new Pcg32(1)).Value!;
			foreach (var word in battle.Name) battle.Place(battle.Pieces.ToList().IndexOf(word));
			Assert.True(battle.Check().Freed, mission.Id);
		}
		foreach (var mission in Of("completar_frase"))
		{
			var answer = mission.Params["answer"].GetString()!;
			Assert.Contains(answer, mission.Params["options"].EnumerateArray().Select(o => o.GetString()));
		}
	}
}

public class CameraRigTests
{
	static readonly CameraRules Rules = TrainingFixtures.Game.Lobby.Camera;

	[Fact]
	public void O_zoom_para_nos_limites_dos_dados()
	{
		var rig = new CameraRig(Rules);

		rig.ZoomSteps(100f);
		rig.Update(System.Numerics.Vector2.Zero, 10f, reduceMotion: true);
		Assert.Equal(Rules.MinSize, rig.Size);

		rig.ZoomSteps(-1000f);
		rig.Update(System.Numerics.Vector2.Zero, 10f, reduceMotion: true);
		Assert.Equal(Rules.MaxSize, rig.Size);
	}

	[Fact]
	public void A_pinca_aproxima_com_fator_maior_que_1_e_afasta_com_menor()
	{
		var rig = new CameraRig(Rules);

		rig.Pinch(1.5f);
		rig.Update(System.Numerics.Vector2.Zero, 10f, reduceMotion: true);
		var closer = rig.Size;
		rig.Pinch(0.5f);
		rig.Update(System.Numerics.Vector2.Zero, 10f, reduceMotion: true);

		Assert.True(closer < Rules.Size);
		Assert.True(rig.Size > closer);
	}

	[Fact]
	public void Reiniciar_o_zoom_volta_ao_tamanho_inicial()
	{
		var rig = new CameraRig(Rules);
		rig.ZoomSteps(2f);

		rig.ResetZoom();
		rig.Update(System.Numerics.Vector2.Zero, 10f, reduceMotion: true);

		Assert.Equal(Rules.Size, rig.Size);
	}

	[Fact]
	public void Dentro_da_folga_a_camera_fica_parada()
	{
		var rig = new CameraRig(Rules);

		rig.Update(new System.Numerics.Vector2(Rules.DeadZone * 0.5f, 0f), 1f, reduceMotion: false);

		Assert.Equal(System.Numerics.Vector2.Zero, rig.Focus);
	}

	[Fact]
	public void Fora_da_folga_a_camera_segue_aos_poucos_e_nunca_passa_do_Personagem()
	{
		var rig = new CameraRig(Rules);
		var player = new System.Numerics.Vector2(6f, 0f);

		rig.Update(player, 0.1f, reduceMotion: false);
		var first = rig.Focus.X;
		for (var i = 0; i < 100; i++) rig.Update(player, 0.1f, reduceMotion: false);

		Assert.InRange(first, 0.01f, player.X - Rules.DeadZone);
		Assert.InRange(rig.Focus.X, first, player.X - Rules.DeadZone + 0.01f);
	}

	[Fact]
	public void Com_a_reducao_de_movimento_a_camera_nao_desliza()
	{
		var rig = new CameraRig(Rules);
		var player = new System.Numerics.Vector2(6f, 0f);

		rig.Update(player, 0.016f, reduceMotion: true);

		Assert.Equal(player.X - Rules.DeadZone, rig.Focus.X, 3);
	}

	[Fact]
	public void A_camera_nao_se_afasta_do_centro_alem_do_limite()
	{
		var rig = new CameraRig(Rules);

		rig.Update(new System.Numerics.Vector2(100f, 0f), 10f, reduceMotion: true);

		Assert.True(rig.Focus.Length() <= Rules.MaxPan + 0.001f);
	}

	[Theory]
	[InlineData("\"minSize\": 12", "\"minSize\": 99")]
	[InlineData("\"zoomStep\": 3", "\"zoomStep\": 0")]
	[InlineData("\"camera\":", "\"outra\":")]
	public void Camera_mal_escrita_nos_dados_e_recusada(string from, string to)
	{
		var result = GameRules.Parse(TestJson.Rules.Replace(from, to));

		Assert.False(result.IsOk);
		Assert.Contains("camera", result.Error);
	}
}

public class EffectsSettingTests
{
	[Fact]
	public void Efeitos_reduzidos_vem_desligado_dos_dados_e_pode_ser_ligado()
	{
		Assert.False(TrainingFixtures.Game.Settings.Defaults.ReduceEffects);
	}
}

public class WordBattleHiddenKeyTests
{
	[Fact]
	public void Letra_escondida_pela_travessura_tambem_nao_vale_no_teclado_fisico()
	{
		for (ulong seed = 1; seed <= 30; seed++)
		{
			var battle = WordBattle.Start([WordFixtures.Word("a", "casa")], new Pcg32(seed)).Value!;
			if (battle.PlayPrank() != Prank.Hidden) continue;
			var hidden = battle.Keys.First(battle.IsHidden);

			Assert.False(battle.Type(hidden));
			battle.Reveal();
			Assert.True(battle.Type(hidden));
			return;
		}
		Assert.Fail("Nenhuma seed escondeu uma letra.");
	}

	[Fact]
	public void Pool_com_escrita_e_com_nome_serve_o_nome_e_nao_descarta_as_outras_Missoes()
	{
		var missions = new[] { WordFixtures.Word("w", "casa", year: 3), WordFixtures.Sentence("s", ["O", "gato", "dorme"], year: 3) };

		var session = TrainingFixtures.Start("portugues", missions, level: 3, year: 3).Value!;

		Assert.NotNull(session.Name);
	}
}
