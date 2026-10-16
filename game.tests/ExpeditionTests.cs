using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// Os Andares vêm do arquivo de reserva, que tem formato conhecido e sempre válido:
//   Sem Marco:  0 entrada, 1 combate, 2 Fast travel, 3 Descanso, 4 Puzzle, 5 saída; ligações 0-1 1-2 0-3 1-4 4-5.
//   Com Marco:  o mesmo, com o Marco (5) entre o Puzzle e a saída (6, o Desafio épico no último Andar).
static class ExpeditionFixtures
{
	public static readonly DungeonRules Rules = GameRules.Parse(TestJson.Rules).Value!.Dungeon;

	public static readonly FallbackFloors Fallback =
		FallbackFloors.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "andares-reserva.json"))).Value!;

	// Dois Andares: o 1º comum e o 2º com Marco e épico.
	public static readonly LevelPlan Plan = new("matematica", 1, [new(1, false, false, false), new(2, false, true, true)]);

	public static Expedition Start(Energy? energy = null, Func<bool>? hasItem = null) =>
		Expedition.Start(new FloorProvider(Rules, Fallback, (_, _, _, _, p, _) => Fallback.For(p)), Plan, 1, 1, energy ?? new Energy(5), hasItem);

	// Vence a Sala e vai para a próxima.
	public static void ClearAndGo(Expedition e, int room)
	{
		if (e.NeedsClearing(e.Current) && !e.IsCleared(e.CurrentRoom)) Assert.True(e.ClearCurrent().IsOk);
		Assert.Equal(Passage.Open, e.MoveTo(room));
	}
}

public class ExpeditionTests
{
	[Fact]
	public void Comeca_na_entrada_do_Andar_1_com_a_Energia_cheia()
	{
		var e = ExpeditionFixtures.Start();

		Assert.Equal(1, e.Floor.Number);
		Assert.Equal(RoomKind.Entrance, e.Current.Kind);
		Assert.Equal(5, e.Energy.Current);
		Assert.False(e.Defeated);
		Assert.False(e.Finished);
	}

	[Fact]
	public void Nao_da_para_ir_a_uma_Sala_que_nao_esta_ligada()
	{
		var e = ExpeditionFixtures.Start();

		Assert.Equal(Passage.NotLinked, e.MoveTo(5));
		Assert.Equal(0, e.CurrentRoom);
	}

	[Fact]
	public void Sala_de_combate_precisa_ser_vencida_para_seguir_para_uma_Sala_nova()
	{
		var e = ExpeditionFixtures.Start();
		Assert.Equal(Passage.Open, e.MoveTo(1));

		Assert.Equal(Passage.MustClearRoom, e.CanMoveTo(2));
		Assert.Equal(Passage.MustClearRoom, e.MoveTo(2));
		Assert.Equal(1, e.CurrentRoom);

		Assert.True(e.ClearCurrent().IsOk);
		Assert.Equal(Passage.Open, e.MoveTo(2));
	}

	[Fact]
	public void Voltar_para_uma_Sala_ja_visitada_e_sempre_permitido()
	{
		var e = ExpeditionFixtures.Start();
		e.MoveTo(1);

		Assert.Equal(Passage.Open, e.MoveTo(0));
		Assert.Equal(0, e.CurrentRoom);
	}

	[Fact]
	public void Entrada_Descanso_e_Fast_travel_nao_precisam_ser_vencidos()
	{
		var e = ExpeditionFixtures.Start();

		Assert.Equal(Passage.Open, e.MoveTo(3));
		Assert.False(e.NeedsClearing(e.Current));
		Assert.False(e.ClearCurrent().IsOk);
	}

	[Fact]
	public void So_da_para_subir_de_Andar_pela_escada()
	{
		var e = ExpeditionFixtures.Start();
		Assert.False(e.Ascend());

		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);

