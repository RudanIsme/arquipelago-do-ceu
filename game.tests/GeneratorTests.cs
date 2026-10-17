using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class LevelPlanTests
{
	static readonly DungeonRules Rules = GameRules.Parse(TestJson.Rules).Value!.Dungeon;

	[Fact]
	public void A_mesma_entrada_gera_sempre_o_mesmo_Plano()
	{
		var a = LevelPlan.Create(Rules, 42, "matematica", 1, 1);
		var b = LevelPlan.Create(Rules, 42, "matematica", 1, 1);

		Assert.Equal(a.Floors, b.Floors);
	}

	[Fact]
	public void Entradas_diferentes_dao_Planos_diferentes()
	{
		var plans = Enumerable.Range(0, 200).Select(seed => LevelPlan.Create(Rules, (ulong)seed, "matematica", 1, 1))
			.Select(p => string.Join("|", p.Floors)).Distinct();

		Assert.True(plans.Count() > 3);
	}

	[Fact]
	public void Cada_parte_da_seed_muda_o_resultado()
	{
		string Key(ulong world, string discipline, int level, int version) =>
			string.Join("|", Enumerable.Range(0, 30).Select(i => string.Join(",", LevelPlan.Create(Rules, world + (ulong)i * 977, discipline, level, version).Floors)));

		var baseline = Key(1, "matematica", 1, 1);

		Assert.NotEqual(baseline, Key(2, "matematica", 1, 1));
		Assert.NotEqual(baseline, Key(1, "portugues", 1, 1));
		Assert.NotEqual(baseline, Key(1, "matematica", 2, 1));
		Assert.NotEqual(baseline, Key(1, "matematica", 1, 2));
	}

	[Fact]
	public void Em_milhares_de_seeds_o_Plano_respeita_limites_e_a_ordem_Item_Marco_epico()
	{
		for (ulong seed = 0; seed < 3000; seed++)
		{
			var floors = LevelPlan.Create(Rules, seed, seed % 2 == 0 ? "matematica" : "portugues", (int)(seed % 3) + 1, 1).Floors;

			Assert.InRange(floors.Count, Rules.FloorsMin, Rules.FloorsMax);
			Assert.Equal(Enumerable.Range(1, floors.Count), floors.Select(f => f.Number));
			Assert.Single(floors, f => f.HoldsItem);
			Assert.Single(floors, f => f.HasMarco);
			Assert.Single(floors, f => f.HasEpic);
			Assert.True(floors[^1].HasEpic);
			Assert.True(floors.Single(f => f.HoldsItem).Number < floors.Single(f => f.HasMarco).Number, $"seed {seed}");
		}
	}
}

public class FloorGeneratorTests
{
	static readonly DungeonRules Rules = GameRules.Parse(TestJson.Rules).Value!.Dungeon;

	static Floor Generate(ulong seed, string discipline = "matematica", int level = 1, int floor = 1)
	{
		var plan = LevelPlan.Create(Rules, seed, discipline, level, 1);
		return FloorGenerator.Generate(Rules, seed, discipline, level, plan.Floors[Math.Min(floor, plan.Floors.Count) - 1], 1);
	}

	[Fact]
	public void A_mesma_entrada_gera_sempre_o_mesmo_Andar()
	{
		var a = Generate(7);
		var b = Generate(7);

		Assert.Equal(a.Rooms, b.Rooms);
		Assert.Equal(a.Links, b.Links);
	}

	[Fact]
	public void Seeds_diferentes_dao_Andares_diferentes()
	{
		var shapes = Enumerable.Range(0, 100).Select(s => Generate((ulong)s)).Select(f => string.Join(",", f.Rooms.Select(r => r.Kind)) + string.Join(",", f.Links)).Distinct();

		Assert.True(shapes.Count() > 10);
	}

	[Fact]
	public void Em_milhares_de_seeds_cada_Andar_tem_o_que_o_Plano_manda()
	{
		for (ulong seed = 0; seed < 3000; seed++)
		{
			var plan = LevelPlan.Create(Rules, seed, "matematica", 1, 1);
			foreach (var floorPlan in plan.Floors)
			{
				var floor = FloorGenerator.Generate(Rules, seed, "matematica", 1, floorPlan, 1);
				var where = $"seed {seed} andar {floorPlan.Number}";

				Assert.InRange(floor.Rooms.Count, Rules.RoomsMin, Rules.RoomsMax);
				Assert.Equal(Enumerable.Range(0, floor.Rooms.Count), floor.Rooms.Select(r => r.Id));
				Assert.Equal(RoomKind.Entrance, floor.Rooms[floor.Entrance].Kind);
				Assert.Single(floor.Rooms, r => r.Kind == RoomKind.Entrance);
				Assert.Single(floor.Rooms, r => r.Kind == RoomKind.FastTravel);
				Assert.Single(floor.Rooms, r => r.Kind == RoomKind.Rest);
				Assert.Equal(floorPlan.HasMarco ? 1 : 0, floor.Rooms.Count(r => r.Kind == RoomKind.Marco));
				Assert.Equal(floorPlan.HoldsItem ? 1 : 0, floor.Rooms.Count(r => r.HoldsItem));
				Assert.Equal(floorPlan.HasEpic ? RoomKind.Epic : RoomKind.Exit, floor.Rooms[floor.Exit].Kind);
				Assert.True(floor.Rooms.Where(r => r.HoldsItem).All(r => r.Kind is RoomKind.Combat or RoomKind.Puzzle), where);
				Assert.All(floor.Links, l => Assert.True(l.A != l.B && l.A >= 0 && l.B >= 0 && l.A < floor.Rooms.Count && l.B < floor.Rooms.Count, where));
				Assert.Equal(floor.Links.Count, floor.Links.Select(l => (Math.Min(l.A, l.B), Math.Max(l.A, l.B))).Distinct().Count());
				Assert.Equal(floor.Rooms.Count, Reachable(floor).Count);
			}
		}
	}

