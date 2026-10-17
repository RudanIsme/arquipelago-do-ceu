using System.Numerics;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class CreatureMoverTests
{
	static readonly MovementRules Rules = TrainingFixtures.Rules.Movement;

	static readonly Vector2 Home = new(10, 10);

	static CreatureMover Start(Func<Vector2, bool>? canStand = null, ulong seed = 1)
	{
		var rng = new Pcg32(seed);
		return new CreatureMover(Rules, Home, canStand ?? (_ => true), () => rng.NextInt(10_000) / 10_000.0);
	}

	// Avança `seconds` em passos de 0,1 s com a criança parada em `player`.
	static void Run(CreatureMover mover, float seconds, Vector2 player, bool engaged = false, bool calm = false)
	{
		for (var t = 0f; t < seconds; t += 0.1f) mover.Tick(0.1f, player, engaged, calm);
	}

	[Fact]
	public void Os_dados_so_aceitam_criaturas_mais_lentas_que_a_crianca()
	{
		Assert.True(Rules.PatrolSpeed <= Rules.ApproachSpeed);
		Assert.True(Rules.ApproachSpeed < ChildSpeed.Walk);

		var fast = TestJson.Rules.Replace("\"approachSpeed\": 0.8", $"\"approachSpeed\": {ChildSpeed.Walk.ToString(System.Globalization.CultureInfo.InvariantCulture)}", StringComparison.Ordinal);

		Assert.False(GameRules.Parse(fast).IsOk);
	}

	[Theory]
	[InlineData("\"patrolSpeed\": 0.35", "\"patrolSpeed\": 0.9", "combat.movement")]
	[InlineData("\"stopDistance\": 2.2", "\"stopDistance\": 7", "combat.movement.stopDistance")]
	[InlineData("\"giveUpSeconds\": 10", "\"giveUpSeconds\": 0.5", "combat.movement.giveUpSeconds")]
	[InlineData("\"approachRadius\": 6", "\"approachRadius\": 99", "combat.movement.approachRadius")]
	[InlineData(", \"cooldownSeconds\": 8 }", " }", "combat.movement.cooldownSeconds")]
	public void Movimento_invalido_vira_resultado_de_falha_na_carga(string good, string bad, string field)
	{
		var parsed = GameRules.Parse(TestJson.Rules.Replace(good, bad, StringComparison.Ordinal));

		Assert.False(parsed.IsOk);
		Assert.Contains(field, parsed.Error);
	}

	[Fact]
	public void Patrulha_fica_perto_de_casa_e_anda_devagar()
	{
		var mover = Start();
		var farthest = 0f;
		var walked = 0f;
		var last = mover.Position;

		for (var t = 0f; t < 120f; t += 0.1f)
		{
			mover.Tick(0.1f, new Vector2(100, 100), engaged: false, calm: false);
			farthest = Math.Max(farthest, Vector2.Distance(mover.Position, Home));
			walked += Vector2.Distance(last, mover.Position);
			last = mover.Position;
		}

		Assert.True(farthest <= Rules.PatrolRadius + 0.2f, $"se afastou {farthest}");
		Assert.True(walked > 1f, "a patrulha precisa andar");
		Assert.True(walked / 120f < Rules.PatrolSpeed + 0.01f);
		Assert.Equal(CreatureMood.Patrolling, mover.Mood);
	}

	[Fact]
	public void Aproxima_devagar_quando_a_crianca_chega_perto_e_para_a_uma_distancia()
	{
		var mover = Start();
		var player = Home + new Vector2(Rules.ApproachRadius - 0.5f, 0);

		Run(mover, 6f, player);

		Assert.Equal(CreatureMood.Approaching, mover.Mood);
		Assert.True(Vector2.Distance(mover.Position, player) >= Rules.StopDistance - Rules.ApproachSpeed * 0.1f);
		Assert.True(Vector2.Distance(mover.Position, Home) > 0.5f);
	}

	[Fact]
	public void Nunca_anda_mais_depressa_que_a_crianca_nem_o_limite_dos_dados()
	{
		var mover = Start();
		var player = Home + new Vector2(4, 0);
		var last = mover.Position;

		for (var t = 0f; t < 20f; t += 0.1f)
		{
			mover.Tick(0.1f, player, engaged: false, calm: false);
			Assert.True(Vector2.Distance(last, mover.Position) / 0.1f <= Rules.ApproachSpeed + 0.001f);
			Assert.True(Vector2.Distance(last, mover.Position) / 0.1f < ChildSpeed.Walk);
			last = mover.Position;
		}
	}

	[Fact]
	public void Desiste_depois_do_tempo_volta_para_casa_e_nao_persegue_para_sempre()
	{
		var mover = Start();
		var player = Home + new Vector2(4, 0);

		Run(mover, Rules.GiveUpSeconds + 1f, player);
		Assert.Equal(CreatureMood.Returning, mover.Mood);

		// Durante a espera a criança continua perto e a criatura segue sem se aproximar.
		Run(mover, Rules.CooldownSeconds - 2f, player);
		Assert.True(Vector2.Distance(mover.Position, Home) <= Rules.PatrolRadius + 0.2f);
		Assert.NotEqual(CreatureMood.Approaching, mover.Mood);
	}

	[Fact]
	public void Depois_da_espera_a_criatura_pode_se_aproximar_de_novo()
	{
		var mover = Start();
		var player = Home + new Vector2(4, 0);
		Run(mover, Rules.GiveUpSeconds + 1f, player);

		Run(mover, 40f, new Vector2(100, 100));
		Run(mover, 3f, player);

		Assert.Equal(CreatureMood.Approaching, mover.Mood);
	}

	[Fact]
	public void A_crianca_sai_sem_punicao_e_a_criatura_volta_para_casa()
	{
		var mover = Start();
		Run(mover, 6f, Home + new Vector2(4, 0));
		Assert.Equal(CreatureMood.Approaching, mover.Mood);

		Run(mover, 60f, Home + new Vector2(40, 0));

		Assert.True(Vector2.Distance(mover.Position, Home) <= Rules.PatrolRadius + 0.2f);
	}

	[Fact]
	public void No_Modo_calmo_a_criatura_nao_se_aproxima()
	{
		var mover = Start();

		Run(mover, 30f, Home + new Vector2(2, 0), calm: true);

		Assert.NotEqual(CreatureMood.Approaching, mover.Mood);
		Assert.True(Vector2.Distance(mover.Position, Home) <= Rules.PatrolRadius + 0.2f);
	}

	[Fact]
	public void Em_luta_a_criatura_chega_perto_e_fica_sem_desistir()
	{
		var mover = Start();
		var player = Home + new Vector2(4.5f, 0);

		Run(mover, Rules.GiveUpSeconds * 3, player, engaged: true);

		Assert.Equal(CreatureMood.Approaching, mover.Mood);
		Assert.InRange(Vector2.Distance(mover.Position, player), Rules.StopDistance - 0.2f, Rules.StopDistance + 0.2f);
	}

	[Fact]
	public void Contorna_um_obstaculo_no_caminho_ate_a_crianca()
	{
		// Uma pedra entre a criatura e a criança; o resto do chão é livre.
		bool Free(Vector2 p) => !(p.X is > 11.8f and < 12.4f && p.Y is > 9.4f and < 10.6f);
		var mover = Start(Free);
		var player = Home + new Vector2(4.4f, 0);

		Run(mover, Rules.GiveUpSeconds - 0.5f, player);

		Assert.True(mover.Position.X > 12.4f || Math.Abs(mover.Position.Y - Home.Y) > 0.6f, "passou ao redor da pedra");
		Assert.True(Free(mover.Position));
	}

	[Fact]
	public void Atras_de_um_desnivel_ela_para_e_nunca_fica_presa()
	{
		// Um muro largo bloqueia tudo entre a criatura e a criança.
		bool Free(Vector2 p) => p.X is < 11.5f or > 12.5f;
		var mover = Start(Free);
		var player = Home + new Vector2(4.4f, 0);

		Run(mover, Rules.GiveUpSeconds + 3f, player);
		var stopped = mover.Position;
		Assert.True(stopped.X <= 11.5f);

		Run(mover, 20f, player);

		Assert.NotEqual(CreatureMood.Approaching, mover.Mood);
		Assert.True(Vector2.Distance(mover.Position, Home) <= Rules.PatrolRadius + 0.2f);
	}

	[Fact]
	public void Cercada_por_obstaculos_volta_para_casa_em_vez_de_ficar_presa()
	{
		var blocked = false;
		var mover = Start(p => !blocked || Vector2.Distance(p, Home) < 0.05f);
		Run(mover, 4f, Home + new Vector2(4, 0));
		blocked = true;

		Run(mover, 30f, new Vector2(100, 100));

		Assert.True(Vector2.Distance(mover.Position, Home) <= 0.2f);
	}

	[Fact]
	public void A_mesma_semente_gera_o_mesmo_passeio()
	{
		Vector2[] Walk(ulong seed)
		{
			var mover = Start(seed: seed);
			var path = new List<Vector2>();
			for (var i = 0; i < 300; i++)
			{
				mover.Tick(0.1f, new Vector2(100, 100), false, false);
				path.Add(mover.Position);
			}
			return [.. path];
		}

		Assert.Equal(Walk(5), Walk(5));
		Assert.NotEqual(Walk(5), Walk(6));
	}
}