		Assert.Equal(RoomKind.Exit, e.Current.Kind);
		Assert.True(e.Ascend());
		Assert.Equal(2, e.Floor.Number);
		Assert.Equal(RoomKind.Entrance, e.Current.Kind);
	}

	[Fact]
	public void A_Energia_zerada_marca_a_Expedicao_como_perdida()
	{
		var energy = new Energy(2);
		var e = ExpeditionFixtures.Start(energy);

		energy.Lose(2);

		Assert.True(e.Defeated);
	}

	[Fact]
	public void Sem_o_Item_o_Marco_nao_deixa_passar_e_com_o_Item_abre_ao_chegar()
	{
		var hasItem = false;
		var e = ExpeditionFixtures.Start(hasItem: () => hasItem);
		GoToSecondFloorMarco(e);

		Assert.Equal(RoomKind.Marco, e.Current.Kind);
		Assert.Equal(Passage.NeedsItem, e.MoveTo(6));

		hasItem = true;
		Assert.Equal(Passage.Open, e.MoveTo(6));
		Assert.Equal(RoomKind.Epic, e.Current.Kind);
	}

	[Fact]
	public void Vencer_o_Desafio_epico_termina_a_Expedicao()
	{
		var e = ExpeditionFixtures.Start(hasItem: () => true);
		GoToSecondFloorMarco(e);
		Assert.Equal(Passage.Open, e.MoveTo(6));
		Assert.False(e.Finished);

		Assert.True(e.ClearCurrent().IsOk);

		Assert.True(e.Finished);
		Assert.False(e.Ascend());
	}

	[Fact]
	public void O_Descanso_devolve_a_Energia_dos_dados_uma_unica_vez()
	{
		var energy = new Energy(5);
		var e = ExpeditionFixtures.Start(energy);
		energy.Lose(3);
		e.MoveTo(3);

		var first = e.UseRest(2);
		var second = e.UseRest(2);

		Assert.Equal(2, first.Value);
		Assert.Equal(4, energy.Current);
		Assert.False(second.IsOk);
		Assert.Equal(4, energy.Current);
	}

	[Fact]
	public void O_Descanso_nunca_passa_do_maximo()
	{
		var energy = new Energy(5);
		var e = ExpeditionFixtures.Start(energy);
		energy.Lose(1);
		e.MoveTo(3);

		Assert.Equal(1, e.UseRest(2).Value);
		Assert.Equal(5, energy.Current);
	}

	[Fact]
	public void Com_a_Energia_cheia_o_Descanso_nao_e_gasto()
	{
		var energy = new Energy(5);
		var e = ExpeditionFixtures.Start(energy);
		e.MoveTo(3);

		Assert.False(e.UseRest(2).IsOk);
		Assert.False(e.Rested(3));

		energy.Lose(2);
		Assert.Equal(2, e.UseRest(2).Value);
	}

	[Fact]
	public void So_se_descansa_na_Sala_de_Descanso()
	{
		var energy = new Energy(5);
		var e = ExpeditionFixtures.Start(energy);
		energy.Lose(3);

		Assert.False(e.UseRest(2).IsOk);
		Assert.Equal(2, energy.Current);
	}

	[Fact]
	public void Em_outro_Andar_o_Descanso_volta_a_valer()
	{
		var energy = new Energy(5);
		var e = ExpeditionFixtures.Start(energy);
		energy.Lose(4);
		e.MoveTo(3);
		Assert.True(e.UseRest(2).IsOk);
		e.MoveTo(0);
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);
		Assert.True(e.Ascend());

		Assert.Equal(Passage.Open, e.MoveTo(3));
		Assert.True(e.UseRest(2).IsOk);
	}

	static void GoToSecondFloorMarco(Expedition e)
	{
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);
		Assert.True(e.Ascend());
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		Assert.True(e.ClearCurrent().IsOk);
		Assert.Equal(Passage.Open, e.MoveTo(5));
	}
}

public class DungeonCombatTests
{
	static ArquipelagoDoCeu.Missions.Mission Math(int year, int difficulty) => TrainingFixtures.MathMission($"m{year}{difficulty}", 9, year, difficulty);

	[Fact]
	public void A_dificuldade_acompanha_o_Andar()
	{
		var missions = new[] { Math(1, 1), Math(1, 2), Math(1, 3) };

		Assert.Equal(1, DungeonCombat.Setup(missions, TrainingSetup.Mathematics, 1, 1).Value!.Difficulty);
		Assert.Equal(3, DungeonCombat.Setup(missions, TrainingSetup.Mathematics, 1, 3).Value!.Difficulty);
	}

	[Fact]
	public void Sem_Missoes_da_dificuldade_do_Andar_usa_a_mais_perto_sem_passar_do_maximo()
	{
		var missions = new[] { Math(1, 1), Math(1, 2) };

		Assert.Equal(2, DungeonCombat.Setup(missions, TrainingSetup.Mathematics, 1, 4).Value!.Difficulty);
		Assert.Equal(2, DungeonCombat.Setup(missions, TrainingSetup.Mathematics, 1, 9).Value!.Difficulty);
	}

	[Fact]
	public void Nivel_sem_Missoes_do_proprio_ano_usa_as_do_ano_anterior()
	{
		var missions = new[] { Math(1, 1), Math(2, 1) };

		Assert.Equal(2, DungeonCombat.Setup(missions, TrainingSetup.Mathematics, 3, 1).Value!.Year);
	}

	[Fact]
	public void Sem_nenhuma_Missao_da_disciplina_e_uma_falha_e_nao_uma_excecao()
	{
		var result = DungeonCombat.Setup([Math(1, 1)], TrainingSetup.Portuguese, 1, 1);

		Assert.False(result.IsOk);
	}

	[Fact]
	public void A_seed_do_mundo_vem_do_Perfil_e_e_estavel()
	{
		Assert.Equal(Seed.ForProfile("abc"), Seed.ForProfile("abc"));
		Assert.NotEqual(Seed.ForProfile("abc"), Seed.ForProfile("abd"));
	}
}
