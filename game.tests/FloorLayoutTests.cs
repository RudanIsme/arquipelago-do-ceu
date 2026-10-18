using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// O Andar no espaço (#151): Salas posicionadas sem sobreposição, ligadas por corredores a pé.
public class FloorLayoutTests
{
	static readonly RoomForms Forms = RoomFormTests.Forms;
	static readonly DungeonRules Rules = ExpeditionFixtures.Rules;

	static Floor Laid(ulong world, int number, int version, bool deep = false)
	{
		var plan = deep ? LevelPlan.DeepFloor(number) : new FloorPlan(number, false, number % 2 == 0, false);
		var floor = FloorGenerator.Generate(Rules, world, "matematica", 1, plan, version);
		var layout = FloorLayouts.Place(floor, Forms, Seed.Mix(world, "matematica", 1, number, version), style: version >= 3 ? LayoutStyle.Compact : version >= 2 ? LayoutStyle.Varied : LayoutStyle.Legacy);
		Assert.True(layout.IsOk, $"mundo {world}, Andar {number}: {layout.Error}");
		return floor with { Layout = layout.Value };
	}

	// A versão 3 aproxima as Salas: os corredores saem bem mais curtos e o Andar continua sempre posicionável.
	[Fact]
	public void A_versao_3_tem_corredores_bem_mais_curtos_que_a_2_e_sempre_posiciona()
	{
		double Average(int version) => Enumerable.Range(1, 40).SelectMany(world => Enumerable.Range(1, 4).SelectMany(number => Laid((ulong)world, number, version).Layout!.Corridors)).Average(c => c.Cells.Count);

		var compact = Average(3);
		var spread = Average(2);

		Assert.True(compact < spread * 0.7, $"corredor médio: {compact:0.0} na versão 3 contra {spread:0.0} na 2");
		Assert.True(compact <= 8, $"corredor médio da versão 3 = {compact:0.0}");
	}

	[Fact]
	public void O_mesmo_Andar_gera_sempre_o_mesmo_layout()
	{
		var a = Laid(7, 2, 2).Layout!;
		var b = Laid(7, 2, 2).Layout!;

		Assert.Equal(a.Rooms.Select(r => (r.Room, r.Form.Id, r.Origin)), b.Rooms.Select(r => (r.Room, r.Form.Id, r.Origin)));
		Assert.Equal(a.Corridors.Select(c => string.Join(";", c.Cells)), b.Corridors.Select(c => string.Join(";", c.Cells)));
	}

	[Fact]
	public void Em_centenas_de_seeds_o_layout_passa_no_validador_sem_sobreposicao()
	{
		for (ulong world = 1; world <= 150; world++)
			for (var number = 1; number <= 4; number++)
			{
				var floor = Laid(world, number, 2);
				var checkedLayout = FloorLayoutValidator.Validate(floor);
				Assert.True(checkedLayout.IsOk, $"mundo {world}, Andar {number}: {checkedLayout.Error}");
			}
	}

	[Fact]
	public void A_versao_2_sorteia_todas_as_formas_pela_seed()
	{
		var used = new HashSet<string>();
		for (ulong world = 1; world <= 80; world++)
			foreach (var room in Laid(world, 1, 2).Layout!.Rooms) used.Add(room.Form.Id);

		Assert.Equal(Forms.All.Select(f => f.Id).Order(), used.Order());
	}

	[Fact]
	public void A_versao_1_continua_com_a_Sala_de_sempre_e_o_mesmo_grafo()
	{
		var floor = Laid(11, 2, 1);

		Assert.All(floor.Layout!.Rooms, r => Assert.Same(Forms.Legacy, r.Form));
		var again = FloorGenerator.Generate(Rules, 11, "matematica", 1, new FloorPlan(2, false, true, false), 1);
		Assert.Equal(again.Links, floor.Links);
		Assert.Equal(again.Rooms, floor.Rooms);
	}

	[Fact]
	public void Cada_ligacao_tem_um_corredor_de_hexagonos_ligados_de_uma_Sala_a_outra()
	{
		var floor = Laid(3, 3, 2);
		var layout = floor.Layout!;

		Assert.Equal(floor.Links.Count, layout.Corridors.Count);
		foreach (var link in floor.Links)
		{
			var cells = layout.CorridorBetween(link.A, link.B)!;
			Assert.NotEmpty(cells);
			for (var i = 1; i < cells.Count; i++) Assert.Equal(1, cells[i - 1].DistanceTo(cells[i]));
			Assert.Contains(layout.Room(link.A).Cells, c => c.DistanceTo(cells[0]) == 1);
			Assert.Contains(layout.Room(link.B).Cells, c => c.DistanceTo(cells[^1]) == 1);
		}
	}

