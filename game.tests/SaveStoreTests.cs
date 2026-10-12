using System.Text.Json;
using System.Text.Json.Nodes;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class SaveStoreTests : IDisposable
{
	readonly string pasta = Path.Combine(Path.GetTempPath(), "arquipelago-testes-" + Guid.NewGuid().ToString("N"));
	string Caminho => Path.Combine(pasta, "save.json");

	static SaveData Exemplo(string apelido = "Luna") =>
		new(SaveData.CurrentVersion, [new ProfileSave("id-1", apelido, 1)]);

	// Formato fictício da versão 0, só para provar a mecânica de migração.
	const string SaveAntigo = """{ "version": 0, "players": [ { "id": "id-1", "name": "Luna" } ] }""";

	static readonly Dictionary<int, Func<JsonObject, JsonObject>> MigracaoDe0 = new()
	{
		[0] = antigo =>
		{
			var perfis = new JsonArray();
			foreach (var jogador in antigo["players"]!.AsArray())
				perfis.Add(new JsonObject { ["id"] = (string)jogador!["id"]!, ["nickname"] = (string)jogador["name"]!, ["level"] = 1 });
			return new JsonObject { ["version"] = 1, ["profiles"] = perfis };
		},
	};

	void Escrever(string texto)
	{
		Directory.CreateDirectory(pasta);
		File.WriteAllText(Caminho, texto);
	}

	string[] Copias(string rotulo) => Directory.Exists(pasta)
		? Directory.GetFiles(pasta, $"save.json.{rotulo}-*").Order().ToArray()
		: [];

	SaveData Dados(SaveStore store) => store.Load().Value!.Data;

	// --- #9: gravar e ler

	[Fact]
	public void Gravar_e_ler_devolve_os_mesmos_dados()
	{
		var store = new SaveStore(Caminho);

		Assert.True(store.Write(Exemplo()).IsOk);
		var lido = store.Load();

		Assert.True(lido.IsOk, lido.Error);
		Assert.Equal(SaveNotice.None, lido.Value!.Notice);
		Assert.Equal(SaveData.CurrentVersion, lido.Value.Data.Version);
		Assert.Equal([new ProfileSave("id-1", "Luna", 1)], lido.Value.Data.Profiles);
	}

	[Fact]
	public void O_arquivo_guarda_o_numero_de_versao_do_formato()
	{
		new SaveStore(Caminho).Write(Exemplo());

		using var documento = JsonDocument.Parse(File.ReadAllText(Caminho));
		Assert.Equal(SaveData.CurrentVersion, documento.RootElement.GetProperty("version").GetInt32());
	}

	[Fact]
	public void Sem_arquivo_o_jogo_comeca_com_um_save_vazio()
	{
		var lido = new SaveStore(Caminho).Load();

		Assert.True(lido.IsOk, lido.Error);
		Assert.Equal(SaveNotice.None, lido.Value!.Notice);
		Assert.Empty(lido.Value.Data.Profiles);
		Assert.Equal(SaveData.CurrentVersion, lido.Value.Data.Version);
	}

	[Fact]
	public void Queda_no_meio_da_gravacao_nao_corrompe_o_save_anterior()
	{
		var store = new SaveStore(Caminho);
		store.Write(Exemplo("Luna"));

		// É o que uma queda de energia deixa: um temporário pela metade ao lado do save bom.
		File.WriteAllText(Caminho + ".tmp", "{ \"version\": 1, \"profiles\": [ { \"id\": \"id-");

		Assert.Equal("Luna", Dados(store).Profiles[0].Nickname);

		Assert.True(store.Write(Exemplo("Theo")).IsOk);
		Assert.Equal("Theo", Dados(store).Profiles[0].Nickname);
	}

	[Fact]
	public void Falha_ao_gravar_devolve_erro_e_preserva_o_save_anterior()
	{
		var store = new SaveStore(Caminho);
		store.Write(Exemplo("Luna"));

		// Uma pasta com o nome do temporário impede a escrita, sem depender de permissões do sistema.
		Directory.CreateDirectory(Caminho + ".tmp");
		var resultado = store.Write(Exemplo("Theo"));

		Assert.False(resultado.IsOk);
		Assert.Contains("gravar", resultado.Error);
		Assert.Equal("Luna", Dados(store).Profiles[0].Nickname);
	}

	// --- #10: migração

	[Fact]
	public void Save_de_versao_antiga_e_migrado_e_o_original_fica_guardado()
	{
		Escrever(SaveAntigo);
		var store = new SaveStore(Caminho, MigracaoDe0);

		var lido = store.Load().Value!;

		Assert.Equal(SaveNotice.Migrated, lido.Notice);
		Assert.Equal(SaveData.CurrentVersion, lido.Data.Version);
		Assert.Equal([new ProfileSave("id-1", "Luna", 1)], lido.Data.Profiles);
		Assert.Equal(SaveAntigo, File.ReadAllText(Assert.Single(Copias("versao-antiga"))));
	}

	[Fact]
	public void Versao_antiga_sem_passo_de_migracao_e_tratada_como_save_ilegivel()
	{
		Escrever(SaveAntigo);

		var lido = new SaveStore(Caminho).Load().Value!;

		Assert.Equal(SaveNotice.Recovered, lido.Notice);
		Assert.Equal(SaveAntigo, File.ReadAllText(Assert.Single(Copias("corrompido"))));
	}

	[Fact]
	public void Migracao_que_falha_nao_derruba_o_jogo_e_guarda_o_save()
	{
		Escrever(SaveAntigo);
		var quebrada = new Dictionary<int, Func<JsonObject, JsonObject>> { [0] = _ => throw new InvalidOperationException("defeito") };

		var lido = new SaveStore(Caminho, quebrada).Load().Value!;

		Assert.Equal(SaveNotice.Recovered, lido.Notice);
		Assert.Equal(SaveAntigo, File.ReadAllText(Assert.Single(Copias("corrompido"))));
	}

	[Fact]
	public void Migracao_que_para_numa_versao_diferente_da_atual_e_tratada_como_save_ilegivel()
	{
		Escrever(SaveAntigo);
		var passo = MigracaoDe0[0];
		var pulaVersao = new Dictionary<int, Func<JsonObject, JsonObject>>
		{
			[0] = antigo => { var novo = passo(antigo); novo["version"] = SaveData.CurrentVersion + 1; return novo; },
		};

		var lido = new SaveStore(Caminho, pulaVersao).Load().Value!;

		Assert.Equal(SaveNotice.Recovered, lido.Notice);
	}

	// --- #10: save corrompido

	[Theory]
	[InlineData("{ isto não é JSON")]
	[InlineData("")]
	[InlineData("[1, 2]")]
	[InlineData("""{ "version": "um" }""")]
	[InlineData("""{ "version": 1 }""")]
	[InlineData("""{ "version": 1, "profiles": [null] }""")]
	public void Save_corrompido_e_copiado_e_o_jogo_comeca_limpo(string conteudo)
	{
		Escrever(conteudo);

		var lido = new SaveStore(Caminho).Load().Value!;

		Assert.Equal(SaveNotice.Recovered, lido.Notice);
		Assert.Empty(lido.Data.Profiles);
		Assert.Equal(conteudo, File.ReadAllText(Assert.Single(Copias("corrompido"))));
		Assert.Equal(Assert.Single(Copias("corrompido")), lido.CopyPath);
	}

	[Fact]
	public void Gravar_depois_de_recuperar_nao_apaga_a_copia()
	{
		Escrever("{ isto não é JSON");
		var store = new SaveStore(Caminho);
		store.Load();

		store.Write(Exemplo("Theo"));

		Assert.Equal("{ isto não é JSON", File.ReadAllText(Assert.Single(Copias("corrompido"))));
		Assert.Equal("Theo", Dados(store).Profiles[0].Nickname);
	}

	[Fact]
	public void Duas_recuperacoes_geram_duas_copias_e_nenhuma_sobrescreve_a_outra()
	{
		var store = new SaveStore(Caminho);
		Escrever("primeiro estrago");
		store.Load();
		Escrever("segundo estrago");
		store.Load();

		var copias = Copias("corrompido");

		Assert.Equal(2, copias.Length);
		Assert.Equal(["primeiro estrago", "segundo estrago"], copias.Select(File.ReadAllText));
	}

	[Fact]
	public void Save_de_versao_mais_nova_e_guardado_e_nunca_apagado()
	{
		var futuro = """{ "version": 99, "profiles": [], "novidade": true }""";
		Escrever(futuro);

		var lido = new SaveStore(Caminho).Load().Value!;

		Assert.Equal(SaveNotice.NewerVersion, lido.Notice);
		Assert.Empty(lido.Data.Profiles);
		Assert.Equal(futuro, File.ReadAllText(Assert.Single(Copias("versao-nova"))));
	}

	[Fact]
	public void Se_nao_der_para_guardar_a_copia_o_save_nao_e_descartado()
	{
		Escrever("{ isto não é JSON");
		// Uma pasta ocupa o nome da cópia e o próximo livre também, então nenhuma cópia é possível.
		for (var n = 1; n <= 1000; n++) Directory.CreateDirectory($"{Caminho}.corrompido-{n}");

		var resultado = new SaveStore(Caminho).Load();

		Assert.False(resultado.IsOk);
		Assert.Contains("cópia", resultado.Error);
		Assert.Equal("{ isto não é JSON", File.ReadAllText(Caminho));
	}

	public void Dispose()
	{
		if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
	}
}
