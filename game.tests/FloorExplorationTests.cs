using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// Os Andares vêm do arquivo de reserva (ver ExpeditionTests): no Andar 1, 0 entrada, 1 combate, 2 Fast travel,
// 3 Descanso, 4 Puzzle e 5 saída; ligações 0-1 1-2 0-3 1-4 4-5. O Andar 2 tem o Marco (5) e o épico (6).
public class FloorExplorationTests
{
	static Expedition OnExitOfFloor1(Energy? energy = null)
	{
		var e = ExpeditionFixtures.Start(energy);
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);
		return e;
	}

	[Fact]
	public void Descer_a_entrada_leva_a_saida_do_Andar_anterior()
	{
		var e = OnExitOfFloor1();
		Assert.True(e.Ascend());
		Assert.True(e.CanDescend);

		Assert.True(e.Descend());

		Assert.Equal(1, e.Floor.Number);
		Assert.Equal(RoomTypes.Exit, e.Current.Kind);
	}

	[Fact]
	public void No_Andar_1_nao_ha_para_onde_descer_e_so_a_entrada_tem_escada_para_baixo()
	{
		var e = ExpeditionFixtures.Start();
		Assert.False(e.CanDescend);
		Assert.False(e.Descend());

		e.MoveTo(1);
		e.ClearCurrent();
		Assert.False(e.CanDescend);
	}

	[Fact]
	public void Subir_e_descer_mantem_visitadas_vencidas_e_Descansos_de_cada_Andar()
	{
		var energy = new Energy(5);
		var e = ExpeditionFixtures.Start(energy);
		energy.Lose(3);
		e.MoveTo(3);
		e.UseRest(2);
		e.MoveTo(0);
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);

		e.Ascend();
		ExpeditionFixtures.ClearAndGo(e, 1);
		e.ClearCurrent();
		e.MoveTo(0);
		e.Descend();

		Assert.Equal(new[] { 0, 1, 3, 4, 5 }, e.Visited.Order().ToArray());
		Assert.Equal(new[] { 1, 4 }, e.Cleared.Order().ToArray());
		Assert.True(e.Rested(3));

		Assert.Equal(Passage.Open, e.MoveTo(4));
		e.MoveTo(5);
		e.Ascend();
		Assert.Equal(new[] { 0, 1 }, e.Visited.Order().ToArray());
		Assert.Equal(new[] { 1 }, e.Cleared.Order().ToArray());
	}

	[Fact]
	public void Subir_sem_vencer_todas_as_Salas_e_permitido_mas_o_Marco_continua_barrando()
	{
		var e = OnExitOfFloor1();

		Assert.True(e.Ascend());
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);
		Assert.Equal(Passage.NeedsItem, e.MoveTo(6));
	}

	[Fact]
	public void A_nevoa_cobre_so_a_vizinhanca_das_Salas_visitadas()
	{
		var e = ExpeditionFixtures.Start();

		Assert.Equal(Sight.Seen, e.SightOf(0));
		Assert.Equal(Sight.Fog, e.SightOf(1));
		Assert.Equal(Sight.Fog, e.SightOf(3));
		Assert.Equal(Sight.Hidden, e.SightOf(2));
		Assert.Equal(Sight.Hidden, e.SightOf(4));
		Assert.Equal(Sight.Hidden, e.SightOf(5));

		e.MoveTo(1);
		Assert.Equal(Sight.Seen, e.SightOf(1));
		Assert.Equal(Sight.Fog, e.SightOf(2));
		Assert.Equal(Sight.Fog, e.SightOf(4));
		Assert.Equal(Sight.Hidden, e.SightOf(5));
	}

	[Fact]
	public void O_cadeado_aparece_na_Sala_nova_cujo_portal_ainda_pede_uma_vitoria()
	{
		var e = ExpeditionFixtures.Start();
		Assert.False(e.IsLocked(1));

		e.MoveTo(1);
		Assert.True(e.IsLocked(2));
		Assert.True(e.IsLocked(4));
		Assert.False(e.IsLocked(3), "o Descanso se alcança pela entrada, que não pede vitória");
		Assert.False(e.IsLocked(0), "Sala já visitada nunca tem cadeado");

		e.ClearCurrent();
		Assert.False(e.IsLocked(2));
		Assert.False(e.IsLocked(4));
	}

	[Fact]
	public void O_cadeado_do_Marco_some_com_o_Item_de_troca()
	{
		var hasItem = false;
		var e = ExpeditionFixtures.Start(hasItem: () => hasItem, startFloor: 2);
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);

		Assert.True(e.IsLocked(6));
		hasItem = true;
		Assert.False(e.IsLocked(6));
	}

	[Fact]
	public void O_progresso_do_Andar_conta_as_Salas_de_conteudo_sem_a_entrada_e_a_saida()
	{
		var e = ExpeditionFixtures.Start();

		Assert.Equal(new FloorProgress(0, 4), e.ProgressOf(1));
		e.MoveTo(3);
		Assert.Equal(new FloorProgress(1, 4), e.ProgressOf(1));
		e.MoveTo(0);
		ExpeditionFixtures.ClearAndGo(e, 1);
		Assert.Equal(new FloorProgress(1, 4), e.ProgressOf(1));
		e.ClearCurrent();
		Assert.Equal(new FloorProgress(2, 4), e.ProgressOf(1));
		e.MoveTo(2);
		Assert.Equal(new FloorProgress(3, 4), e.ProgressOf(1));
		Assert.False(e.IsFloorComplete(1));
	}

	[Fact]
	public void O_Andar_esta_completo_quando_todas_as_Salas_foram_resolvidas()
	{
		var e = ExpeditionFixtures.Start();
		e.MoveTo(3);
		e.MoveTo(0);
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 2);
		e.MoveTo(1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		e.ClearCurrent();

		Assert.Equal(new FloorProgress(4, 4), e.ProgressOf(1));
		Assert.True(e.IsFloorComplete(1));
	}

	[Fact]
	public void O_Marco_so_conta_quando_vencido_e_o_epico_conta_no_ultimo_Andar()
	{
		var e = ExpeditionFixtures.Start(hasItem: () => true, startFloor: 2);

		Assert.Equal(6, e.ProgressOf(2).Total);
		ExpeditionFixtures.ClearAndGo(e, 1);
		ExpeditionFixtures.ClearAndGo(e, 4);
		ExpeditionFixtures.ClearAndGo(e, 5);
		Assert.Equal(3, e.ProgressOf(2).Resolved);
		e.MoveTo(6);
		Assert.Equal(3, e.ProgressOf(2).Resolved);
		e.ClearCurrent();
		Assert.Equal(4, e.ProgressOf(2).Resolved);
	}

	[Fact]
	public void A_exploracao_guardada_volta_e_o_Descanso_zera()
	{
		var energy = new Energy(5);
		var first = ExpeditionFixtures.Start(energy);
		energy.Lose(3);
		first.MoveTo(3);
		first.UseRest(2);
		first.MoveTo(0);
		ExpeditionFixtures.ClearAndGo(first, 1);
		first.ClearCurrent();
		var saved = first.Exploration();

		var second = ExpeditionFixtures.Start(explored: floor => floor == 1 ? saved : null);

		Assert.Equal(new[] { 0, 1, 3 }, second.Visited.Order().ToArray());
		Assert.Equal(new[] { 1 }, second.Cleared.Order().ToArray());
		Assert.Equal(0, second.CurrentRoom);
		Assert.False(second.Rested(3));
	}

	[Fact]
	public void Exploracao_guardada_de_outro_Andar_com_Salas_que_nao_existem_e_ignorada()
	{
		var stale = new FloorExploration([0, 1, 99], [1, 98]);

		var e = ExpeditionFixtures.Start(explored: _ => stale);

		Assert.Equal(new[] { 0, 1 }, e.Visited.Order().ToArray());
		Assert.Equal(new[] { 1 }, e.Cleared.Order().ToArray());
	}

	[Fact]
	public void Save_antigo_sem_estado_trata_o_caminho_ate_o_Fast_travel_como_vencido_e_o_resto_fica_em_nevoa()
	{
		var e = ExpeditionFixtures.Start(startFloor: 1, atFastTravel: true);

		Assert.Equal(RoomTypes.FastTravel, e.Current.Kind);
		Assert.Equal(new[] { 0, 1, 2 }, e.Visited.Order().ToArray());
		Assert.Equal(new[] { 1 }, e.Cleared.Order().ToArray());
		Assert.Equal(Sight.Fog, e.SightOf(4));
		Assert.Equal(Sight.Fog, e.SightOf(3));
	}

	[Fact]
	public void Save_antigo_recomecando_num_Andar_alto_conta_o_caminho_dos_Andares_de_baixo()
	{
		var e = ExpeditionFixtures.Start(startFloor: 2);

		Assert.True(e.Descend());

		Assert.Equal(RoomTypes.Exit, e.Current.Kind);
		Assert.Equal(new[] { 0, 1, 4, 5 }, e.Visited.Order().ToArray());
		Assert.Equal(new[] { 1, 4 }, e.Cleared.Order().ToArray());
	}
}
