using ArquipelagoDoCeu.Combat;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class EquipmentEnergyTests
{
	[Fact]
	public void Dez_trocas_de_equipamento_nao_curam_energia_ferida()
	{
		var energy = new Energy(5);
		energy.Lose(2);
		for (var i = 0; i < 10; i++)
		{
			foreach (var maximum in new[] { 7, 7, 5, 6, 5 })
			{
				energy.SetMaximum(maximum);
				Assert.Equal(maximum, energy.Max);
				Assert.Equal(3, energy.Current);
			}
		}
	}

	[Fact]
	public void Remover_bonus_limita_atual_e_recolocar_nao_restaura_perda()
	{
		var energy = new Energy(7);
		energy.Lose(1);
		energy.SetMaximum(5);
		Assert.Equal(5, energy.Current);
		energy.SetMaximum(7);
		Assert.Equal(5, energy.Current);
		energy.Recover(1);
		Assert.Equal(6, energy.Current);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-20)]
	public void Maximo_invalido_vira_um_sem_reanimar_energia_zerada(int maximum)
	{
		var energy = new Energy(5);
		energy.Lose(5);
		energy.SetMaximum(maximum);
		Assert.Equal(1, energy.Max);
		Assert.Equal(0, energy.Current);
		Assert.True(energy.IsEmpty);
		energy.SetMaximum(7);
		Assert.True(energy.IsEmpty);
	}
}
