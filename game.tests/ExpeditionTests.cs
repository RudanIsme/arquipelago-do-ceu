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

	public static Expedition Start(Energy? energy = null, Func<bool>? hasItem = null, int startFloor = 1, bool atFastTravel = false, bool atEpic = false) =>
		Expedition.Start(new FloorProvider(Rules, Fallback, (_, _, _, _, p, _) => Fallback.For(p)), Plan, 1, 1, energy ?? new Energy(5), hasItem, startFloor, atFastTravel, atEpic);

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
	public void Voltar_atras_do_Marco_sem_o_Item_nao_o_deixa_aberto()
	{
		var e = ExpeditionFixtures.Start(hasItem: () => false);
		GoToSecondFloorMarco(e);
		Assert.Equal(Passage.NeedsItem, e.MoveTo(6));

		Assert.Equal(Passage.Open, e.MoveTo(4));
		Assert.Equal(Passage.Open, e.MoveTo(5));

		Assert.Equal(Passage.NeedsItem, e.MoveTo(6));
		Assert.False(e.IsCleared(5));
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

	[Fact]
	public void Pode_recomecar_na_entrada_de_um_Andar_mais_fundo()
	{
		var e = ExpeditionFixtures.Start(startFloor: 2);

		Assert.Equal(2, e.Floor.Number);
		Assert.Equal(RoomKind.Entrance, e.Current.Kind);
	}

	[Fact]
	public void Pode_recomecar_no_Fast_travel_com_o_caminho_ate_ele_ja_vencido()
	{
		var e = ExpeditionFixtures.Start(atFastTravel: true);

		Assert.Equal(RoomKind.FastTravel, e.Current.Kind);
		Assert.True(e.IsCleared(1), "o combate no caminho já foi vencido");
		Assert.Equal(Passage.Open, e.MoveTo(1));
		Assert.Equal(Passage.Open, e.MoveTo(0));
	}

	[Fact]
	public void O_Atalho_comeca_direto_no_Desafio_epico_do_ultimo_Andar()
	{
		var e = ExpeditionFixtures.Start(atEpic: true);

		Assert.Equal(2, e.Floor.Number);
		Assert.Equal(RoomKind.Epic, e.Current.Kind);
		Assert.False(e.Finished);
		Assert.True(e.IsCleared(5), "o Marco do caminho fica para trás");

		Assert.True(e.ClearCurrent().IsOk);
		Assert.True(e.Finished);
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
	public void O_Atalho_sorteia_Desafios_de_dificuldades_diferentes_do_Nivel_inteiro()
	{
		var missions = new[] { Math(1, 1), Math(1, 2), Math(1, 3), Math(1, 4) };

		var setups = DungeonCombat.ShortcutSetups(missions, TrainingSetup.Mathematics, 1, 3, new ArquipelagoDoCeu.Generation.Pcg32(7)).Value!;

		Assert.Equal(3, setups.Count);
		Assert.Equal(3, setups.Select(s => s.Difficulty).Distinct().Count());
		Assert.All(setups, s => Assert.Equal(1, s.Year));
	}

	[Fact]
	public void O_Atalho_repete_dificuldades_quando_nao_ha_o_bastante_e_e_deterministico()
	{
		var missions = new[] { Math(1, 1), Math(1, 2) };

		var a = DungeonCombat.ShortcutSetups(missions, TrainingSetup.Mathematics, 1, 3, new ArquipelagoDoCeu.Generation.Pcg32(7)).Value!;
		var b = DungeonCombat.ShortcutSetups(missions, TrainingSetup.Mathematics, 1, 3, new ArquipelagoDoCeu.Generation.Pcg32(7)).Value!;

		Assert.Equal(3, a.Count);
		Assert.Equal(a, b);
	}

	[Fact]
	public void O_Atalho_sem_Missoes_e_uma_falha()
	{
		Assert.False(DungeonCombat.ShortcutSetups([], TrainingSetup.Mathematics, 1, 3, new ArquipelagoDoCeu.Generation.Pcg32(1)).IsOk);
	}

	[Fact]
	public void O_Desafio_mestre_mistura_as_duas_disciplinas_em_partes_iguais_e_so_com_o_Nivel()
	{
		var missions = new[] { Math(1, 1), Math(1, 2), TrainingFixtures.NameMission("n1", ["pa", "to"], 1, 1), TrainingFixtures.NameMission("n2", ["pa", "to"], 1, 2), Math(2, 1) };

		var setups = DungeonCombat.MasterSetups(missions, 1, 4, new ArquipelagoDoCeu.Generation.Pcg32(3)).Value!;

		Assert.Equal(4, setups.Count);
		Assert.Equal(2, setups.Count(s => s.Discipline == TrainingSetup.Mathematics));
		Assert.Equal(2, setups.Count(s => s.Discipline == TrainingSetup.Portuguese));
		Assert.All(setups, s => Assert.Equal(1, s.Year));
	}

	[Fact]
	public void O_Desafio_mestre_alterna_as_disciplinas()
	{
		var missions = new[] { Math(1, 1), TrainingFixtures.NameMission("n1", ["pa", "to"], 1, 1) };

		var setups = DungeonCombat.MasterSetups(missions, 1, 4, new ArquipelagoDoCeu.Generation.Pcg32(3)).Value!;

		Assert.NotEqual(setups[0].Discipline, setups[1].Discipline);
		Assert.Equal(setups[0].Discipline, setups[2].Discipline);
	}

	[Fact]
	public void O_Desafio_mestre_sem_Missoes_de_uma_disciplina_e_uma_falha()
	{
		Assert.False(DungeonCombat.MasterSetups([Math(1, 1)], 1, 4, new ArquipelagoDoCeu.Generation.Pcg32(3)).IsOk);
	}

	[Fact]
	public void A_seed_do_mundo_vem_do_Perfil_e_e_estavel()
	{
		Assert.Equal(Seed.ForProfile("abc"), Seed.ForProfile("abc"));
		Assert.NotEqual(Seed.ForProfile("abc"), Seed.ForProfile("abd"));
	}
}

public class ResumeTests
{
	[Fact]
	public void Recomecar_no_Fast_travel_nao_da_a_Sala_do_Item_por_vencida()
	{
		var provider = new FloorProvider(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback);
		for (ulong seed = 0; seed < 600; seed++)
		{
			var plan = LevelPlan.Create(ExpeditionFixtures.Rules, seed, "matematica", 1, 1);
			foreach (var floor in plan.Floors)
			{
				var e = Expedition.Start(provider, plan, seed, 1, new Energy(5), null, floor.Number, atFastTravel: true);

				Assert.All(e.Visited.Where(r => e.Floor.Rooms[r].HoldsItem), r => Assert.False(e.IsCleared(r), $"seed {seed} andar {floor.Number}"));
			}
		}
	}

	[Fact]
	public void Recomecar_no_Fast_travel_funciona_em_milhares_de_seeds_e_Andares()
	{
		for (ulong seed = 0; seed < 1500; seed++)
		{
			var plan = LevelPlan.Create(ExpeditionFixtures.Rules, seed, "matematica", 1, 1);
			var provider = new FloorProvider(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback);
			for (var floor = 1; floor <= plan.Floors.Count; floor++)
			{
				var e = Expedition.Start(provider, plan, seed, 1, new Energy(5), null, floor, atFastTravel: true);

				Assert.Equal(RoomKind.FastTravel, e.Current.Kind);
				Assert.Equal(floor, e.Floor.Number);
				Assert.Contains(e.Floor.Entrance, e.Visited);
				Assert.All(e.Visited.Where(r => e.NeedsClearing(e.Floor.Rooms[r]) && !e.Floor.Rooms[r].HoldsItem), r => Assert.True(e.IsCleared(r)));
			}
		}
	}
}

// O ADR 0002 em forma de teste: um jogador que só sabe vencer Salas e andar nunca fica preso entre as duas torres.
public class TradeItemTests
{
	// Anda e vence, nunca passando pelo Marco, até chegar a uma Sala que atende ao pedido. Devolve se chegou.
	static bool Reach(Expedition e, Func<Room, bool> goal, Action<Room> onCleared)
	{
		for (var guard = 0; guard < 200; guard++)
		{
			if (goal(e.Current)) return true;
			var path = Path(e.Floor, e.CurrentRoom, goal);
			if (path.Count < 2)
			{
				// A Sala não está neste Andar: vai até a escada e sobe.
				if (e.CanAscend) { e.Ascend(); continue; }
				path = Path(e.Floor, e.CurrentRoom, r => r.Id == e.Floor.Exit);
				if (path.Count < 2) return false;
			}
			if (e.NeedsClearing(e.Current) && !e.IsCleared(e.CurrentRoom) && e.Current.Kind != RoomKind.Marco)
				onCleared(e.ClearCurrent().Value!);
			if (e.MoveTo(path[1]) != Passage.Open) return false;
		}
		return false;
	}

	static List<int> Path(Floor floor, int from, Func<Room, bool> goal)
	{
		var previous = new Dictionary<int, int> { [from] = -1 };
		var queue = new Queue<int>([from]);
		int? found = null;
		while (queue.Count > 0 && found is null)
		{
			var room = queue.Dequeue();
			if (goal(floor.Rooms[room])) { found = room; break; }
			foreach (var next in floor.Neighbors(room).Where(n => !previous.ContainsKey(n))) { previous[next] = room; queue.Enqueue(next); }
		}
		var path = new List<int>();
		if (found is null) return path;
		for (var room = found.Value; room != -1; room = previous[room]) path.Insert(0, room);
		return path;
	}

	[Fact]
	public void Em_milhares_de_seeds_as_duas_torres_nunca_travam_uma_a_outra()
	{
		var provider = new FloorProvider(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback);
		for (ulong seed = 0; seed < 1000; seed++)
		{
			var level = (int)(seed % 3) + 1;
			var mathItem = false;
			var lettersItem = false;
			var math = Expedition.Start(provider, LevelPlan.Create(ExpeditionFixtures.Rules, seed, "matematica", level, 1), seed, 1, new Energy(5), () => lettersItem);
			var letters = Expedition.Start(provider, LevelPlan.Create(ExpeditionFixtures.Rules, seed, "portugues", level, 1), seed, 1, new Energy(5), () => mathItem);
			var where = $"seed {seed}";

			// Cada torre dá o próprio Item antes do próprio Marco, sem precisar do Item da outra.
			Assert.True(Reach(math, r => r.HoldsItem, r => { }), where);
			Assert.True(Reach(letters, r => r.HoldsItem, r => { }), where);

			// O Item é entregue ao vencer a Sala; com os dois, as torres chegam ao fim.
			math.ClearCurrent();
			letters.ClearCurrent();
			mathItem = lettersItem = true;
			Assert.True(Reach(math, r => r.Kind == RoomKind.Epic, r => { }), where);
			Assert.True(Reach(letters, r => r.Kind == RoomKind.Epic, r => { }), where);
		}
	}

	[Fact]
	public void Sem_o_Item_da_outra_torre_o_Marco_barra_e_com_ele_abre()
	{
		var provider = new FloorProvider(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback);
		var plan = LevelPlan.Create(ExpeditionFixtures.Rules, 5, "matematica", 1, 1);
		var otherItem = false;
		var e = Expedition.Start(provider, plan, 5, 1, new Energy(5), () => otherItem);

		Assert.True(Reach(e, r => r.Kind == RoomKind.Marco, r => { }));
		var beyond = e.Floor.Neighbors(e.CurrentRoom).First(n => !e.Visited.Contains(n));
		Assert.Equal(Passage.NeedsItem, e.MoveTo(beyond));

		otherItem = true;
		Assert.Equal(Passage.Open, e.MoveTo(beyond));
	}
}