	[Fact]
	public void Ha_corredores_curtos_longos_retos_e_com_curva()
	{
		var lengths = new List<int>();
		var bends = 0;
		var straight = 0;
		for (ulong world = 1; world <= 60; world++)
			foreach (var corridor in Laid(world, 2, 2).Layout!.Corridors)
			{
				lengths.Add(corridor.Cells.Count);
				var turns = Enumerable.Range(2, Math.Max(0, corridor.Cells.Count - 2))
					.Count(i => Step(corridor.Cells, i) != Step(corridor.Cells, i - 1));
				if (turns == 0) straight++;
				else bends++;
			}

		Assert.True(lengths.Min() <= 6, $"mais curto: {lengths.Min()}");
		Assert.True(lengths.Max() >= 12, $"mais longo: {lengths.Max()}");
		Assert.True(bends > 0 && straight > 0);
	}

	static (int, int) Step(IReadOnlyList<HexCell> cells, int i) => (cells[i].Q - cells[i - 1].Q, cells[i].R - cells[i - 1].R);

	[Fact]
	public void A_crianca_chega_a_pe_de_qualquer_Sala_a_qualquer_outra_pelos_corredores()
	{
		var floor = Laid(5, 2, 2);
		var layout = floor.Layout!;
		var cells = layout.Rooms.SelectMany(r => r.Cells).Concat(layout.Corridors.SelectMany(c => c.Cells));
		var grid = new HexGrid(cells, []);

		var from = layout.Room(floor.Entrance);
		foreach (var room in layout.Rooms)
			Assert.NotNull(grid.FindPath(from.Spot, room.Spot));
	}

	[Fact]
	public void O_Personagem_anda_de_verdade_pelos_corredores_sem_ficar_preso()
	{
		for (ulong world = 1; world <= 25; world++)
		{
			var floor = Laid(world, 2, 2);
			var layout = floor.Layout!;
			var cells = layout.Rooms.SelectMany(r => r.Cells).Concat(layout.Corridors.SelectMany(c => c.Cells));
			var grid = new HexGrid(cells, []);
			foreach (var link in floor.Links)
			{
				var from = layout.Room(link.A);
				var to = layout.Room(link.B);
				var mover = new MovementController(grid, HexGrid.ToWorld(from.Spot), 3f);
				var target = HexGrid.ToWorld(to.Spot);

				Assert.True(mover.GoTo(target));
				for (var i = 0; i < 60 * 40; i++) mover.Tick(System.Numerics.Vector2.Zero, 1f / 60f);

				Assert.True(System.Numerics.Vector2.Distance(mover.Position, target) < 0.1f, $"mundo {world}, {link.A}-{link.B}: parou em {mover.Position}");
			}
		}
	}

	[Fact]
	public void Layout_que_falha_cai_no_Andar_de_reserva_que_tem_layout_valido()
	{
		var plan = new FloorPlan(2, false, true, false);
		var provider = new FloorProvider(Rules, ExpeditionFixtures.Fallback, forms: Forms, layout: (_, _, _, _) => Result<FloorLayout>.Fail("quebrou"));

		var floor = provider.Get(9, "matematica", 1, plan, 2);

		Assert.Equal(ExpeditionFixtures.Fallback.For(plan).Rooms, floor.Rooms);
		Assert.NotNull(floor.Layout);
		Assert.True(FloorLayoutValidator.Validate(floor).IsOk);
	}

	[Fact]
	public void Todo_Andar_de_reserva_tem_layout_valido_em_qualquer_mundo()
	{
		foreach (var (item, marco, epic) in new[] { (false, false, false), (true, false, false), (false, true, false), (false, false, true), (false, true, true) })
			for (ulong world = 1; world <= 30; world++)
			{
				var plan = new FloorPlan(1, item, marco, epic);
				var floor = ExpeditionFixtures.Fallback.For(plan);
				var laid = FloorLayouts.Place(floor, Forms, Seed.Mix(world, "matematica", 1, 1, 2), style: LayoutStyle.Varied);
				Assert.True(laid.IsOk, laid.Error);
				Assert.True(FloorLayoutValidator.Validate(floor with { Layout = laid.Value }).IsOk);
			}
	}

	[Fact]
	public void O_provedor_sempre_entrega_Andar_com_layout_para_a_Progressao_e_o_fundo()
	{
		var provider = new FloorProvider(Rules, ExpeditionFixtures.Fallback, forms: Forms);
		for (ulong world = 1; world <= 40; world++)
		{
			Assert.True(FloorLayoutValidator.Validate(provider.Get(world, "portugues", 2, new FloorPlan(3, false, false, true), 2)).IsOk);
			Assert.True(FloorLayoutValidator.Validate(provider.Get(world, "portugues", 2, LevelPlan.DeepFloor(4), 2)).IsOk);
		}
	}
}
