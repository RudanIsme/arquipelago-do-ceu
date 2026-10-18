using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// Os Andares profundos (#153): sem fim, gerados pela mesma seed, nunca bloqueiam nem avançam a Progressão.
public class DeepFloorsTests
{
	static readonly FloorProvider Provider = new(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback);

	static Expedition Start(ulong world = 1) =>
		Expedition.Start(Provider, LevelPlan.Create(ExpeditionFixtures.Rules, world, "matematica", 1, 1), world, 1, new Energy(5), () => true);

	static List<int> PathTo(Expedition e, int goal)
	{
		var previous = new Dictionary<int, int> { [e.CurrentRoom] = -1 };
		var queue = new Queue<int>([e.CurrentRoom]);
		while (queue.Count > 0 && !previous.ContainsKey(goal))
		{
			var room = queue.Dequeue();
			foreach (var next in e.Floor.Neighbors(room).Where(n => !previous.ContainsKey(n)))
			{
				previous[next] = room;
				queue.Enqueue(next);
			}
		}
		var path = new List<int>();
		for (var room = goal; room != -1; room = previous[room]) path.Insert(0, room);
		return path;
	}

	static void WalkTo(Expedition e, int goal)
	{
		foreach (var room in PathTo(e, goal).Skip(1))
		{
			if (e.NeedsClearing(e.Current) && !e.Current.Kind.OpensWithItem && !e.IsCleared(e.CurrentRoom)) Assert.True(e.ClearCurrent().IsOk);
			Assert.Equal(Passage.Open, e.MoveTo(room));
		}
	}

	static void ClimbDeepFloor(Expedition e)
	{
		WalkTo(e, e.Floor.Exit);
		Assert.True(e.Ascend());
	}

	[Fact]
	public void O_Plano_do_Nivel_nao_muda_e_o_Andar_profundo_e_comum()
	{
		var plan = LevelPlan.Create(ExpeditionFixtures.Rules, 1, "matematica", 1, 1);

		Assert.Equal(new FloorPlan(1, false, false, false, Deep: true), LevelPlan.DeepFloor(1));
		Assert.Equal(new FloorPlan(40, false, false, false, Deep: true), LevelPlan.DeepFloor(40));
		Assert.All(plan.Floors, f => Assert.False(f.Deep));
	}

	[Fact]
	public void Entrar_numa_torre_comeca_a_Progressao_e_o_Andar_profundo_so_vem_pela_escada_lateral()
	{
		var e = Start();

		Assert.False(e.InDeep);
		Assert.Equal(1, e.Floor.Number);
		Assert.True(e.CanEnterDeep);
	}

	[Fact]
	public void A_escada_lateral_so_existe_na_entrada_de_um_Andar_da_Progressao_e_no_epico_vencido()
	{
		var e = Start();
		WalkTo(e, e.Floor.Exit);
		Assert.False(e.CanEnterDeep);

		Assert.True(e.Ascend());
		while (e.Floor.Number < e.Plan.Floors.Count)
		{
			WalkTo(e, e.Floor.Exit);
			Assert.True(e.Ascend());
		}
		WalkTo(e, e.Floor.Exit);
		Assert.False(e.CanEnterDeep);
		Assert.True(e.ClearCurrent().IsOk);
		Assert.True(e.CanEnterDeep);
	}

	[Fact]
	public void Os_Andares_profundos_seguem_sem_fim_sem_Marco_Item_nem_Desafio_epico()
	{
		var e = Start();
		Assert.True(e.EnterDeep());

		for (var depth = 1; depth <= 12; depth++)
		{
			Assert.True(e.InDeep);
			Assert.Equal(depth, e.Floor.Number);
			Assert.DoesNotContain(e.Floor.Rooms, r => r.Kind.OpensWithItem || r.Kind.EndsExpedition || r.HoldsItem);
			ClimbDeepFloor(e);
		}

		Assert.Equal(13, e.Floor.Number);
		Assert.False(e.Finished);
	}

	[Fact]
	public void Os_Andares_profundos_nascem_da_mesma_seed_e_mudam_com_o_mundo()
	{
		Expedition Deep(ulong world)
		{
			var e = Start(world);
			e.EnterDeep();
			for (var i = 0; i < 4; i++) ClimbDeepFloor(e);
			return e;
		}

		Assert.Equal(Describe(Deep(5).Floor), Describe(Deep(5).Floor));
		Assert.NotEqual(Describe(Deep(5).Floor), Describe(Deep(6).Floor));
	}

