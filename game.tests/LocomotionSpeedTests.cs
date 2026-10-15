using System.Numerics;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class LocomotionSpeedTests
{
	[Fact]
	public void Corrida_muda_velocidade_do_clique_sem_perder_destino()
	{
		var grid = HexGrid.Island(5, []);
		var mover = new MovementController(grid, Vector2.Zero, 1f);
		mover.GoTo(HexGrid.ToWorld(new HexCell(0, 4)));
		mover.Tick(Vector2.Zero, .25f);
		Assert.Equal(.25f, mover.Position.Length(), 3);
		mover.Tick(Vector2.Zero, .25f, 3f);
		Assert.Equal(1f, mover.Position.Length(), 3);
		Assert.True(mover.HasDestination);
	}

	[Fact]
	public void Corrida_nao_atravessa_obstaculo_mesmo_em_frame_longo()
	{
		var grid = HexGrid.Island(5, [new HexCell(0, 1)]);
		var mover = new MovementController(grid, Vector2.Zero, 1f);
		mover.Tick(Vector2.UnitY, 1f, 5f);
		Assert.True(mover.Position.Y < 1f);
		Assert.True(grid.IsFree(mover.Position, .2f));
	}

	[Fact]
	public void Velocidade_zero_preserva_rota_sem_animar_movimento()
	{
		var mover = new MovementController(HexGrid.Island(3, []), Vector2.Zero, 1f);
		mover.GoTo(HexGrid.ToWorld(new HexCell(0, 2)));
		mover.Tick(Vector2.Zero, .5f, 0f);
		Assert.Equal(Vector2.Zero, mover.Position);
		Assert.False(mover.IsMoving);
		Assert.True(mover.HasDestination);
	}
}
