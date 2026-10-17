using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class FloorProgressTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-andares-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");
	SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	static readonly CosmeticCatalog Catalog = CosmeticCatalog.Parse("""
		{"cosmetics":[
			{"id":"m11","name":"Lacinho dos Números","description":"Enfeite","discipline":"matematica","level":1,"floor":1},
			{"id":"m12","name":"Mochila de Contar","description":"Enfeite","discipline":"matematica","level":1,"floor":2}]}
		""").Value!;

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	ProgressService NewProgress(SaveStore store) => new(new SaveSession(store, Example));

	[Fact]
	public void A_exploracao_do_Andar_sobrevive_a_gravar_e_reabrir()
	{
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);

		Assert.Null(progress.Exploration("p", "matematica", 1, 1));
		Assert.True(progress.SaveExploration("p", "matematica", 1, 1, new FloorExploration([0, 1, 3], [1])).IsOk);
		Assert.True(progress.SaveExploration("p", "matematica", 1, 2, new FloorExploration([0], [])).IsOk);
		Assert.True(progress.SaveExploration("p", "matematica", 1, 1, new FloorExploration([0, 1, 3, 4], [1, 4])).IsOk);

		var reopened = new ProgressService(new SaveSession(store, store.Load().Value!.Data));
		var floor1 = reopened.Exploration("p", "matematica", 1, 1)!;
		Assert.Equal(new[] { 0, 1, 3, 4 }, floor1.Visited.ToArray());
		Assert.Equal(new[] { 1, 4 }, floor1.Cleared.ToArray());
		Assert.Equal(new[] { 0 }, reopened.Exploration("p", "matematica", 1, 2)!.Visited.ToArray());
		Assert.Null(reopened.Exploration("p", "portugues", 1, 1));
		Assert.Null(reopened.Exploration("q", "matematica", 1, 1));
	}

	[Fact]
	public void Save_antigo_sem_exploracao_continua_abrindo_e_nao_e_regravado_ao_consultar()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"fastTravels":["matematica:1:2"]}}]}""";
		File.WriteAllText(PathName, json);
		var store = new SaveStore(PathName);
		var progress = new ProgressService(new SaveSession(store, store.Load().Value!.Data));

		Assert.Null(progress.Exploration("p", "matematica", 1, 2));
		Assert.False(progress.FloorDone("p", "matematica", 1, 2));
		Assert.Empty(progress.Cosmetics("p"));
		Assert.Equal(new FastTravelPoint("matematica", 1, 2), progress.LastFastTravel("p", "matematica", 1));
		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void O_ultimo_Fast_travel_achado_vale_em_qualquer_Andar_e_so_do_mesmo_Nivel_e_torre()
	{
		var progress = NewProgress(new SaveStore(PathName));
		Assert.Null(progress.LastFastTravel("p", "matematica", 1));

		progress.FindFastTravel("p", "matematica", 1, 3);
		progress.FindFastTravel("p", "matematica", 1, 1);
		progress.FindFastTravel("p", "portugues", 1, 4);
		progress.FindFastTravel("p", "matematica", 2, 4);

		Assert.Equal(new FastTravelPoint("matematica", 1, 1), progress.LastFastTravel("p", "matematica", 1));
		Assert.Equal(new FastTravelPoint("portugues", 1, 4), progress.LastFastTravel("p", "portugues", 1));
		Assert.Null(progress.LastFastTravel("q", "matematica", 1));
	}

	[Fact]
	public void Andar_completo_e_registrado_uma_unica_vez_e_rende_o_Cosmetico_do_catalogo()
	{
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);

		var first = progress.CompleteFloor("p", "matematica", 1, 1, Catalog);
		var again = progress.CompleteFloor("p", "matematica", 1, 1, Catalog);

		Assert.True(first.IsOk && first.Value!.NewlyDone);
		Assert.Equal("m11", first.Value.Cosmetic!.Id);
		Assert.True(again.IsOk && !again.Value!.NewlyDone);
		Assert.Null(again.Value.Cosmetic);

		var reopened = new ProgressService(new SaveSession(store, store.Load().Value!.Data));
		Assert.True(reopened.FloorDone("p", "matematica", 1, 1));
		Assert.False(reopened.FloorDone("p", "matematica", 1, 2));
		Assert.Equal(new[] { "m11" }, reopened.Cosmetics("p").ToArray());
		Assert.Empty(reopened.Cosmetics("q"));
	}

	[Fact]
	public void Andar_sem_Cosmetico_no_catalogo_completa_sem_ganhar_nada()
	{
		var progress = NewProgress(new SaveStore(PathName));

		var done = progress.CompleteFloor("p", "portugues", 3, 4, Catalog);

		Assert.True(done.Value!.NewlyDone);
		Assert.Null(done.Value.Cosmetic);
		Assert.Empty(progress.Cosmetics("p"));
	}

	[Fact]
	public void Se_o_save_falha_o_Andar_nao_vale_como_completo()
	{
		var blocker = Path.Combine(folder, "arquivo");
		Directory.CreateDirectory(folder);
		File.WriteAllText(blocker, "x");
		var progress = new ProgressService(new SaveSession(new SaveStore(Path.Combine(blocker, "save.json")), Example));

		Assert.False(progress.CompleteFloor("p", "matematica", 1, 1, Catalog).IsOk);
		Assert.False(progress.FloorDone("p", "matematica", 1, 1));
		Assert.Empty(progress.Cosmetics("p"));
	}

	[Fact]
	public void Catalogo_recusa_json_quebrado_ou_ligacao_repetida()
	{
		Assert.False(CosmeticCatalog.Parse("{").IsOk);
		Assert.False(CosmeticCatalog.Parse("""{"cosmetics":[]}""").IsOk);
		Assert.False(CosmeticCatalog.Parse("""
			{"cosmetics":[
				{"id":"a","name":"A","description":"","discipline":"matematica","level":1,"floor":1},
				{"id":"b","name":"B","description":"","discipline":"matematica","level":1,"floor":1}]}
			""").IsOk);
		Assert.False(CosmeticCatalog.Parse("""
			{"cosmetics":[
				{"id":"a","name":"A","description":"","discipline":"matematica","level":1,"floor":1},
				{"id":"a","name":"B","description":"","discipline":"matematica","level":1,"floor":2}]}
			""").IsOk);
	}

	[Fact]
	public void O_catalogo_do_jogo_cobre_toda_torre_nivel_e_Andar_possiveis()
	{
		var path = Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "cosmeticos.json");
		var catalog = CosmeticCatalog.Parse(File.ReadAllText(path)).Value!;
		var rules = ExpeditionFixtures.Rules;

		foreach (var discipline in new[] { "matematica", "portugues" })
			foreach (var level in new[] { 1, 2, 3 })
				for (var floor = 1; floor <= rules.FloorsMax; floor++)
					Assert.NotNull(catalog.For(discipline, level, floor));
	}

	[Fact]
	public void Sem_catalogo_o_Andar_nao_e_registrado_para_nao_perder_o_Cosmetico()
	{
		var progress = NewProgress(new SaveStore(PathName));

		var done = progress.CompleteFloor("p", "matematica", 1, 1, CosmeticCatalog.Empty);

		Assert.False(done.Value!.NewlyDone);
		Assert.False(progress.FloorDone("p", "matematica", 1, 1));
	}

	[Fact]
	public void Exploracao_de_outra_versao_do_gerador_e_ignorada()
	{
		var progress = NewProgress(new SaveStore(PathName));
		progress.SaveExploration("p", "matematica", 1, 1, new FloorExploration([0, 1], [1]), version: 1);

		Assert.NotNull(progress.Exploration("p", "matematica", 1, 1, version: 1));
		Assert.Null(progress.Exploration("p", "matematica", 1, 1, version: 2));
	}

	// O treino no Lobby não conta progresso nem dá Cosmético: só a Expedição registra Andares completos.
	[Fact]
	public void So_a_Dungeon_registra_Andar_completo_e_concede_Cosmetico()
	{
		var game = Path.Combine(MissionFiles.RepositoryRoot(), "game");
		var callers = Directory.EnumerateFiles(game, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar))
			.Where(f => File.ReadAllText(f).Contains(".CompleteFloor("))
			.Select(Path.GetFileName)
			.ToList();

		Assert.Equal(new[] { "DungeonRun.cs" }, callers.ToArray());
	}
}
