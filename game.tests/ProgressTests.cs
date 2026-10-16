using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class ProgressTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-progress-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");
	SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	[Fact]
	public void Save_antigo_sem_progresso_continua_valido_e_nao_e_regravado_ao_consultar()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":2}]}""";
		File.WriteAllText(PathName, json);
		var store = new SaveStore(PathName);
		var loaded = store.Load();

		Assert.Equal(SaveNotice.None, loaded.Value!.Notice);
		var progress = new ProgressService(new SaveSession(store, loaded.Value.Data));
		Assert.False(progress.EpicDone("p", "matematica", 1));
		Assert.False(progress.HasItem("p", "matematica", 1));
		Assert.False(progress.FloorDiscovered("p", "matematica", 1, 1));
		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void Epico_Item_e_Andar_sobrevivem_a_gravar_e_reabrir()
	{
		var store = new SaveStore(PathName);
		var progress = new ProgressService(new SaveSession(store, Example));

		Assert.True(progress.MarkEpicDone("p", "matematica", 1).IsOk);
		Assert.True(progress.GiveItem("p", "portugues", 1).IsOk);
		Assert.True(progress.DiscoverFloor("p", "matematica", 1, 2).IsOk);

		var reopened = new ProgressService(new SaveSession(store, store.Load().Value!.Data));
		Assert.True(reopened.EpicDone("p", "matematica", 1));
		Assert.True(reopened.HasItem("p", "portugues", 1));
		Assert.True(reopened.FloorDiscovered("p", "matematica", 1, 2));
	}

	[Fact]
	public void O_progresso_e_de_cada_Perfil_Dungeon_e_Nivel()
	{
		var progress = new ProgressService(new SaveSession(new SaveStore(PathName), Example));

		progress.GiveItem("p", "matematica", 1);

		Assert.True(progress.HasItem("p", "matematica", 1));
		Assert.False(progress.HasItem("q", "matematica", 1));
		Assert.False(progress.HasItem("p", "portugues", 1));
		Assert.False(progress.HasItem("p", "matematica", 2));
	}

	[Fact]
	public void Marcar_de_novo_nao_duplica_e_nao_apaga_o_resto_do_save()
	{
		var store = new SaveStore(PathName);
		var progress = new ProgressService(new SaveSession(store, Example));

		progress.GiveItem("p", "matematica", 1);
		progress.GiveItem("p", "matematica", 1);

		var saved = store.Load().Value!.Data;
		Assert.Single(saved.Profiles[0].Progress!.Items!);
		Assert.Equal(2, saved.Profiles.Count);
		Assert.False(saved.Settings!.Voice);
	}

	[Fact]
	public void Os_Fast_travels_encontrados_ficam_no_save_sem_repetir_e_em_ordem()
	{
		var store = new SaveStore(PathName);
		var progress = new ProgressService(new SaveSession(store, Example));

		progress.FindFastTravel("p", "portugues", 1, 2);
		progress.FindFastTravel("p", "matematica", 2, 1);
		progress.FindFastTravel("p", "matematica", 1, 3);
		progress.FindFastTravel("p", "matematica", 1, 3);

		var reopened = new ProgressService(new SaveSession(store, store.Load().Value!.Data));
		Assert.Equal([new FastTravelPoint("matematica", 1, 3), new FastTravelPoint("matematica", 2, 1), new FastTravelPoint("portugues", 1, 2)], reopened.FastTravels("p"));
		Assert.Empty(reopened.FastTravels("q"));
	}

	[Fact]
	public void O_Atalho_de_Nivel_so_pode_ser_tentado_uma_vez_por_Nivel_e_Dungeon()
	{
		var store = new SaveStore(PathName);
		var progress = new ProgressService(new SaveSession(store, Example));
		Assert.False(progress.ShortcutTried("p", "matematica", 1));

		Assert.True(progress.MarkShortcutTried("p", "matematica", 1).IsOk);

		var reopened = new ProgressService(new SaveSession(store, store.Load().Value!.Data));
		Assert.True(reopened.ShortcutTried("p", "matematica", 1));
		Assert.False(reopened.ShortcutTried("p", "portugues", 1));
		Assert.False(reopened.ShortcutTried("p", "matematica", 2));
		Assert.False(reopened.ShortcutTried("q", "matematica", 1));
	}

	[Fact]
	public void O_Desafio_mestre_so_abre_com_os_dois_epicos_do_Nivel_concluidos()
	{
		var progress = new ProgressService(new SaveSession(new SaveStore(PathName), Example));
		Assert.False(progress.MasterOpen("p", 1));

		progress.MarkEpicDone("p", "matematica", 1);
		Assert.False(progress.MasterOpen("p", 1));
		progress.MarkEpicDone("p", "portugues", 2);
		Assert.False(progress.MasterOpen("p", 1), "o épico de outro Nível não vale");

		progress.MarkEpicDone("p", "portugues", 1);
		Assert.True(progress.MasterOpen("p", 1));
		Assert.False(progress.MasterOpen("q", 1));
	}

	[Fact]
	public void Vencer_o_Desafio_mestre_sobe_o_Nivel_liberado_ate_o_maximo()
	{
		var store = new SaveStore(PathName);
		var progress = new ProgressService(new SaveSession(store, Example));

		Assert.Equal(2, progress.RaiseLevel("p", 3).Value!.Level);
		Assert.Equal(3, progress.RaiseLevel("p", 3).Value!.Level);
		Assert.Equal(3, progress.RaiseLevel("p", 3).Value!.Level);

		var saved = store.Load().Value!.Data;
		Assert.Equal(3, saved.Profiles.Single(p => p.Id == "p").Level);
		Assert.Equal(1, saved.Profiles.Single(p => p.Id == "q").Level);
	}

	[Fact]
	public void O_Andar_mais_fundo_descoberto_vale_1_quando_nao_ha_nenhum()
	{
		var progress = new ProgressService(new SaveSession(new SaveStore(PathName), Example));
		Assert.Equal(1, progress.HighestFloor("p", "matematica", 1));

		progress.DiscoverFloor("p", "matematica", 1, 1);
		progress.DiscoverFloor("p", "matematica", 1, 3);
		progress.DiscoverFloor("p", "matematica", 2, 4);

		Assert.Equal(3, progress.HighestFloor("p", "matematica", 1));
		Assert.Equal(1, progress.HighestFloor("p", "portugues", 1));
	}

	[Fact]
	public void Perfil_que_nao_existe_e_uma_falha_e_nao_uma_excecao()
	{
		var progress = new ProgressService(new SaveSession(new SaveStore(PathName), Example));

		Assert.False(progress.GiveItem("nao-existe", "matematica", 1).IsOk);
	}

	[Fact]
	public void Se_a_gravacao_falha_o_progresso_em_memoria_nao_muda()
	{
		Directory.CreateDirectory(folder);
		var blocker = Path.Combine(folder, "arquivo");
		File.WriteAllText(blocker, "x");
		var progress = new ProgressService(new SaveSession(new SaveStore(Path.Combine(blocker, "save.json")), Example));

		var result = progress.GiveItem("p", "matematica", 1);

		Assert.False(result.IsOk);
		Assert.False(progress.HasItem("p", "matematica", 1));
	}
}