	static HashSet<int> Reachable(Floor floor)
	{
		var seen = new HashSet<int> { floor.Entrance };
		var queue = new Queue<int>([floor.Entrance]);
		while (queue.Count > 0)
		{
			var room = queue.Dequeue();
			foreach (var next in floor.Neighbors(room))
				if (seen.Add(next)) queue.Enqueue(next);
		}
		return seen;
	}
}

public class FloorValidatorTests
{
	static readonly DungeonRules Rules = GameRules.Parse(TestJson.Rules).Value!.Dungeon;

	// Monta um Andar à mão para provar que o validador recusa cada regra quebrada.
	static Floor Hand((RoomKind Kind, bool Item)[] rooms, (int, int)[] links, int? exit = null) =>
		new(1, rooms.Select((r, i) => new Room(i, r.Kind, r.Item)).ToList(), links.Select(l => new Link(l.Item1, l.Item2)).ToList(), 0, exit ?? rooms.Length - 1);

	static readonly FloorPlan WithMarco = new(1, false, true, false);

	// Entrada(0) - Combate(1) - Fast travel(2); Descanso(3) na entrada; Marco(4) depois do combate; saída(5).
	static (RoomKind, bool)[] GoodRooms => [(RoomKind.Entrance, false), (RoomKind.Combat, false), (RoomKind.FastTravel, false), (RoomKind.Rest, false), (RoomKind.Marco, false), (RoomKind.Exit, false)];
	static (int, int)[] GoodLinks => [(0, 1), (1, 2), (0, 3), (1, 4), (4, 5)];

	[Fact]
	public void Aceita_os_Andares_gerados_em_milhares_de_seeds()
	{
		for (ulong seed = 0; seed < 3000; seed++)
		{
			var discipline = seed % 2 == 0 ? "matematica" : "portugues";
			var plan = LevelPlan.Create(Rules, seed, discipline, (int)(seed % 3) + 1, 1);
			Assert.True(FloorValidator.ValidatePlan(plan).IsOk, $"plano da seed {seed}");
			foreach (var floorPlan in plan.Floors)
			{
				var floor = FloorGenerator.Generate(Rules, seed, discipline, plan.Level, floorPlan, 1);
				var result = FloorValidator.Validate(floor, floorPlan);
				Assert.True(result.IsOk, $"seed {seed} andar {floorPlan.Number}: {result.Error}");
			}
		}
	}

	[Fact]
	public void O_Andar_de_exemplo_montado_a_mao_e_valido()
	{
		Assert.True(FloorValidator.Validate(Hand(GoodRooms, GoodLinks), WithMarco).IsOk);
	}

	[Fact]
	public void Recusa_Sala_que_nao_da_para_alcancar()
	{
		var result = FloorValidator.Validate(Hand(GoodRooms, [(0, 1), (1, 2), (1, 4), (4, 5)]), WithMarco);

		Assert.False(result.IsOk);
		Assert.Contains("alcançável", result.Error);
	}

	[Fact]
	public void Recusa_Fast_travel_que_so_se_alcanca_passando_pelo_Marco()
	{
		var rooms = new[] { (RoomKind.Entrance, false), (RoomKind.Combat, false), (RoomKind.Rest, false), (RoomKind.Marco, false), (RoomKind.FastTravel, false), (RoomKind.Exit, false) };

		var result = FloorValidator.Validate(Hand(rooms, [(0, 1), (0, 2), (1, 3), (3, 4), (3, 5)]), WithMarco);

		Assert.False(result.IsOk);
		Assert.Contains("Fast travel", result.Error);
	}

	[Fact]
	public void Recusa_Puzzle_que_so_se_alcanca_passando_pelo_Marco()
	{
		var rooms = new[] { (RoomKind.Entrance, false), (RoomKind.FastTravel, false), (RoomKind.Rest, false), (RoomKind.Marco, false), (RoomKind.Puzzle, false), (RoomKind.Exit, false) };

		var result = FloorValidator.Validate(Hand(rooms, [(0, 1), (0, 2), (0, 3), (3, 4), (3, 5)]), WithMarco);

		Assert.False(result.IsOk);
		Assert.Contains("Puzzle", result.Error);
	}

