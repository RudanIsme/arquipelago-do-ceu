using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// Seed fixa: cada pedido devolve a mesma sequência, então um teste repete o mesmo resultado.
sealed class FixedRandomSource(ulong seed) : IRandomSource
{
	int requests;

	public Pcg32 NewStream() => new(seed + (ulong)requests++);
}

public class RandomSourceTests
{
	static readonly IReadOnlyList<Mission> Missions =
		MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!.Load(MissionFiles.All()).Missions;

	// Joga todas as Salas do Andar 1 ao 3 de uma Expedição e devolve as Missões sorteadas, na ordem.
	static List<string> MissionsOfExpedition(IRandomSource random)
	{
		var plan = LevelPlan.Create(ExpeditionFixtures.Rules, 7, TrainingSetup.Mathematics, 1, 1);
		var energy = new Energy(5);
		var ids = new List<string>();
		foreach (var floor in plan.Floors)
		{
			var setup = DungeonCombat.Setup(Missions, TrainingSetup.Mathematics, 1, floor.Number).Value!;
			var fight = CombatSession.Start(TrainingFixtures.Rules, energy, 1, setup, Missions, random.NewStream()).Value!;
			ids.Add(fight.Mission.Id);
			fight.NextChallenge();
			ids.Add(fight.Mission.Id);
		}
		return ids;
	}

	[Fact]
	public void Uma_Expedicao_com_fonte_de_numeros_fixa_repete_as_mesmas_Missoes()
	{
		Assert.Equal(MissionsOfExpedition(new FixedRandomSource(42)), MissionsOfExpedition(new FixedRandomSource(42)));
	}

	[Fact]
	public void Outra_seed_sorteia_outras_Missoes()
	{
		Assert.NotEqual(MissionsOfExpedition(new FixedRandomSource(1)), MissionsOfExpedition(new FixedRandomSource(99999)));
	}

	[Fact]
	public void Os_Desafios_do_Atalho_e_do_mestre_tambem_repetem_com_a_mesma_seed()
	{
		var first = DungeonCombat.ShortcutSetups(Missions, TrainingSetup.Mathematics, 1, 3, new FixedRandomSource(5).NewStream()).Value!;
		var again = DungeonCombat.ShortcutSetups(Missions, TrainingSetup.Mathematics, 1, 3, new FixedRandomSource(5).NewStream()).Value!;
		var master = DungeonCombat.MasterSetups(Missions, 1, 4, new FixedRandomSource(5).NewStream()).Value!;
		var masterAgain = DungeonCombat.MasterSetups(Missions, 1, 4, new FixedRandomSource(5).NewStream()).Value!;

		Assert.Equal(first, again);
		Assert.Equal(master, masterAgain);
	}
}
