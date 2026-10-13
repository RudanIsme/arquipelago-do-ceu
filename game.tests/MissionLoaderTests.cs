using System.Text.RegularExpressions;
using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class MissionLoaderTests
{
	const string Valid = """
		{ "id": "mat-compor-007", "version": 1, "discipline": "matematica", "concept": "adicao-ate-10",
		  "bncc": ["EF01MA06"], "year": 1, "difficulty": 2, "mechanic": "compor_numero",
		  "params": { "target": 7, "pieces": [1, 2, 3, 4, 5], "maxPieces": 2 },
		  "prompt": "Monte o número 7 para desfazer a nuvem!",
		  "hints": ["Comece pela peça maior.", "4 e mais quanto dá 7?"],
		  "explanation": "3 + 4 = 7. Juntamos 3 e 4 e chegamos a 7.",
		  "accessibility": { "timerAllowed": false },
		  "review": { "status": "pending" } }
		""";

	static MissionLoader Loader() => MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!;

	// Tira o campo (de uma linha só) do JSON de exemplo.
	static string Without(string field) => Regex.Replace(Valid, $"\"{field}\": (\\[[^\\]]*\\]|\"[^\"]*\"),\\s*", "");

	[Fact]
	public void Missao_de_exemplo_e_carregada()
	{
		var result = Loader().Parse("exemplo.json", Valid);

		Assert.True(result.IsOk, result.Error);
		Assert.Equal("mat-compor-007", result.Value!.Id);
		Assert.Equal("compor_numero", result.Value.Mechanic);
		Assert.Equal(7, result.Value.Params["target"].GetInt32());
		Assert.False(result.Value.Stretch);
	}

	[Theory]
	[InlineData("hints")]
	[InlineData("explanation")]
	[InlineData("bncc")]
	[InlineData("concept")]
	public void Campo_faltando_e_recusado_e_a_mensagem_nomeia_o_arquivo_e_o_campo(string field)
	{
		var json = Without(field);
		Assert.DoesNotContain($"\"{field}\"", json);

		var result = Loader().Parse("mat-x.json", json);

		Assert.False(result.IsOk);
		Assert.StartsWith("mat-x.json:", result.Error);
		Assert.Contains(field, result.Error);
	}

	[Theory]
	[InlineData("\"version\": 1", "\"version\": \"1\"")]
	[InlineData("\"difficulty\": 2", "\"difficulty\": 9")]
	[InlineData("\"EF01MA06\"", "\"MA06\"")]
	[InlineData("\"year\": 1", "\"year\": 4")]
	[InlineData("\"status\": \"pending\"", "\"status\": \"talvez\"")]
	[InlineData("\"timerAllowed\": false", "\"timerAllowed\": \"nao\"")]
	[InlineData("\"maxPieces\": 2", "\"maxPieces\": \"dois\"")]
	[InlineData("\"hints\": [\"Comece pela peça maior.\", \"4 e mais quanto dá 7?\"]", "\"hints\": []")]
	public void Campo_de_tipo_ou_valor_errado_e_recusado(string from, string to)
	{
		Assert.Contains(from, Valid);

		Assert.False(Loader().Parse("x.json", Valid.Replace(from, to)).IsOk);
	}

	[Fact]
	public void Campo_desconhecido_e_recusado_para_pegar_erro_de_digitacao()
	{
		Assert.False(Loader().Parse("x.json", Valid.Replace("\"year\": 1,", "\"year\": 1, \"anno\": 1,")).IsOk);
	}

	[Fact]
	public void Parametro_que_a_mecanica_nao_conhece_e_recusado()
	{
		Assert.False(Loader().Parse("x.json", Valid.Replace("\"maxPieces\": 2", "\"maxPieces\": 2, \"codigo\": \"x\"")).IsOk);
	}

	[Fact]
	public void Mecanica_planejada_mas_ainda_inexistente_no_jogo_e_ignorada()
	{
		var result = Loader().Parse("x.json", Valid.Replace("compor_numero", "comparar_quantidades"));

		Assert.False(result.IsOk);
		Assert.Contains("comparar_quantidades", result.Error);
	}

	[Fact]
	public void Mecanica_inventada_pelo_JSON_e_recusada()
	{
		Assert.False(Loader().Parse("x.json", Valid.Replace("compor_numero", "rodar_script")).IsOk);
	}

	[Fact]
	public void JSON_quebrado_ou_vazio_e_recusado_sem_excecao()
	{
		Assert.False(Loader().Parse("a.json", "{ nao e json").IsOk);
		Assert.False(Loader().Parse("b.json", "null").IsOk);
		Assert.False(Loader().Parse("c.json", "").IsOk);
	}

	[Fact]
	public void Load_carrega_as_validas_e_registra_as_ignoradas()
	{
		var load = Loader().Load([("boa.json", Valid), ("ruim.json", Without("hints"))]);

		Assert.Equal(["mat-compor-007"], load.Missions.Select(m => m.Id));
		var rejected = Assert.Single(load.Rejected);
		Assert.Equal("ruim.json", rejected.Source);
	}

	[Fact]
	public void Schema_quebrado_e_falha_explicita()
	{
		Assert.False(MissionLoader.Create("{ nao").IsOk);
	}
}
