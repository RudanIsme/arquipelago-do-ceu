using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class FreeExpeditionTests
{
	static readonly FloorProvider Provider = new(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback);

	static Expedition Free(ulong world = 1, string discipline = "matematica", int level = 1) =>
		Expedition.Start(Provider, LevelPlan.Free(discipline, level), world, 1, new Energy(5));

	// Vence o que há pelo caminho e sobe a escada.
	static void ClimbOneFloor(Expedition e)
	{
		var path = PathToExit(e);
		foreach (var room in path.Skip(1))
		{
			if (e.NeedsClearing(e.Current) && !e.IsCleared(e.CurrentRoom)) Assert.True(e.ClearCurrent().IsOk);
			Assert.Equal(Passage.Open, e.MoveTo(room));
		}
		Assert.True(e.Ascend());
	}

	static List<int> PathToExit(Expedition e)
	{
		var previous = new Dictionary<int, int> { [e.CurrentRoom] = -1 };
		var queue = new Queue<int>([e.CurrentRoom]);
		while (queue.Count > 0 && !previous.ContainsKey(e.Floor.Exit))
		{
			var room = queue.Dequeue();
			foreach (var next in e.Floor.Neighbors(room).Where(n => !previous.ContainsKey(n)))
			{
				previous[next] = room;
				queue.Enqueue(next);
			}
		}
		var path = new List<int>();
		for (var room = e.Floor.Exit; room != -1; room = previous[room]) path.Insert(0, room);
		return path;
	}

	[Fact]
	public void O_Plano_livre_nao_tem_fim_Marco_nem_Desafio_epico()
	{
		var plan = LevelPlan.Free("portugues", 2);

		Assert.True(plan.Endless);
		Assert.Equal(new FloorPlan(1, false, false, false), plan.For(1));
		Assert.Equal(new FloorPlan(40, false, false, false), plan.For(40));
		Assert.True(plan.HasFloor(1000));
	}

	[Fact]
	public void Na_Livre_os_Andares_seguem_alem_do_fim_de_um_Plano_comum()
	{
		var e = Free();

		for (var floor = 1; floor <= 12; floor++)
		{
			Assert.Equal(floor, e.Floor.Number);
			Assert.DoesNotContain(e.Floor.Rooms, r => r.Kind is RoomKind.Marco or RoomKind.Epic || r.HoldsItem);
			ClimbOneFloor(e);
		}

		Assert.Equal(13, e.Floor.Number);
		Assert.False(e.Finished);
	}

	[Fact]
	public void A_Livre_e_deterministica_pela_seed_e_muda_com_o_mundo()
	{
		var a = Free(world: 5);
		var b = Free(world: 5);
		var other = Free(world: 6);
		for (var i = 0; i < 8; i++)
		{
			ClimbOneFloor(a);
			ClimbOneFloor(b);
			ClimbOneFloor(other);
		}

		Assert.Equal(Describe(a.Floor), Describe(b.Floor));
		Assert.NotEqual(Describe(a.Floor), Describe(other.Floor));
	}

	static string Describe(Floor floor) =>
		string.Join(",", floor.Rooms.Select(r => $"{r.Id}{r.Kind}")) + "|" + string.Join(",", floor.Links.Select(l => $"{l.A}-{l.B}"));

	[Fact]
	public void Todo_Andar_da_Livre_passa_no_validador_ou_cai_no_de_reserva()
	{
		var plan = LevelPlan.Free("matematica", 1);
		for (ulong world = 1; world <= 60; world++)
			for (var floor = 1; floor <= 12; floor++)
			{
				var built = Provider.Get(world, "matematica", 1, plan.For(floor), 1);
				Assert.True(FloorValidator.Validate(built, plan.For(floor)).IsOk, $"mundo {world}, Andar {floor}");
			}
	}

	[Fact]
	public void Descer_na_Livre_volta_ao_Andar_anterior_como_ele_estava()
	{
		var e = Free();
		ClimbOneFloor(e);
		var cleared = e.Cleared.ToList();
		Assert.Empty(cleared);

		Assert.True(e.Descend());

		Assert.Equal(1, e.Floor.Number);
		Assert.True(e.Cleared.Count > 0);
	}
}
