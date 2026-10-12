using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class GameRulesTests
{
	const string Valido = """
		{ "gameName": "Arquipélago do Céu",
		  "roles": { "mathematics": "Matemático", "portuguese": "Escriba" },
		  "maxEnergy": 5,
		  "adaptive": { "streakToLevelUp": 3, "mistakesToLevelDown": 2, "hintsToLevelDown": 2 },
		  "leitnerIntervalsDays": [1, 2, 4, 8, 16] }
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

	[Fact]
	public void Le_a_Energia_maxima_os_limiares_adaptativos_e_os_intervalos_de_Leitner()
	{
		var regras = GameRules.Parse(Valido).Value!;

		Assert.Equal(5, regras.MaxEnergy);
		Assert.Equal(new AdaptiveRules(3, 2, 2), regras.Adaptive);
		Assert.Equal(new[] { 1, 2, 4, 8, 16 }, regras.LeitnerIntervalsDays);
	}

	[Fact]
	public void Trocar_um_valor_no_arquivo_muda_o_resultado()
	{
		var regras = GameRules.Parse(Valido.Replace("\"maxEnergy\": 5", "\"maxEnergy\": 7")).Value!;

		Assert.Equal(7, regras.MaxEnergy);
	}

	[Theory]
	[InlineData("\"maxEnergy\": 5", "\"maxEnergy\": 0", "maxEnergy")]
	[InlineData("\"maxEnergy\": 5", "\"maxEnergy\": 100", "maxEnergy")]
	[InlineData("\"streakToLevelUp\": 3", "\"streakToLevelUp\": 0", "adaptive.streakToLevelUp")]
	[InlineData("\"mistakesToLevelDown\": 2", "\"mistakesToLevelDown\": 0", "adaptive.mistakesToLevelDown")]
	[InlineData("\"hintsToLevelDown\": 2", "\"hintsToLevelDown\": 11", "adaptive.hintsToLevelDown")]
	[InlineData("[1, 2, 4, 8, 16]", "[]", "leitnerIntervalsDays")]
	[InlineData("[1, 2, 4, 8, 16]", "[0, 1]", "leitnerIntervalsDays")]
	[InlineData("[1, 2, 4, 8, 16]", "[2, 1]", "leitnerIntervalsDays")]
	[InlineData("[1, 2, 4, 8, 16]", "[1, 2, 2]", "leitnerIntervalsDays")]
	public void Valor_fora_do_intervalo_e_recusado_dizendo_qual_campo(string original, string novo, string campo)
	{
		var resultado = GameRules.Parse(Valido.Replace(original, novo));

		Assert.False(resultado.IsOk);
		Assert.Contains(campo, resultado.Error);
	}

	[Theory]
	[InlineData("maxEnergy")]
	[InlineData("adaptive")]
	[InlineData("leitnerIntervalsDays")]
	public void Valor_ausente_diz_qual_campo(string campo)
	{
		// Renomear o campo o deixa ausente sem quebrar o JSON.
		var resultado = GameRules.Parse(Valido.Replace($"\"{campo}\"", "\"outroCampo\""));

		Assert.False(resultado.IsOk);
		Assert.Contains(campo, resultado.Error);
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
	[InlineData("\"gameName\": \"Arquipélago do Céu\"", "\"gameName\": \"  \"", "gameName")]
	[InlineData("\"gameName\": \"Arquipélago do Céu\",", "", "gameName")]
	[InlineData("\"mathematics\": \"Matemático\",", "", "roles.mathematics")]
	[InlineData("\"portuguese\": \"Escriba\"", "\"portuguese\": \"\"", "roles.portuguese")]
	public void Nome_ausente_ou_vazio_diz_qual_campo(string original, string novo, string campo)
	{
		var resultado = GameRules.Parse(Valido.Replace(original, novo));

		Assert.False(resultado.IsOk);
		Assert.Contains(campo, resultado.Error);
	}
}
