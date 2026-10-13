using System.Numerics;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class MovementTests
{
	// Ilha de raio 3 com um obstáculo no hex (1, 0), ao lado do centro.
	static HexGrid Ilha(params HexCell[] bloqueados) => HexGrid.Island(3, bloqueados);

	static readonly HexCell Centro = new(0, 0);

	// --- grade

	[Fact]
	public void A_ilha_de_raio_3_tem_37_hexagonos()
	{
		Assert.Equal(37, Ilha().Cells.Count);
	}

	[Fact]
	public void Hexagono_do_miolo_tem_6_vizinhos_e_o_da_borda_tem_menos()
	{
		var grade = Ilha();

		Assert.Equal(6, grade.WalkableNeighbors(Centro).Count());
		Assert.Equal(3, grade.WalkableNeighbors(new HexCell(3, 0)).Count());
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(2, -1)]
	[InlineData(-3, 2)]
	public void Converter_para_o_mundo_e_voltar_devolve_o_mesmo_hexagono(int q, int r)
	{
		var cell = new HexCell(q, r);

		Assert.Equal(cell, HexGrid.FromWorld(HexGrid.ToWorld(cell)));
	}

	[Fact]
	public void Hexagono_vizinho_fica_a_raiz_de_3_do_centro()
	{
		var mundo = HexGrid.ToWorld(new HexCell(0, 1));

		Assert.Equal(0f, mundo.X, 3);
		Assert.Equal(MathF.Sqrt(3f), mundo.Y, 3);
	}

	[Fact]
	public void Um_ponto_perto_do_vertice_pertence_ao_hexagono_mais_proximo()
	{
		var quaseNoVizinho = HexGrid.ToWorld(new HexCell(1, 0)) + new Vector2(-0.2f, 0f);

		Assert.Equal(new HexCell(1, 0), HexGrid.FromWorld(quaseNoVizinho));
	}

	// --- caminho

	[Fact]
	public void Caminho_em_linha_reta_vai_do_inicio_ao_fim_passando_por_vizinhos()
	{
		var caminho = Ilha().FindPath(new HexCell(-2, 0), new HexCell(2, 0))!;

		Assert.Equal(new HexCell(-2, 0), caminho[0]);
		Assert.Equal(new HexCell(2, 0), caminho[^1]);
		Assert.Equal(5, caminho.Count);
	}

	[Fact]
	public void Caminho_contorna_o_obstaculo()
	{
		var grade = Ilha(new HexCell(0, 0));

		var caminho = grade.FindPath(new HexCell(-1, 0), new HexCell(1, 0))!;

		Assert.DoesNotContain(new HexCell(0, 0), caminho);
		Assert.Equal(new HexCell(1, 0), caminho[^1]);
		for (var i = 1; i < caminho.Count; i++) Assert.Equal(1, caminho[i - 1].DistanceTo(caminho[i]));
	}

	[Fact]
	public void Sem_saida_nao_ha_caminho()
	{
		var cercado = Ilha(new HexCell(1, 0), new HexCell(1, -1), new HexCell(0, -1), new HexCell(-1, 0), new HexCell(-1, 1), new HexCell(0, 1));

		Assert.Null(cercado.FindPath(new HexCell(2, 0), Centro));
	}

	[Fact]
	public void Destino_bloqueado_ou_fora_da_ilha_nao_tem_caminho()
	{
		var grade = Ilha(new HexCell(1, 0));

		Assert.Null(grade.FindPath(Centro, new HexCell(1, 0)));
		Assert.Null(grade.FindPath(Centro, new HexCell(9, 9)));
	}

	[Fact]
	public void Caminho_ate_o_proprio_hexagono_tem_um_passo()
	{
		Assert.Equal([Centro], Ilha().FindPath(Centro, Centro));
	}

	[Fact]
	public void O_hexagono_livre_mais_proximo_de_um_bloqueado_e_um_vizinho_dele()
	{
		var grade = Ilha(new HexCell(2, 0));

		var perto = grade.NearestWalkable(new HexCell(2, 0))!.Value;

		Assert.True(grade.IsWalkable(perto));
		Assert.Equal(1, perto.DistanceTo(new HexCell(2, 0)));
	}

	[Fact]
	public void O_hexagono_livre_mais_proximo_de_um_ponto_fora_da_ilha_fica_na_borda()
	{
		var perto = Ilha().NearestWalkable(new HexCell(6, 0))!.Value;

		Assert.Equal(new HexCell(3, 0), perto);
	}

	// --- colisão

	[Fact]
	public void Andar_para_dentro_de_um_obstaculo_fica_parado()
	{
		var grade = Ilha(new HexCell(1, 0));
		var parede = HexGrid.ToWorld(new HexCell(1, 0)) + new Vector2(-1.2f, 0f);

		var depois = grade.Move(parede, new Vector2(1f, 0f), 0.2f);

		Assert.Equal(parede, depois);
	}

	[Fact]
	public void Encostar_numa_parede_desliza_pelo_lado_livre()
	{
		var grade = Ilha(new HexCell(1, 0));
		var parede = HexGrid.ToWorld(new HexCell(1, 0)) + new Vector2(-1.2f, 0f);

		var depois = grade.Move(parede, new Vector2(0.3f, 0.3f), 0.2f);

		Assert.Equal(parede.X, depois.X, 3);
		Assert.True(depois.Y > parede.Y);
	}

	[Fact]
	public void Andar_em_campo_aberto_vai_ate_onde_foi_pedido()
	{
		var depois = Ilha().Move(Vector2.Zero, new Vector2(0.4f, 0f), 0.2f);

		Assert.Equal(new Vector2(0.4f, 0f), depois);
	}

	[Fact]
	public void Nao_sai_da_ilha()
	{
		var grade = Ilha();
		var borda = HexGrid.ToWorld(new HexCell(3, 0));

		var depois = grade.Move(borda, new Vector2(3f, 0f), 0.2f);

		Assert.True(grade.IsWalkable(HexGrid.FromWorld(depois)));
		Assert.True(depois.X < borda.X + 1f);
	}

	// --- controlador

	[Fact]
	public void Teclado_move_na_direcao_pedida_com_a_velocidade_dada()
	{
		var controle = new MovementController(Ilha(), Vector2.Zero, speed: 2f);

		controle.Tick(new Vector2(0f, 1f), 0.5f);

		Assert.Equal(0f, controle.Position.X, 3);
		Assert.Equal(1f, controle.Position.Y, 3);
		Assert.True(controle.IsMoving);
	}

	[Fact]
	public void Diagonal_nao_e_mais_rapida_que_reta()
	{
		var reto = new MovementController(Ilha(), Vector2.Zero, 2f);
		var diagonal = new MovementController(Ilha(), Vector2.Zero, 2f);

		reto.Tick(new Vector2(1f, 0f), 0.2f);
		diagonal.Tick(new Vector2(1f, 1f), 0.2f);

		Assert.Equal(reto.Position.Length(), diagonal.Position.Length(), 3);
	}

	[Fact]
	public void Sem_direcao_e_sem_caminho_fica_parado()
	{
		var controle = new MovementController(Ilha(), Vector2.Zero, 2f);

		controle.Tick(Vector2.Zero, 0.5f);

		Assert.False(controle.IsMoving);
		Assert.Equal(Vector2.Zero, controle.Position);
	}

	[Fact]
	public void O_teclado_na_direcao_de_um_obstaculo_nunca_o_atravessa()
	{
		var grade = Ilha(new HexCell(1, 0));
		var controle = new MovementController(grade, Vector2.Zero, 3f);

		for (var i = 0; i < 200; i++) controle.Tick(new Vector2(1f, 0f), 0.05f);

		Assert.NotEqual(new HexCell(1, 0), HexGrid.FromWorld(controle.Position));
	}

	[Fact]
	public void Ir_ate_um_ponto_contorna_obstaculos_e_chega()
	{
		var grade = Ilha(new HexCell(0, 0), new HexCell(1, 0));
		var inicio = HexGrid.ToWorld(new HexCell(-1, 0));
		var alvo = HexGrid.ToWorld(new HexCell(2, 0));
		var controle = new MovementController(grade, inicio, 3f);

		Assert.True(controle.GoTo(alvo));
		for (var i = 0; i < 400 && controle.IsMoving || i == 0; i++)
		{
			controle.Tick(Vector2.Zero, 0.05f);
			Assert.True(grade.IsWalkable(HexGrid.FromWorld(controle.Position)));
		}

		Assert.Equal(alvo.X, controle.Position.X, 2);
		Assert.Equal(alvo.Y, controle.Position.Y, 2);
		Assert.False(controle.IsMoving);
	}

	[Fact]
	public void Tocar_num_obstaculo_leva_ao_hexagono_livre_mais_proximo()
	{
		var grade = Ilha(new HexCell(2, 0));
		var controle = new MovementController(grade, Vector2.Zero, 3f);

		Assert.True(controle.GoTo(HexGrid.ToWorld(new HexCell(2, 0))));
		for (var i = 0; i < 400; i++) controle.Tick(Vector2.Zero, 0.05f);

		Assert.True(grade.IsWalkable(HexGrid.FromWorld(controle.Position)));
		Assert.True(HexGrid.FromWorld(controle.Position).DistanceTo(new HexCell(2, 0)) <= 1);
	}

	[Fact]
	public void Tocar_fora_da_ilha_leva_ate_a_borda()
	{
		var controle = new MovementController(Ilha(), Vector2.Zero, 3f);

		Assert.True(controle.GoTo(HexGrid.ToWorld(new HexCell(8, 0))));
		for (var i = 0; i < 400; i++) controle.Tick(Vector2.Zero, 0.05f);

		Assert.Equal(new HexCell(3, 0), HexGrid.FromWorld(controle.Position));
	}

	[Fact]
	public void O_teclado_cancela_o_caminho_em_andamento()
	{
		var controle = new MovementController(Ilha(), Vector2.Zero, 2f);
		controle.GoTo(HexGrid.ToWorld(new HexCell(3, 0)));
		controle.Tick(Vector2.Zero, 0.1f);

		controle.Tick(new Vector2(0f, -1f), 0.1f);
		var depoisDoTeclado = controle.Position;
		controle.Tick(Vector2.Zero, 0.1f);

		Assert.Equal(depoisDoTeclado, controle.Position);
		Assert.False(controle.IsMoving);
	}

	[Fact]
	public void O_personagem_olha_para_onde_anda()
	{
		var controle = new MovementController(Ilha(), Vector2.Zero, 2f);

		controle.Tick(new Vector2(-1f, 0f), 0.1f);

		Assert.Equal(new Vector2(-1f, 0f), controle.Facing);
	}

	[Fact]
	public void A_tela_e_a_camera_isometrica_se_traduzem_em_direcoes_do_mundo()
	{
		var cima = MovementController.FromScreen(new Vector2(0f, 1f));
		var direita = MovementController.FromScreen(new Vector2(1f, 0f));

		Assert.True(cima.X < 0f && cima.Y < 0f);
		Assert.True(direita.X > 0f && direita.Y < 0f);
		Assert.Equal(1f, MovementController.FromScreen(new Vector2(1f, 1f)).Length(), 3);
		Assert.Equal(Vector2.Zero, MovementController.FromScreen(Vector2.Zero));
	}

	[Fact]
	public void Entre_vizinhos_igualmente_perto_do_toque_vai_ao_mais_perto_de_quem_anda()
	{
		var grade = Ilha(new HexCell(0, 0));

		var doLadoDeLa = grade.NearestWalkable(new HexCell(0, 0), from: new HexCell(2, 0))!.Value;
		var doLadoDeCa = grade.NearestWalkable(new HexCell(0, 0), from: new HexCell(-2, 0))!.Value;

		Assert.Equal(1, doLadoDeLa.Q);
		Assert.Equal(-1, doLadoDeCa.Q);
	}

	[Fact]
	public void Parar_cancela_o_caminho_em_andamento()
	{
		var controle = new MovementController(Ilha(), Vector2.Zero, 2f);
		controle.GoTo(HexGrid.ToWorld(new HexCell(3, 0)));
		controle.Tick(Vector2.Zero, 0.1f);

		controle.Stop();
		var parado = controle.Position;
		controle.Tick(Vector2.Zero, 0.5f);

		Assert.Equal(parado, controle.Position);
		Assert.False(controle.IsMoving);
		Assert.False(controle.HasDestination);
	}
}
