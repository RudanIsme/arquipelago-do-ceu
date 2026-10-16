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
