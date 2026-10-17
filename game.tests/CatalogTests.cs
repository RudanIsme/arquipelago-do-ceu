using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class CatalogTests
{
	sealed class FakeSource(Dictionary<string, string> files) : IDataSource
	{
		public int Reads { get; private set; }

		public Result<string> Read(string name)
		{
			Reads++;
			return files.TryGetValue(name, out var text) ? Result<string>.Ok(text) : Result<string>.Fail($"Não foi possível abrir {name}.");
		}
	}

	sealed class DiskSource : IDataSource
	{
		public Result<string> Read(string name)
		{
			var path = Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", name);
			return File.Exists(path) ? Result<string>.Ok(File.ReadAllText(path)) : Result<string>.Fail($"Não foi possível abrir {name}.");
		}
	}

	static Result<int> ParseNumber(string text) => int.TryParse(text, out var n) ? Result<int>.Ok(n) : Result<int>.Fail("não é número");

	[Fact]
	public void Required_devolve_o_catalogo_lido_e_nao_le_o_arquivo_de_novo()
	{
		var source = new FakeSource(new() { ["a.json"] = "7" });
		var loader = new CatalogLoader(source, _ => { });

		Assert.Equal(7, loader.Required("a.json", ParseNumber).Value);
		Assert.Equal(7, loader.Required("a.json", ParseNumber).Value);
		Assert.Equal(1, source.Reads);
	}

	[Fact]
	public void Arquivo_ausente_e_dado_ruim_dizem_qual_arquivo()
	{
		var loader = new CatalogLoader(new FakeSource(new() { ["ruim.json"] = "x" }), _ => { });

		Assert.Contains("falta.json", loader.Required("falta.json", ParseNumber).Error);
		var bad = loader.Required("ruim.json", ParseNumber);
		Assert.Contains("ruim.json", bad.Error);
		Assert.Contains("não é número", bad.Error);
	}

	[Fact]
	public void Optional_com_falha_devolve_o_padrao_e_avisa_uma_vez_so()
	{
		var warnings = new List<string>();
		var loader = new CatalogLoader(new FakeSource(new()), warnings.Add);

		Assert.Equal(-1, loader.Optional("falta.json", ParseNumber, -1));
		Assert.Equal(-1, loader.Optional("falta.json", ParseNumber, -1));

		Assert.Single(warnings);
		Assert.Contains("falta.json", warnings[0]);
	}

	[Fact]
	public void Optional_com_arquivo_bom_devolve_o_catalogo_sem_avisar()
	{
		var warnings = new List<string>();
		var loader = new CatalogLoader(new FakeSource(new() { ["a.json"] = "3" }), warnings.Add);

		Assert.Equal(3, loader.Optional("a.json", ParseNumber, -1));
		Assert.Empty(warnings);
	}

	[Fact]
	public void O_leitor_de_JSON_recusa_texto_quebrado_e_raiz_nula_dizendo_o_que_era()
	{
		var broken = CatalogJson.Deserialize<List<int>>("{", "O catálogo X");
		var empty = CatalogJson.Deserialize<List<int>>("null", "O catálogo X");

		Assert.Contains("O catálogo X não é um JSON válido", broken.Error);
		Assert.Contains("O catálogo X está vazio", empty.Error);
		Assert.Equal(new[] { 1, 2 }, CatalogJson.Deserialize<List<int>>("[1,2]", "X").Value!.ToArray());
	}

	[Fact]
	public void Id_repetido_e_encontrado()
	{
		Assert.Equal("b", CatalogJson.Duplicate(new[] { "a", "b", "b" }, x => x));
		Assert.Null(CatalogJson.Duplicate(new[] { "a", "b" }, x => x));
	}

	[Fact]
	public void Os_catalogos_do_jogo_carregam_pelo_mesmo_contrato()
	{
		var loader = new CatalogLoader(new DiskSource(), message => throw new Xunit.Sdk.XunitException(message));

		Assert.True(loader.Required("cosmeticos.json", CosmeticCatalog.Parse).IsOk);
		Assert.True(loader.Required("equipamentos.json", GearCatalog.Parse).IsOk);
		Assert.True(loader.Required("conceitos.json", ConceptNames.Parse).IsOk);
		Assert.True(loader.Required("andares-reserva.json", FallbackFloors.Parse).IsOk);
		Assert.True(loader.Required("rules.json", GameRules.Parse).IsOk);
	}

	[Theory]
	[InlineData("cosmeticos.json")]
	[InlineData("equipamentos.json")]
	[InlineData("conceitos.json")]
	[InlineData("andares-reserva.json")]
	[InlineData("rules.json")]
	public void Cada_catalogo_com_JSON_quebrado_falha_dizendo_o_arquivo(string file)
	{
		var loader = new CatalogLoader(new FakeSource(new() { [file] = "{" }), _ => { });

		var result = file switch
		{
			"cosmeticos.json" => Fail(loader.Required(file, CosmeticCatalog.Parse)),
			"equipamentos.json" => Fail(loader.Required(file, GearCatalog.Parse)),
			"conceitos.json" => Fail(loader.Required(file, ConceptNames.Parse)),
			"andares-reserva.json" => Fail(loader.Required(file, FallbackFloors.Parse)),
			_ => Fail(loader.Required(file, GameRules.Parse)),
		};

		Assert.StartsWith(file, result);
		Assert.Contains("JSON", result);
	}

	static string Fail<T>(Result<T> result)
	{
		Assert.False(result.IsOk);
		return result.Error!;
	}
}
