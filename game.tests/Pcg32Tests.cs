using ArquipelagoDoCeu.Generation;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class Pcg32Tests
{
	// Valores do pcg32-demo oficial (seed 42, sequência 54): se mudarem, os andares gerados mudam.
	[Fact]
	public void Seed_42_sequencia_54_gera_os_valores_de_referencia()
	{
		var rng = new Pcg32(42, 54);

		uint[] esperado = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
		Assert.Equal(esperado, esperado.Select(_ => rng.NextUInt()).ToArray());
	}

	[Fact]
	public void A_mesma_seed_gera_sempre_a_mesma_sequencia()
	{
		var a = new Pcg32(12345);
		var b = new Pcg32(12345);

		Assert.Equal(Take(a, 100), Take(b, 100));
	}

	[Fact]
	public void Seeds_diferentes_geram_sequencias_diferentes()
	{
		Assert.NotEqual(Take(new Pcg32(1), 20), Take(new Pcg32(2), 20));
	}

	[Fact]
	public void NextInt_fica_dentro_do_limite_e_usa_todos_os_valores()
	{
		var rng = new Pcg32(7);

		var sorteados = Enumerable.Range(0, 500).Select(_ => rng.NextInt(6)).ToList();

		Assert.All(sorteados, n => Assert.InRange(n, 0, 5));
		Assert.Equal(6, sorteados.Distinct().Count());
	}

	[Fact]
	public void NextInt_com_limite_invalido_e_erro_de_programacao()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32(1).NextInt(0));
	}

	static uint[] Take(Pcg32 rng, int n) => Enumerable.Range(0, n).Select(_ => rng.NextUInt()).ToArray();
}