	static string Describe(Floor floor) =>
		string.Join(",", floor.Rooms.Select(r => $"{r.Id}{r.Kind}")) + "|" + string.Join(",", floor.Links.Select(l => $"{l.A}-{l.B}"));

	[Fact]
	public void O_Andar_profundo_numero_1_nao_e_o_Andar_1_da_Progressao()
	{
		var e = Start();
		var main = Describe(e.Floor);

		e.EnterDeep();

		Assert.NotEqual(main, Describe(e.Floor));
	}

	[Fact]
	public void Todo_Andar_profundo_passa_no_validador_ou_cai_no_de_reserva()
	{
		for (ulong world = 1; world <= 60; world++)
			for (var depth = 1; depth <= 12; depth++)
			{
				var plan = LevelPlan.DeepFloor(depth);
				var built = Provider.Get(world, "matematica", 1, plan, 1);
				Assert.True(FloorValidator.Validate(built, plan).IsOk, $"mundo {world}, profundidade {depth}");
				Assert.True(built.Deep);
			}
	}

	[Fact]
	public void Descer_do_primeiro_Andar_profundo_volta_a_Progressao_onde_a_crianca_saiu()
	{
		var e = Start();
		WalkTo(e, e.Floor.Entrance);
		var floor = e.Floor.Number;
		e.EnterDeep();

		Assert.True(e.CanDescend);
		Assert.True(e.Descend());

		Assert.False(e.InDeep);
		Assert.Equal(floor, e.Floor.Number);
		Assert.Equal(e.Floor.Entrance, e.CurrentRoom);
	}

	[Fact]
	public void O_Fast_travel_do_Andar_profundo_volta_a_Progressao()
	{
		var e = Start();
		e.EnterDeep();
		var fast = e.Floor.Rooms.First(r => r.Kind.SavesPoint).Id;
		WalkTo(e, fast);

		Assert.True(e.CanReturnToProgression);
		Assert.True(e.ReturnToProgression());

		Assert.False(e.InDeep);
		Assert.False(e.CanReturnToProgression);
	}

	[Fact]
	public void Voltar_ao_Andar_profundo_o_encontra_como_a_crianca_o_deixou()
	{
		var e = Start();
		e.EnterDeep();
		WalkTo(e, e.Floor.Exit);
		var cleared = e.Cleared.Count;
		Assert.True(cleared > 0);
		e.Ascend();
		e.Descend();

		Assert.Equal(cleared, e.Cleared.Count);
	}

	[Fact]
	public void Vencer_o_fundo_nao_avanca_o_Plano_nem_conclui_o_Nivel()
	{
		var e = Start();
		e.EnterDeep();
		for (var i = 0; i < 3; i++) ClimbDeepFloor(e);

		Assert.False(e.Finished);
		for (var depth = 4; depth >= 1; depth--)
		{
			WalkTo(e, e.Floor.Entrance);
			Assert.True(e.Descend());
		}

		Assert.False(e.Finished);
		Assert.False(e.InDeep);
		Assert.Equal(1, e.Floor.Number);
		Assert.Empty(e.Cleared);
	}

	[Fact]
	public void O_progresso_do_fundo_conta_separado_do_da_Progressao()
	{
		var e = Start();
		e.EnterDeep();

		Assert.Equal(new FloorProgress(0, e.ProgressOf(1, deep: true).Total), e.ProgressOf(1, deep: true));
		Assert.Equal(0, e.ProgressOf(1).Resolved);
		WalkTo(e, e.Floor.Exit);
		Assert.True(e.ProgressOf(1, deep: true).Resolved > 0);
		Assert.Equal(0, e.ProgressOf(1).Resolved);
	}

	[Fact]
	public void Nao_se_entra_no_fundo_de_dentro_do_fundo_nem_se_volta_sem_estar_la()
	{
		var e = Start();

		Assert.False(e.CanReturnToProgression);
		Assert.False(e.ReturnToProgression());
		e.EnterDeep();
		Assert.False(e.CanEnterDeep);
		Assert.False(e.EnterDeep());
	}
}
