using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// Saves como cada marco do jogo os gravou. Todos continuam abrindo na versão atual, sem aviso e sem serem regravados ao consultar.
public sealed class SaveCompatTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-compat-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	const string Lobby = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":2,"appearance":{"hair":"blue"}}],"settings":{"voice":false}}""";

	const string Experiment = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"journey":{"variant":"book","equipment":{"armor":"scout","helmet":"open"}}}]}""";

	const string Dungeons = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"epicDone":["matematica:1"],"items":["matematica:1"],"floors":["matematica:1:1","matematica:1:2"],"fastTravels":["matematica:1:2"],"shortcutsTried":["portugues:1"]}}]}""";

	const string Exploration = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"fastTravels":["matematica:1:2"],"explored":[{"key":"matematica:1:1","visited":[0,1],"cleared":[1],"version":1}],"floorsDone":["matematica:1:1"],"cosmetics":["matematica-1-1"]}}]}""";

	const string Growth = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"solved":{"matematica:adicao-ate-10":2},"mastered":["matematica:composicao-ate-5"],"coins":12}}]}""";

	const string Gear = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"gear":{"matematica":{"armor":"scout","helmet":"open"}}}]}""";

	ProgressServices Open(string json, out GearService gear, out string untouched)
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(PathName, json);
		var store = new SaveStore(PathName);
		var loaded = store.Load();
		Assert.True(loaded.IsOk);
		Assert.Equal(SaveNotice.None, loaded.Value!.Notice);
		var session = new SaveSession(store, loaded.Value.Data);
		gear = new GearService(session, GearCatalog.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "equipamentos.json"))).Value!);
		untouched = json;
		return ProgressServices.Create(session);
	}

	[Theory]
	[InlineData(nameof(Lobby))]
	[InlineData(nameof(Experiment))]
	[InlineData(nameof(Dungeons))]
	[InlineData(nameof(Exploration))]
	[InlineData(nameof(Growth))]
	[InlineData(nameof(Gear))]
	public void Todo_save_de_marco_antigo_abre_sem_aviso_e_nao_e_regravado(string which)
	{
		var json = which switch
		{
			nameof(Lobby) => Lobby,
			nameof(Experiment) => Experiment,
			nameof(Dungeons) => Dungeons,
			nameof(Exploration) => Exploration,
			nameof(Growth) => Growth,
			_ => Gear,
		};

		Open(json, out _, out _);

		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void O_save_do_Lobby_abre_com_o_progresso_todo_vazio()
	{
		var progress = Open(Lobby, out var gear, out _);

		Assert.False(progress.Levels.EpicDone("p", new LevelKey("matematica", 1)));
		Assert.Null(progress.Explored.Get("p", new FloorKey("matematica", 1, 1)));
		Assert.Equal(0, progress.Mastery.Of("p", "matematica"));
		Assert.Equal(0, progress.Wallet.Balance("p"));
		Assert.Empty(progress.Rewards.Cosmetics("p"));
		Assert.Equal(new Equipment.Gear(), gear.Get("p", "matematica"));
	}

	[Fact]
	public void O_save_das_Dungeons_traz_o_que_foi_conquistado()
	{
		var progress = Open(Dungeons, out _, out _);

		Assert.True(progress.Levels.EpicDone("p", new LevelKey("matematica", 1)));
		Assert.True(progress.Levels.HasItem("p", new LevelKey("matematica", 1)));
		Assert.True(progress.Levels.ShortcutTried("p", new LevelKey("portugues", 1)));
		Assert.Equal(2, progress.Levels.HighestFloor("p", new LevelKey("matematica", 1)));
		Assert.Equal(new FloorKey("matematica", 1, 2), progress.Levels.LastFastTravel("p", new LevelKey("matematica", 1)));
	}

	[Fact]
	public void O_save_da_exploracao_e_o_do_crescimento_trazem_o_que_foi_gravado()
	{
		var explored = Open(Exploration, out _, out _);
		Assert.Equal(new[] { 0, 1 }, explored.Explored.Get("p", new FloorKey("matematica", 1, 1), version: 1)!.Visited.ToArray());
		Assert.True(explored.Rewards.IsFloorDone("p", new FloorKey("matematica", 1, 1)));
		Assert.Equal(new[] { "matematica-1-1" }, explored.Rewards.Cosmetics("p").ToArray());

		var grown = Open(Growth, out _, out _);
		Assert.Equal(1, grown.Mastery.Of("p", "matematica"));
		Assert.Equal(2, grown.Mastery.Solves("p", new ConceptKey("matematica", "adicao-ate-10")));
		Assert.Equal(12, grown.Wallet.Balance("p"));

		Open(Gear, out var gear, out _);
		Assert.Equal("scout", gear.Get("p", "matematica").Armor);
	}

	[Fact]
	public void Os_servicos_do_progresso_enxergam_as_mudancas_uns_dos_outros_no_mesmo_save()
	{
		var progress = Open(Lobby, out _, out _);

		progress.Wallet.Add("p", 4);
		progress.Mastery.Record("p", new ConceptKey("matematica", "x"), 1);
		progress.Levels.GiveItem("p", new LevelKey("matematica", 1));

		Assert.Equal(4, progress.Wallet.Balance("p"));
		Assert.Equal(1, progress.Mastery.Of("p", "matematica"));
		Assert.True(progress.Levels.HasItem("p", new LevelKey("matematica", 1)));
	}

	[Fact]
	public void Save_de_versao_mais_nova_nao_e_apagado_e_a_cadeia_de_migracoes_leva_a_versao_antiga_a_atual()
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(PathName, """{"version":99,"profiles":[]}""");
		var newer = new SaveStore(PathName).Load();
		Assert.Equal(SaveNotice.NewerVersion, newer.Value!.Notice);
		Assert.True(File.Exists(newer.Value.CopyPath));

		File.WriteAllText(PathName, """{"version":0,"profiles":[{"id":"p","nickname":"Luna","level":1}]}""");
		var migrations = new Dictionary<int, Func<System.Text.Json.Nodes.JsonObject, System.Text.Json.Nodes.JsonObject>>
		{
			[0] = root =>
			{
				root["version"] = 1;
				return root;
			},
		};
		var migrated = new SaveStore(PathName, migrations).Load();

		Assert.Equal(SaveNotice.Migrated, migrated.Value!.Notice);
		Assert.Equal("Luna", migrated.Value.Data.Profiles[0].Nickname);
	}

	[Theory]
	[InlineData("matematica:1:2", true)]
	[InlineData("portugues:3:4", true)]
	[InlineData("matematica:1", false)]
	[InlineData("matematica:a:2", false)]
	[InlineData("", false)]
	public void Chave_de_Andar_nasce_e_e_lida_so_pelo_tipo(string text, bool valid)
	{
		var parsed = FloorKey.TryParse(text);

		Assert.Equal(valid, parsed is not null);
		if (valid) Assert.Equal(text, parsed!.Value.ToString());
	}

	[Fact]
	public void As_chaves_gravam_o_mesmo_texto_que_os_saves_antigos_usam()
	{
		Assert.Equal("matematica:1", new LevelKey("matematica", 1).ToString());
		Assert.Equal("portugues:2:3", new FloorKey("portugues", 2, 3).ToString());
		Assert.Equal("matematica:adicao-ate-10", new ConceptKey("matematica", "adicao-ate-10").ToString());
		Assert.Equal(new LevelKey("matematica", 1), new FloorKey("matematica", 1, 4).Of);
	}
}
