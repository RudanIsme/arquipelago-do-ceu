using System.Text.Json;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class SaveStoreTests : IDisposable
{
	readonly string pasta = Path.Combine(Path.GetTempPath(), "arquipelago-testes-" + Guid.NewGuid().ToString("N"));
	string Caminho => Path.Combine(pasta, "save.json");

	static SaveData Exemplo(string apelido = "Luna") =>
		new(SaveData.CurrentVersion, [new ProfileSave("id-1", apelido, 1)]);

	[Fact]
	public void Gravar_e_ler_devolve_os_mesmos_dados()
	{
		var store = new SaveStore(Caminho);

		Assert.True(store.Write(Exemplo()).IsOk);
		var lido = store.Load();

		Assert.True(lido.IsOk, lido.Error);
		Assert.Equal(SaveData.CurrentVersion, lido.Value!.Version);
		Assert.Equal([new ProfileSave("id-1", "Luna", 1)], lido.Value.Profiles);
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
		Assert.Empty(lido.Value!.Profiles);
		Assert.Equal(SaveData.CurrentVersion, lido.Value.Version);
	}

	[Fact]
	public void Queda_no_meio_da_gravacao_nao_corrompe_o_save_anterior()
	{
		var store = new SaveStore(Caminho);
		store.Write(Exemplo("Luna"));

		// É o que uma queda de energia deixa: um temporário pela metade ao lado do save bom.
		File.WriteAllText(Caminho + ".tmp", "{ \"version\": 1, \"profiles\": [ { \"id\": \"id-");

		Assert.Equal("Luna", store.Load().Value!.Profiles[0].Nickname);

		Assert.True(store.Write(Exemplo("Theo")).IsOk);
		Assert.Equal("Theo", store.Load().Value!.Profiles[0].Nickname);
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
		Assert.Equal("Luna", store.Load().Value!.Profiles[0].Nickname);
	}

	public void Dispose()
	{
		if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
	}
}
