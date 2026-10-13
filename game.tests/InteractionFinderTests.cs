using System.Numerics;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class InteractionFinderTests
{
	static readonly Interactable Album = new("album", new Vector2(0f, 0f));
	static readonly Interactable Portal = new("portal", new Vector2(3f, 0f));

	[Fact]
	public void Perto_de_um_objeto_ele_e_o_alvo()
	{
		var alvo = InteractionFinder.Nearest(new Vector2(1f, 0f), [Album, Portal], reach: 2f);

		Assert.Equal("album", alvo?.Id);
	}

	[Fact]
	public void Longe_de_todos_nao_ha_alvo()
	{
		Assert.Null(InteractionFinder.Nearest(new Vector2(10f, 10f), [Album, Portal], reach: 2f));
	}

	[Fact]
	public void Entre_dois_ao_alcance_vence_o_mais_perto()
	{
		var alvo = InteractionFinder.Nearest(new Vector2(1.8f, 0f), [Album, Portal], reach: 2f);

		Assert.Equal("portal", alvo?.Id);
	}

	[Fact]
	public void No_empate_vence_o_primeiro_da_lista_sempre()
	{
		var alvo = InteractionFinder.Nearest(new Vector2(1.5f, 0f), [Album, Portal], reach: 2f);
		var inverso = InteractionFinder.Nearest(new Vector2(1.5f, 0f), [Portal, Album], reach: 2f);

		Assert.Equal("album", alvo?.Id);
		Assert.Equal("portal", inverso?.Id);
	}

	[Fact]
	public void No_limite_exato_do_alcance_ainda_conta()
	{
		Assert.NotNull(InteractionFinder.Nearest(new Vector2(2f, 0f), [Album], reach: 2f));
		Assert.Null(InteractionFinder.Nearest(new Vector2(2.01f, 0f), [Album], reach: 2f));
	}

	[Fact]
	public void Sem_objetos_nao_ha_alvo()
	{
		Assert.Null(InteractionFinder.Nearest(Vector2.Zero, [], reach: 2f));
	}
}
