using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class CombatContextTests
{
	static readonly TrainingSetup Math1 = new(TrainingSetup.Mathematics, 1, 1);
	static readonly IReadOnlyList<ArquipelagoDoCeu.Missions.Mission> Missions =
		[TrainingFixtures.MathMission("a", 9), TrainingFixtures.MathMission("b", 8)];

	static CombatSession Fight(Energy energy) =>
		CombatSession.Start(TrainingFixtures.Rules, energy, 1, Math1, Missions, new Pcg32(1)).Value!;

	[Fact]
	public void A_Energia_da_Expedicao_segue_de_uma_luta_para_a_outra()
	{
		var energy = new Energy(5);

		Fight(energy).Mistake();
		var next = Fight(energy);

		Assert.Same(energy, next.Energy);
		Assert.Equal(4, next.Energy.Current);
	}

	[Fact]
	public void Energia_zerada_numa_luta_encerra_a_proxima_tambem()
	{
		var energy = new Energy(1);

		var outcome = Fight(energy).Mistake();

		Assert.True(outcome.Empty);
		Assert.True(Fight(energy).IsOver);
	}

	[Fact]
	public void Sem_Energia_de_fora_o_treino_continua_comecando_cheio()
	{
		var session = TrainingFixtures.Math(Missions.ToArray());

		Assert.Equal(TrainingFixtures.Game.MaxEnergy, session.Energy.Current);
	}
}