	[Fact]
	public void Recusa_saida_que_da_para_alcancar_sem_passar_pelo_Marco()
	{
		var result = FloorValidator.Validate(Hand(GoodRooms, [(0, 1), (1, 2), (0, 3), (1, 4), (4, 5), (1, 5)]), WithMarco);

		Assert.False(result.IsOk);
		Assert.Contains("Marco", result.Error);
	}

	[Fact]
	public void Recusa_Andar_sem_o_Marco_ou_sem_a_Sala_do_Item_que_o_Plano_manda()
	{
		var noMarco = FloorValidator.Validate(Hand(GoodRooms.Where(r => r.Item1 != RoomKind.Marco).ToArray(), [(0, 1), (1, 2), (0, 3), (1, 4)]), WithMarco);
		var noItem = FloorValidator.Validate(Hand(GoodRooms, GoodLinks), new FloorPlan(1, true, true, false));

		Assert.False(noMarco.IsOk);
		Assert.False(noItem.IsOk);
		Assert.Contains("Item", noItem.Error);
	}

	[Fact]
	public void Recusa_o_Desafio_epico_que_nao_e_a_saida_do_ultimo_Andar()
	{
		var result = FloorValidator.Validate(Hand(GoodRooms, GoodLinks), new FloorPlan(1, false, true, true));

		Assert.False(result.IsOk);
		Assert.Contains("épico", result.Error);
	}

	[Fact]
	public void Recusa_ligacao_para_Sala_que_nao_existe_e_sem_excecao()
	{
		var result = FloorValidator.Validate(Hand(GoodRooms, [(0, 1), (1, 2), (0, 3), (1, 4), (4, 9)]), WithMarco);

		Assert.False(result.IsOk);
	}

	[Fact]
	public void Recusa_Plano_com_o_Item_depois_do_Marco_ou_epico_fora_do_fim()
	{
		var itemAfterMarco = new LevelPlan("matematica", 1, [new(1, false, true, false), new(2, true, false, false), new(3, false, false, true)]);
		var epicInTheMiddle = new LevelPlan("matematica", 1, [new(1, true, false, true), new(2, false, true, false)]);

		Assert.False(FloorValidator.ValidatePlan(itemAfterMarco).IsOk);
		Assert.False(FloorValidator.ValidatePlan(epicInTheMiddle).IsOk);
	}
}

public class FallbackFloorTests
{
	static readonly DungeonRules Rules = GameRules.Parse(TestJson.Rules).Value!.Dungeon;

	static FallbackFloors Fallback() =>
		FallbackFloors.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "andares-reserva.json"))).Value!;

	[Fact]
	public void Existe_Andar_de_reserva_valido_para_todo_formato_de_Andar_do_Plano()
	{
		var fallback = Fallback();
		for (ulong seed = 0; seed < 500; seed++)
			foreach (var floorPlan in LevelPlan.Create(Rules, seed, "portugues", 2, 1).Floors)
			{
				var floor = fallback.For(floorPlan);
				var result = FloorValidator.Validate(floor, floorPlan);
				Assert.True(result.IsOk, $"andar {floorPlan}: {result.Error}");
				Assert.Equal(floorPlan.Number, floor.Number);
			}
	}

	[Fact]
	public void O_provedor_usa_o_Andar_gerado_quando_ele_e_valido()
	{
		var provider = new FloorProvider(Rules, Fallback());
		var plan = LevelPlan.Create(Rules, 3, "matematica", 1, 1).Floors[0];

		var floor = provider.Get(3, "matematica", 1, plan, 1);

		var expected = FloorGenerator.Generate(Rules, 3, "matematica", 1, plan, 1);
		Assert.Equal(expected.Rooms, floor.Rooms);
		Assert.Equal(expected.Links, floor.Links);
	}

	[Fact]
	public void O_provedor_usa_o_Andar_de_reserva_quando_o_gerador_erra_e_nunca_lanca_excecao()
	{
		GenerateFloor broken = (_, _, _, _, p, _) =>
			new Floor(p.Number, [new Room(0, RoomKind.Entrance)], [], 0, 0);
		var provider = new FloorProvider(Rules, Fallback(), broken);
		var plan = LevelPlan.Create(Rules, 3, "matematica", 1, 1).Floors[0];

		var floor = provider.Get(3, "matematica", 1, plan, 1);

		Assert.True(FloorValidator.Validate(floor, plan).IsOk);
		Assert.Equal(Fallback().For(plan).Rooms, floor.Rooms);
		Assert.Equal(Fallback().For(plan).Links, floor.Links);
	}

	[Theory]
	[InlineData("")]
	[InlineData("{ não é json")]
	[InlineData("{\"floors\": []}")]
	public void Arquivo_de_reserva_ruim_e_uma_falha_explicada(string json)
	{
		var result = FallbackFloors.Parse(json);

		Assert.False(result.IsOk);
		Assert.False(string.IsNullOrWhiteSpace(result.Error));
	}

	[Fact]
	public void O_arquivo_real_de_regras_traz_o_bloco_dungeon()
	{
		var json = File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "rules.json"));

		Assert.True(GameRules.Parse(json).IsOk);
	}
}
