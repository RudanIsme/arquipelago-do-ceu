using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class MissionHistoryTests
{
	static IReadOnlyList<Mission> Pool(int count) => Enumerable.Range(1, count).Select(i => TrainingFixtures.MathMission($"m{i}", i + 5)).ToList();

	[Fact]
	public void Prefere_as_Missoes_que_ainda_nao_apareceram()
	{
		var history = new MissionHistory();
		history.Add("m1");
		history.Add("m2");

		var fresh = history.Fresh(Pool(4));

		Assert.Equal(new[] { "m3", "m4" }, fresh.Select(m => m.Id).ToArray());
	}

	[Fact]
	public void Quando_todas_ja_apareceram_recomeca_evitando_as_ultimas_vistas()
	{
		var history = new MissionHistory(avoidRecent: 2);
		foreach (var id in new[] { "m1", "m2", "m3", "m4" }) history.Add(id);

		var fresh = history.Fresh(Pool(4));

		Assert.Equal(new[] { "m1", "m2" }, fresh.Select(m => m.Id).ToArray());
	}

	[Fact]
	public void Com_uma_unica_Missao_ela_volta_mesmo_assim()
	{
		var history = new MissionHistory();
		history.Add("m1");

		Assert.Equal(new[] { "m1" }, history.Fresh(Pool(1)).Select(m => m.Id).ToArray());
	}

	[Fact]
	public void Uma_luta_nunca_repete_o_problema_enquanto_houver_outro_novo()
	{
		var pool = Pool(6);
		var history = new MissionHistory();
		var rules = TrainingFixtures.Rules;
		var fight = CombatSession.Start(rules, new Energy(5), 1, new TrainingSetup(TrainingSetup.Mathematics, 1, 1), pool, new Pcg32(7), history).Value!;

		var seen = new List<string> { fight.Mission.Id };
		for (var i = 0; i < 5; i++)
		{
			fight.NextChallenge();
			seen.Add(fight.Mission.Id);
		}

		Assert.Equal(6, seen.Distinct().Count());
	}

	[Fact]
	public void Duas_lutas_seguidas_da_mesma_Expedicao_nao_repetem_o_problema()
	{
		var pool = Pool(4);
		var history = new MissionHistory();
		var setup = new TrainingSetup(TrainingSetup.Mathematics, 1, 1);

		var ids = new List<string>();
		for (var i = 0; i < 4; i++)
			ids.Add(CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, setup, pool, new Pcg32((ulong)i + 1), history).Value!.Mission.Id);

		Assert.Equal(4, ids.Distinct().Count());
	}

	[Fact]
	public void Depois_de_esgotar_o_conjunto_os_ultimos_vistos_ficam_de_fora()
	{
		var pool = Pool(5);
		var history = new MissionHistory(avoidRecent: 2);
		var setup = new TrainingSetup(TrainingSetup.Mathematics, 1, 1);

		var ids = new List<string>();
		for (var i = 0; i < 10; i++)
			ids.Add(CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, setup, pool, new Pcg32((ulong)i + 1), history).Value!.Mission.Id);

		for (var i = 2; i < ids.Count; i++)
			Assert.DoesNotContain(ids[i], ids.Skip(i - 2).Take(2));
	}

	[Fact]
	public void Sem_historico_o_treino_segue_como_sempre()
	{
		var fight = CombatSession.Start(TrainingFixtures.Rules, new Energy(5), 1, new TrainingSetup(TrainingSetup.Mathematics, 1, 1), Pool(3), new Pcg32(3)).Value!;

		Assert.NotNull(fight.Mission);
	}
}
