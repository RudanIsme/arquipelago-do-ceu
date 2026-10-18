using System.Numerics;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class RhythmTests
{
	static readonly CombatRules Rules = TrainingFixtures.Rules;

	static (AttackRhythm Rhythm, CombatTime Time) Start()
	{
		var time = new CombatTime(Rules);
		return (new AttackRhythm(Rules, time), time);
	}

	// Avança `seconds` de tempo real em passos pequenos e devolve o primeiro evento que não é None.
	static BubbleEvent Run(AttackRhythm rhythm, float seconds)
	{
		for (var t = 0f; t < seconds; t += 0.1f)
			if (rhythm.Tick(0.1f, Vector2.Zero, new Vector2(3, 0), stopped: false) is var e and not BubbleEvent.None) return e;
		return BubbleEvent.None;
	}

	[Fact]
	public void O_intervalo_de_cada_tipo_de_criatura_vem_dos_dados()
	{
		Assert.Equal(8f, Rules.Rhythm.For("dummy").IntervalSeconds);
		Assert.Equal(7f, Rules.Rhythm.For("wild").IntervalSeconds);
		Assert.Equal(Rules.Rhythm.For("dummy"), Rules.Rhythm.For("desconhecida"));
	}

	[Fact]
	public void O_relogio_da_criatura_corre_em_tempo_normal_fora_da_montagem()
	{
		var (rhythm, _) = Start();

		Assert.Equal(BubbleEvent.None, Run(rhythm, 7f));
		Assert.Equal(BubbleEvent.Warned, Run(rhythm, 2f));
	}

	[Fact]
	public void Enquanto_a_crianca_monta_a_resposta_o_relogio_anda_em_camera_lenta()
	{
		var (rhythm, time) = Start();
		time.Begin();

		// Com a escala lenta o aviso que viria em 8 s leva bem mais: demorar não pune, o golpe só chega mais devagar.
		Assert.Equal(BubbleEvent.None, Run(rhythm, 20f));
		Assert.Equal(BubbleEvent.Warned, Run(rhythm, 20f));
	}

	[Fact]
	public void No_Modo_calmo_a_criatura_nao_lanca_e_o_relogio_recomeca_ao_sair()
	{
		var (rhythm, _) = Start();

		for (var t = 0f; t < 300f; t += 0.1f)
			Assert.Equal(BubbleEvent.None, rhythm.Tick(0.1f, Vector2.Zero, new Vector2(3, 0), stopped: true));

		Assert.Equal(BubbleEvent.Warned, Run(rhythm, 9f));
	}

	[Fact]
	public void O_jogador_so_tem_uma_recarga_curta_entre_uma_resposta_e_a_proxima()
	{
		var (rhythm, _) = Start();
		Assert.True(rhythm.PlayerReady);

		rhythm.PlayerAnswered();

		Assert.False(rhythm.PlayerReady);
		Run(rhythm, Rules.Rhythm.PlayerRechargeSeconds - 0.2f);
		Assert.False(rhythm.PlayerReady);
		Run(rhythm, 0.5f);
		Assert.True(rhythm.PlayerReady);
	}

	[Fact]
	public void Uma_luta_nova_comeca_sem_recarga_e_sem_bolha_no_ar()
	{
		var (rhythm, _) = Start();
		Run(rhythm, 9f);
		rhythm.PlayerAnswered();

		rhythm.Reset();

		Assert.True(rhythm.PlayerReady);
		Assert.Equal(BubblePhase.Waiting, rhythm.Bubble.Phase);
	}

	[Fact]
	public void A_recarga_corre_em_tempo_real_mesmo_na_montagem()
	{
		var (rhythm, time) = Start();
		time.Begin();
		rhythm.PlayerAnswered();

		Run(rhythm, Rules.Rhythm.PlayerRechargeSeconds + 0.2f);

		Assert.True(rhythm.PlayerReady);
	}

	[Fact]
	public void Trocar_de_criatura_troca_o_intervalo_e_recomeca_o_relogio()
	{
		var (rhythm, _) = Start();
		Run(rhythm, 5f);

		rhythm.UseCreature("wild");

		Assert.Equal(BubbleEvent.None, Run(rhythm, 6f));
		Assert.Equal(BubbleEvent.Warned, Run(rhythm, 1.5f));
	}

	[Fact]
	public void Parado_a_criatura_nao_ataca_e_a_bolha_no_ar_some()
	{
		var (rhythm, _) = Start();
		Run(rhythm, 9f);

		var parado = rhythm.Tick(0.1f, Vector2.Zero, Vector2.Zero, stopped: true);

		Assert.Equal(BubbleEvent.None, parado);
		Assert.Equal(BubblePhase.Waiting, rhythm.Bubble.Phase);
	}

	[Theory]
	[InlineData("\"rechargeSeconds\": 0.8", "\"rechargeSeconds\": 9", "combat.rhythm.player.rechargeSeconds")]
	[InlineData("\"intervalSeconds\": 7, \"warningSeconds\": 1.6 }", "\"intervalSeconds\": 1, \"warningSeconds\": 1.6 }", "combat.rhythm.creatures")]
	[InlineData("\"intervalSeconds\": 7, \"warningSeconds\": 1.6 }", "\"intervalSeconds\": 7, \"warningSeconds\": 9 }", "combat.rhythm.creatures")]
	[InlineData("\"kind\": \"wild\"", "\"kind\": \"dummy\"", "combat.rhythm.creatures")]
	[InlineData("\"kind\": \"dummy\"", "\"kind\": \"\"", "combat.rhythm.creatures")]
	public void Ritmo_invalido_vira_resultado_de_falha_na_carga(string good, string bad, string field)
	{
		var parsed = GameRules.Parse(TestJson.Rules.Replace(good, bad, StringComparison.Ordinal));

		Assert.False(parsed.IsOk);
		Assert.Contains(field, parsed.Error);
	}

	[Fact]
	public void O_ritmo_precisa_ter_o_Boneco_e_existir()
	{
		var withoutDummy = TestJson.Rules.Replace("{ \"kind\": \"dummy\", \"intervalSeconds\": 8, \"warningSeconds\": 1.6 },", "", StringComparison.Ordinal);
		var withoutRhythm = TestJson.Rules.Replace("\"rhythm\": { \"player\": { \"rechargeSeconds\": 0.8 }, \"creatures\": [ { \"kind\": \"dummy\", \"intervalSeconds\": 8, \"warningSeconds\": 1.6 }, { \"kind\": \"wild\", \"intervalSeconds\": 7, \"warningSeconds\": 1.6 } ] },", "", StringComparison.Ordinal);

		Assert.False(GameRules.Parse(withoutDummy).IsOk);
		Assert.False(GameRules.Parse(withoutRhythm).IsOk);
	}
}
