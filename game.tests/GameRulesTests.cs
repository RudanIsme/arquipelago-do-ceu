using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class GameRulesTests
{
	const string Valido = """
		{ "gameName": "Arquipélago do Céu",
		  "roles": { "mathematics": "Matemático", "portuguese": "Escriba" } }
		""";

	[Fact]
	public void Le_o_nome_do_jogo_e_dos_Papeis()
	{
		var resultado = GameRules.Parse(Valido);

		Assert.True(resultado.IsOk, resultado.Error);
		Assert.Equal("Arquipélago do Céu", resultado.Value!.GameName);
		Assert.Equal("Matemático", resultado.Value.Roles.Mathematics);
		Assert.Equal("Escriba", resultado.Value.Roles.Portuguese);
	}

	[Theory]
	[InlineData("", "vazio")]
	[InlineData("   ", "vazio")]
	[InlineData("{ isto não é JSON", "JSON")]
	[InlineData("[1, 2]", "JSON")]
	public void Texto_ilegivel_gera_erro_claro(string texto, string trechoDoErro)
	{
		var resultado = GameRules.Parse(texto);

		Assert.False(resultado.IsOk);
		Assert.Contains(trechoDoErro, resultado.Error);
	}

	[Theory]
	[InlineData("""{ "roles": { "mathematics": "M", "portuguese": "E" } }""", "gameName")]
	[InlineData("""{ "gameName": "  ", "roles": { "mathematics": "M", "portuguese": "E" } }""", "gameName")]
	[InlineData("""{ "gameName": "J" }""", "roles")]
	[InlineData("""{ "gameName": "J", "roles": { "mathematics": "M" } }""", "roles.portuguese")]
	public void Campo_ausente_ou_vazio_diz_qual_campo(string texto, string campo)
	{
		var resultado = GameRules.Parse(texto);

		Assert.False(resultado.IsOk);
		Assert.Contains(campo, resultado.Error);
	}
}
