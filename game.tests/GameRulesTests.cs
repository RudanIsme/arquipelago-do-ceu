using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class GameRulesTests
{
	const string Valido = TestJson.Rules;

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
	[InlineData("\"floorsMin\": 3", "\"floorsMin\": 1", "dungeon.floorsMin")]
	[InlineData("\"floorsMin\": 3", "\"floorsMin\": 5", "dungeon.floorsMin")]
	[InlineData("\"floorsMax\": 4", "\"floorsMax\": 11", "dungeon.floorsMax")]
	[InlineData("\"roomsMin\": 6", "\"roomsMin\": 5", "dungeon.roomsMin")]
	[InlineData("\"roomsMin\": 6", "\"roomsMin\": 10", "dungeon.roomsMin")]
	[InlineData("\"roomsMax\": 9", "\"roomsMax\": 31", "dungeon.roomsMax")]
	[InlineData("\"restEnergy\": 2", "\"restEnergy\": 0", "dungeon.restEnergy")]
	[InlineData("\"shortcutChallenges\": 3", "\"shortcutChallenges\": 0", "dungeon.shortcutChallenges")]
	[InlineData("\"masterChallenges\": 4", "\"masterChallenges\": 1", "dungeon.masterChallenges")]
	[InlineData("\"masterChallenges\": 4", "\"masterChallenges\": 13", "dungeon.masterChallenges")]
	[InlineData("\"shortcutChallenges\": 3", "\"shortcutChallenges\": 10", "dungeon.shortcutChallenges")]
	[InlineData("\"conceptSolves\": 3", "\"conceptSolves\": 0", "dungeon.conceptSolves")]
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
	[InlineData("dungeon")]
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

	[Fact]
	public void Le_as_regras_de_perfil()
	{
		var regras = GameRules.Parse(Valido).Value!;

		Assert.Equal(12, regras.Profile.MaxNicknameLength);
		Assert.Equal(6, regras.Profile.MaxProfiles);
	}

	[Fact]
	public void Le_o_catalogo_de_aparencia_com_nomes_e_cores()
	{
		var catalogo = GameRules.Parse(Valido).Value!.Appearance;

		Assert.Equal(["skinTone", "clothesColor", "accessory"], catalogo.Categories.Select(c => c.Id));
		var pele = catalogo.Categories[0];
		Assert.Equal("Tom de pele", pele.Name);
		Assert.Equal(new AppearanceOption("marfim", "Marfim", "#F7D9C4"), pele.Options[0]);
		Assert.Equal(2, catalogo.Categories[1].Options.Count);
	}

	[Fact]
	public void Categoria_sem_opcoes_e_aceita_e_fica_em_aberto()
	{
		var acessorios = GameRules.Parse(Valido).Value!.Appearance.Categories[2];

		Assert.Equal("accessory", acessorios.Id);
		Assert.Empty(acessorios.Options);
	}

	[Theory]
	[InlineData("\"maxNicknameLength\": 12", "\"maxNicknameLength\": 0", "profile.maxNicknameLength")]
	[InlineData("\"maxNicknameLength\": 12", "\"maxNicknameLength\": 33", "profile.maxNicknameLength")]
	[InlineData("\"maxProfiles\": 6", "\"maxProfiles\": 0", "profile.maxProfiles")]
	[InlineData("\"maxProfiles\": 6", "\"maxProfiles\": 21", "profile.maxProfiles")]
	[InlineData("\"id\": \"skinTone\"", "\"id\": \" \"", "appearance.categories")]
	[InlineData("\"id\": \"clothesColor\"", "\"id\": \"skinTone\"", "skinTone")]
	[InlineData("\"id\": \"cafe\"", "\"id\": \"marfim\"", "marfim")]
	[InlineData("\"name\": \"Café\"", "\"name\": \"\"", "name")]
	[InlineData("\"color\": \"#8D5A3A\"", "\"color\": \"marrom\"", "color")]
	public void Perfil_ou_aparencia_invalidos_sao_recusados_dizendo_o_que_esta_errado(string original, string novo, string trecho)
	{
		var resultado = GameRules.Parse(Valido.Replace(original, novo));

		Assert.False(resultado.IsOk);
		Assert.Contains(trecho, resultado.Error);
	}

	[Theory]
	[InlineData("profile")]
	[InlineData("appearance")]
	public void Perfil_ou_aparencia_ausentes_dizem_qual_campo(string campo)
	{
		var resultado = GameRules.Parse(Valido.Replace($"\"{campo}\"", "\"outroCampo\""));

		Assert.False(resultado.IsOk);
		Assert.Contains(campo, resultado.Error);
	}
}

public class DungeonRulesTests
{
	[Fact]
	public void Le_os_limites_do_Plano_do_Andar_e_do_Descanso()
	{
		var dungeon = GameRules.Parse(TestJson.Rules).Value!.Dungeon;

		Assert.Equal(new DungeonRules(3, 4, 6, 9, 2, 3, 4, 3), dungeon);
	}
}
