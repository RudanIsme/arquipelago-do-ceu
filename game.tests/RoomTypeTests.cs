using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class RoomTypeTests
{
	static readonly RoomType Treasure = new("treasure", "Tesouro", "T", "#FFCC00", RoomShape.Diamond, NeedsVictory: true, Free: true);

	static readonly RoomTypes WithTreasure = RoomTypes.Default.With(Treasure);

	[Fact]
	public void O_registro_traz_os_8_tipos_de_Sala_com_ids_unicos()
	{
		Assert.Equal(8, RoomTypes.Default.All.Count);
		Assert.Equal(8, RoomTypes.Default.All.Select(t => t.Id).Distinct().Count());
		Assert.All(RoomTypes.Default.All, t => Assert.Same(t, RoomTypes.Default.Find(t.Id)));
	}

	[Fact]
	public void Pede_vitoria_so_quem_tem_desafio_ou_Marco()
	{
		var needing = RoomTypes.Default.All.Where(t => t.NeedsVictory).Select(t => t.Id);

		Assert.Equal(["combat", "puzzle", "marco", "epic"], needing);
		Assert.Equal([RoomCounting.None, RoomCounting.None], new[] { RoomTypes.Entrance, RoomTypes.Exit }.Select(t => t.Counts));
	}

	[Fact]
	public void Um_tipo_novo_entra_so_com_a_definicao_e_o_gerador_o_sorteia()
	{
		var rooms = Enumerable.Range(0, 60).SelectMany(seed => GenerateFloor((ulong)seed).Rooms).ToList();

		Assert.Contains(rooms, r => r.Kind == Treasure);
	}

	[Fact]
	public void O_validador_aceita_Andares_com_o_tipo_novo_e_ele_pode_guardar_o_Item()
	{
		foreach (var seed in Enumerable.Range(0, 60))
		{
			var floor = GenerateFloor((ulong)seed, holdsItem: true);
			Assert.True(FloorValidator.Validate(floor, new FloorPlan(1, true, false, false)).IsOk, $"seed {seed}");
		}
	}

	[Fact]
	public void A_Expedicao_trata_o_tipo_novo_pela_definicao()
	{
		var floor = Enumerable.Range(0, 60).Select(s => GenerateFloor((ulong)s)).First(f => f.Rooms.Any(r => r.Kind == Treasure));
		var plan = new LevelPlan("matematica", 1, [new(1, false, false, false), new(2, false, true, true)]);
		var provider = new FloorProvider(ExpeditionFixtures.Rules, ExpeditionFixtures.Fallback, (_, _, _, _, _, _) => floor);
		var e = Expedition.Start(provider, plan, 1, 1, new Energy(5));
		var room = floor.Rooms.First(r => r.Kind == Treasure);

		Assert.True(e.NeedsClearing(room));
		Assert.Equal(floor.Rooms.Count(r => r.Kind.Counts != RoomCounting.None), e.ProgressOf(1).Total);
	}

	static Floor GenerateFloor(ulong seed, bool holdsItem = false) =>
		FloorGenerator.Generate(ExpeditionFixtures.Rules, seed, "matematica", 1, new FloorPlan(1, holdsItem, false, false), 1, WithTreasure);
}
