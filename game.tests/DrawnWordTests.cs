using ArquipelagoDoCeu.Combat;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class DrawnWordTests
{
	static DrawnWord Bola() => new(["bo", "la"]);

	[Fact]
	public void Uma_silaba_por_vez_na_ordem_ate_completar_a_palavra()
	{
		var word = Bola();

		var first = word.Submit("BO");

		Assert.Equal(WriteKind.Accepted, first.Kind);
		Assert.Equal(1, first.Parts);
		Assert.Equal("BO", word.Written);
		Assert.False(word.IsDone);
		Assert.Equal(WriteKind.Accepted, word.Submit("LA").Kind);
		Assert.True(word.IsDone);
	}

	[Fact]
	public void A_palavra_inteira_de_uma_vez_tambem_vale()
	{
		var word = Bola();

		var step = word.Submit("BOLA");

		Assert.Equal(WriteKind.Accepted, step.Kind);
		Assert.Equal(2, step.Parts);
		Assert.True(word.IsDone);
	}

	[Fact]
	public void Silaba_fora_de_ordem_ou_errada_e_resposta_errada()
	{
		var word = Bola();

		Assert.Equal(WriteKind.Wrong, word.Submit("LA").Kind);
		Assert.Equal(WriteKind.Wrong, word.Submit("BA").Kind);
		Assert.Equal("", word.Written);
	}

	[Fact]
	public void Silaba_pela_metade_espera_o_resto_sem_ser_erro()
	{
		var word = Bola();

		Assert.Equal(WriteKind.Incomplete, word.Submit("B").Kind);
		Assert.Equal("B", word.Pending);

		var rest = word.Submit("O");

		Assert.Equal(WriteKind.Accepted, rest.Kind);
		Assert.Equal("BO", word.Written);
		Assert.Equal("", word.Pending);
	}

	[Fact]
	public void Um_pedaco_da_palavra_que_passa_do_limite_de_uma_silaba_tambem_espera()
	{
		var word = Bola();

		Assert.Equal(WriteKind.Incomplete, word.Submit("BOL").Kind);
		Assert.Equal(WriteKind.Accepted, word.Submit("A").Kind);
		Assert.True(word.IsDone);
	}

	[Fact]
	public void Errar_depois_de_uma_metade_descarta_a_metade()
	{
		var word = Bola();
		word.Submit("B");

		Assert.Equal(WriteKind.Wrong, word.Submit("X").Kind);
		Assert.Equal("", word.Pending);
	}

	[Fact]
	public void O_acento_e_o_cedilha_nao_contam_e_maiuscula_ou_minuscula_tanto_faz()
	{
		var word = new DrawnWord(["ma", "ção"]);

		Assert.Equal(WriteKind.Accepted, word.Submit("macao").Kind);
		Assert.True(word.IsDone);
	}

	[Fact]
	public void Depois_de_pronta_nada_mais_e_aceito()
	{
		var word = new DrawnWord(["sol"]);
		word.Submit("SOL");

		Assert.Equal(WriteKind.Wrong, word.Submit("SOL").Kind);
	}

	[Fact]
	public void A_proxima_silaba_e_a_do_pontilhado_e_zera_com_a_palavra_pronta()
	{
		var word = Bola();
		Assert.Equal("bo", word.NextPart);

		word.Submit("BO");
		Assert.Equal("la", word.NextPart);

		word.Submit("LA");
		Assert.Null(word.NextPart);
	}

	[Fact]
	public void Palavra_sem_silabas_marcadas_vale_letra_a_letra_ou_inteira()
	{
		var word = DrawnWord.ByLetters("gato");

		Assert.Equal(WriteKind.Accepted, word.Submit("GA").Kind);
		Assert.Equal("GA", word.Written);
		Assert.Equal(WriteKind.Accepted, word.Submit("TO").Kind);
		Assert.True(word.IsDone);
	}
}
