using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class FloorProgressTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-andares-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");
	SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	ProgressServices NewProgress(SaveStore store) => ProgressServices.Create(new SaveSession(store, Example));

	[Fact]
	public void O_Nivel_explorado_numa_versao_antiga_do_gerador_continua_nela_e_o_novo_usa_a_atual()
	{
		var progress = NewProgress(new SaveStore(PathName));
		var level = new LevelKey("matematica", 1);

		Assert.Equal(2, progress.Explored.VersionFor("p", level, current: 2));
		progress.Explored.Save("p", new FloorKey("matematica", 1, 1), new FloorExploration([0], []), version: 1);

		Assert.Equal(1, progress.Explored.VersionFor("p", level, current: 2));
		Assert.Equal(2, progress.Explored.VersionFor("p", new LevelKey("matematica", 2), current: 2));
		Assert.Equal(2, progress.Explored.VersionFor("p", new LevelKey("portugues", 1), current: 2));
		Assert.Equal(2, progress.Explored.VersionFor("q", level, current: 2));
	}

	[Fact]
	public void A_exploracao_do_Andar_sobrevive_a_gravar_e_reabrir()
	{
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);

		Assert.Null(progress.Explored.Get("p", new FloorKey("matematica", 1, 1)));
		Assert.True(progress.Explored.Save("p", new FloorKey("matematica", 1, 1), new FloorExploration([0, 1, 3], [1])).IsOk);
		Assert.True(progress.Explored.Save("p", new FloorKey("matematica", 1, 2), new FloorExploration([0], [])).IsOk);
		Assert.True(progress.Explored.Save("p", new FloorKey("matematica", 1, 1), new FloorExploration([0, 1, 3, 4], [1, 4])).IsOk);

		var reopened = ProgressServices.Create(new SaveSession(store, store.Load().Value!.Data));
		var floor1 = reopened.Explored.Get("p", new FloorKey("matematica", 1, 1))!;
		Assert.Equal(new[] { 0, 1, 3, 4 }, floor1.Visited.ToArray());
		Assert.Equal(new[] { 1, 4 }, floor1.Cleared.ToArray());
		Assert.Equal(new[] { 0 }, reopened.Explored.Get("p", new FloorKey("matematica", 1, 2))!.Visited.ToArray());
		Assert.Null(reopened.Explored.Get("p", new FloorKey("portugues", 1, 1)));
		Assert.Null(reopened.Explored.Get("q", new FloorKey("matematica", 1, 1)));
	}

	[Fact]
	public void Save_antigo_sem_exploracao_continua_abrindo_e_nao_e_regravado_ao_consultar()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"fastTravels":["matematica:1:2"]}}]}""";
		File.WriteAllText(PathName, json);
		var store = new SaveStore(PathName);
		var progress = ProgressServices.Create(new SaveSession(store, store.Load().Value!.Data));

		Assert.Null(progress.Explored.Get("p", new FloorKey("matematica", 1, 2)));
		Assert.False(progress.Explored.IsFloorDone("p", new FloorKey("matematica", 1, 2)));
		Assert.Equal(new FloorKey("matematica", 1, 2), progress.Levels.LastFastTravel("p", new LevelKey("matematica", 1)));
		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void O_ultimo_Fast_travel_achado_vale_em_qualquer_Andar_e_so_do_mesmo_Nivel_e_torre()
	{
		var progress = NewProgress(new SaveStore(PathName));
		Assert.Null(progress.Levels.LastFastTravel("p", new LevelKey("matematica", 1)));

		progress.Levels.FindFastTravel("p", new FloorKey("matematica", 1, 3));
		progress.Levels.FindFastTravel("p", new FloorKey("matematica", 1, 1));
		progress.Levels.FindFastTravel("p", new FloorKey("portugues", 1, 4));
		progress.Levels.FindFastTravel("p", new FloorKey("matematica", 2, 4));

		Assert.Equal(new FloorKey("matematica", 1, 1), progress.Levels.LastFastTravel("p", new LevelKey("matematica", 1)));
		Assert.Equal(new FloorKey("portugues", 1, 4), progress.Levels.LastFastTravel("p", new LevelKey("portugues", 1)));
		Assert.Null(progress.Levels.LastFastTravel("q", new LevelKey("matematica", 1)));
	}

	[Fact]
	public void Exploracao_de_outra_versao_do_gerador_e_ignorada()
	{
		var progress = NewProgress(new SaveStore(PathName));
		progress.Explored.Save("p", new FloorKey("matematica", 1, 1), new FloorExploration([0, 1], [1]), version: 1);

		Assert.NotNull(progress.Explored.Get("p", new FloorKey("matematica", 1, 1), version: 1));
		Assert.Null(progress.Explored.Get("p", new FloorKey("matematica", 1, 1), version: 2));
	}
}
