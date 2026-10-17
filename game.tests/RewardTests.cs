using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class RewardTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-recompensas-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");

	static SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	static readonly CosmeticCatalog Cosmetics = CosmeticCatalog.Parse("""
		{"cosmetics":[
			{"id":"laco","name":"Lacinho","description":"Enfeite"},
			{"id":"mochila","name":"Mochila","description":"Enfeite"},
			{"id":"chapeu","name":"Chapéu","description":"Enfeite"}]}
		""").Value!;

	const string Rewards = """
		{"rewards":[
			{"id":"moedas-criatura","trigger":"creature-calmed","coins":1},
			{"id":"moedas-item","trigger":"item-room","coins":3},
			{"id":"moedas-andar","trigger":"floor-complete","coins":5},
			{"id":"moedas-andar-livre","trigger":"free-floor-complete","coins":5},
			{"id":"laco-andar-1","trigger":"floor-complete","cosmetic":"laco","when":{"discipline":"matematica","level":1,"floor":1}},
			{"id":"mochila-andar-1","trigger":"floor-complete","cosmetic":"mochila","when":{"discipline":"matematica","level":1,"floor":1}},
			{"id":"chapeu-andar-2","trigger":"floor-complete","cosmetic":"chapeu","when":{"discipline":"matematica","level":1,"floor":2}}]}
		""";

	static readonly RewardCatalog Catalog = RewardCatalog.Parse(Rewards, RewardTriggers.Default, Cosmetics).Value!;

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	(ProgressServices Progress, RewardService Rewards, SaveStore Store) Open()
	{
		var store = new SaveStore(PathName);
		var session = new SaveSession(store, Example);
		var progress = ProgressServices.Create(session);
		return (progress, new RewardService(new ProgressStore(session)), store);
	}

	static RewardContext Floor(int floor, string trigger = "floor-complete") => new(trigger, "matematica", 1, floor);

	[Fact]
	public void O_catalogo_de_Cosmeticos_recusa_json_quebrado_vazio_e_id_repetido()
	{
		Assert.False(CosmeticCatalog.Parse("{").IsOk);
		Assert.False(CosmeticCatalog.Parse("""{"cosmetics":[]}""").IsOk);
		Assert.False(CosmeticCatalog.Parse("""{"cosmetics":[{"id":"a","name":""}]}""").IsOk);
		Assert.False(CosmeticCatalog.Parse("""{"cosmetics":[{"id":"a","name":"A"},{"id":"a","name":"B"}]}""").IsOk);
		Assert.Equal("Lacinho", Cosmetics.Find("laco")!.Name);
	}

	[Fact]
	public void Cada_criatura_acalmada_paga_de_novo()
	{
		var (progress, rewards, _) = Open();
		var creature = new RewardContext("creature-calmed", "matematica", 1);

		var first = rewards.Grant("p", creature, Catalog).Value!;
		var second = rewards.Grant("p", creature, Catalog).Value!;

		Assert.True(first.Fired && second.Fired);
		Assert.Equal(1, first.Coins);
		Assert.Equal(2, progress.Wallet.Balance("p"));
	}

	[Fact]
	public void A_Sala_do_Item_paga_uma_vez_so_por_Nivel_e_torre()
	{
		var (progress, rewards, _) = Open();
		var item = new RewardContext("item-room", "matematica", 1, 2);

		Assert.Equal(3, rewards.Grant("p", item, Catalog).Value!.Coins);
		var again = rewards.Grant("p", item, Catalog).Value!;

		Assert.False(again.Fired);
		Assert.Equal(0, again.Coins);
		Assert.Equal(3, progress.Wallet.Balance("p"));
		Assert.Equal(3, rewards.Grant("p", item with { Discipline = "portugues" }, Catalog).Value!.Coins);
	}

	[Fact]
	public void Andar_completo_paga_moedas_e_o_Cosmetico_dele_uma_so_vez_mesmo_recomecando()
	{
		var (progress, rewards, store) = Open();

		var first = rewards.Grant("p", Floor(2), Catalog).Value!;
		var again = rewards.Grant("p", Floor(2), Catalog).Value!;

		Assert.True(first.Fired);
		Assert.Equal(5, first.Coins);
		Assert.Equal(new[] { "chapeu" }, first.Cosmetics.Select(c => c.Id).ToArray());
		Assert.False(again.Fired);

		var reopenedSession = new SaveSession(store, store.Load().Value!.Data);
		var reopened = new RewardService(new ProgressStore(reopenedSession));
		Assert.False(reopened.Grant("p", Floor(2), Catalog).Value!.Fired);
		Assert.True(ProgressServices.Create(reopenedSession).Explored.IsFloorDone("p", new FloorKey("matematica", 1, 2)));
		Assert.Equal(5, progress.Wallet.Balance("p"));
	}

	[Fact]
	public void Dois_Cosmeticos_no_mesmo_gatilho_saem_juntos_so_com_dados()
	{
		var (_, rewards, _) = Open();

		var done = rewards.Grant("p", Floor(1), Catalog).Value!;

		Assert.Equal(new[] { "laco", "mochila" }, done.Cosmetics.Select(c => c.Id).Order().ToArray());
		Assert.Equal(new[] { "laco", "mochila" }, rewards.Cosmetics("p").Order().ToArray());
	}

	[Fact]
	public void O_filtro_do_gatilho_deixa_cada_Andar_com_o_seu_Cosmetico()
	{
		var (_, rewards, _) = Open();

		var third = rewards.Grant("p", Floor(3), Catalog).Value!;

		Assert.True(third.Fired);
		Assert.Empty(third.Cosmetics);
		Assert.Equal(5, third.Coins);
	}

	[Fact]
	public void Save_antigo_com_Andar_completo_nao_repete_o_premio()
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(PathName, """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"floorsDone":["matematica:1:1"],"cosmetics":["laco"]}}]}""");
		var store = new SaveStore(PathName);
		var session = new SaveSession(store, store.Load().Value!.Data);
		var rewards = new RewardService(new ProgressStore(session));

		var done = rewards.Grant("p", Floor(1), Catalog).Value!;

		Assert.False(done.Fired);
		Assert.Equal(new[] { "laco" }, rewards.Cosmetics("p").ToArray());
	}

	[Fact]
	public void Na_Expedicao_livre_o_Andar_paga_moedas_sem_registrar_Andar_completo_nem_Cosmetico()
	{
		var (progress, rewards, _) = Open();

		var free = rewards.Grant("p", Floor(1, "free-floor-complete"), Catalog).Value!;
		var again = rewards.Grant("p", Floor(1, "free-floor-complete"), Catalog).Value!;

		Assert.Equal(5, free.Coins);
		Assert.Equal(5, again.Coins);
		Assert.Empty(free.Cosmetics);
		Assert.False(progress.Explored.IsFloorDone("p", new FloorKey("matematica", 1, 1)));
		Assert.Empty(rewards.Cosmetics("p"));
	}

	[Fact]
	public void Se_o_save_falha_nada_vale_e_o_premio_continua_a_ganhar()
	{
		var blocker = Path.Combine(folder, "arquivo");
		Directory.CreateDirectory(folder);
		File.WriteAllText(blocker, "x");
		var session = new SaveSession(new SaveStore(Path.Combine(blocker, "save.json")), Example);
		var rewards = new RewardService(new ProgressStore(session));

		Assert.False(rewards.Grant("p", Floor(1), Catalog).IsOk);
		Assert.Empty(rewards.Cosmetics("p"));
		Assert.False(ProgressServices.Create(session).Explored.IsFloorDone("p", new FloorKey("matematica", 1, 1)));
	}

	[Fact]
	public void Sem_recompensas_carregadas_o_Andar_fica_por_registrar()
	{
		var (progress, rewards, _) = Open();

		var done = rewards.Grant("p", Floor(1), RewardCatalog.Empty).Value!;

		Assert.False(done.Fired);
		Assert.False(progress.Explored.IsFloorDone("p", new FloorKey("matematica", 1, 1)));
	}

	[Fact]
	public void Perfil_inexistente_falha()
	{
		var (_, rewards, _) = Open();

		Assert.False(rewards.Grant("nao-existe", Floor(1), Catalog).IsOk);
	}

	// Um gatilho novo é uma linha com a regra de "uma vez": aqui, a porta secreta de cada Andar.
	[Fact]
	public void Um_gatilho_novo_entra_com_uma_linha_no_registro_e_a_recompensa_com_dados()
	{
		var triggers = RewardTriggers.Default.With(new OnceTrigger("secret-door", c => $"{c.Discipline}:{c.Level}:{c.Floor}"));
		var catalog = RewardCatalog.Parse("""{"rewards":[{"id":"porta","trigger":"secret-door","coins":7}]}""", triggers, Cosmetics).Value!;
		var (progress, rewards, _) = Open();
		var door = new RewardContext("secret-door", "matematica", 1, 4);

		Assert.Equal(7, rewards.Grant("p", door, catalog).Value!.Coins);
		Assert.False(rewards.Grant("p", door, catalog).Value!.Fired);
		Assert.Equal(7, rewards.Grant("p", door with { Floor = 5 }, catalog).Value!.Coins);
		Assert.Equal(14, progress.Wallet.Balance("p"));
	}

	[Fact]
	public void O_catalogo_de_recompensas_recusa_gatilho_Cosmetico_e_valores_que_nao_existem()
	{
		RewardCatalog Parse(string reward) => RewardCatalog.Parse("{\"rewards\":[" + reward + "]}", RewardTriggers.Default, Cosmetics) is { IsOk: true } ok ? ok.Value! : null!;

		Assert.Null(Parse("""{"id":"a","trigger":"nao-existe","coins":1}"""));
		Assert.Null(Parse("""{"id":"a","trigger":"floor-complete","cosmetic":"nao-existe"}"""));
		Assert.Null(Parse("""{"id":"a","trigger":"floor-complete","coins":0}"""));
		Assert.Null(Parse("""{"id":"a","trigger":"floor-complete","coins":2,"cosmetic":"laco"}"""));
		Assert.Null(Parse("""{"id":"","trigger":"floor-complete","coins":1}"""));
		Assert.Null(Parse("""{"id":"a","trigger":"floor-complete","coins":1001}"""));
		Assert.Null(Parse("""{"id":"a","trigger":"floor-complete","coins":1},{"id":"a","trigger":"item-room","coins":1}"""));
		Assert.False(RewardCatalog.Parse("{", RewardTriggers.Default, Cosmetics).IsOk);
		Assert.False(RewardCatalog.Parse("""{"rewards":[]}""", RewardTriggers.Default, Cosmetics).IsOk);
		Assert.NotNull(Parse("""{"id":"a","trigger":"floor-complete","coins":2}"""));
	}

	[Fact]
	public void As_recompensas_do_jogo_cobrem_os_Cosmeticos_e_as_moedas_dos_dados()
	{
		var data = Path.Combine(MissionFiles.RepositoryRoot(), "game", "data");
		var cosmetics = CosmeticCatalog.Parse(File.ReadAllText(Path.Combine(data, "cosmeticos.json"))).Value!;
		var catalog = RewardCatalog.Parse(File.ReadAllText(Path.Combine(data, "recompensas.json")), RewardTriggers.Default, cosmetics).Value!;

		foreach (var discipline in new[] { "matematica", "portugues" })
			foreach (var level in new[] { 1, 2, 3 })
				for (var floor = 1; floor <= ExpeditionFixtures.Rules.FloorsMax; floor++)
					Assert.Single(catalog.For(new RewardContext("floor-complete", discipline, level, floor)), r => r.Cosmetic is not null);

		Assert.All(new[] { "creature-calmed", "item-room", "floor-complete", "free-floor-complete" },
			trigger => Assert.Contains(catalog.For(new RewardContext(trigger, "matematica", 1, 1)), r => r.Coins > 0));
	}

	// O treino do Lobby não conta progresso: só a Dungeon e o Desafio mestre pedem recompensa. O piloto de depuração (game/Dev) fica de fora.
	[Fact]
	public void So_a_Dungeon_e_o_Desafio_mestre_pedem_recompensa()
	{
		var game = Path.Combine(MissionFiles.RepositoryRoot(), "game");
		var callers = Directory.EnumerateFiles(game, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "Dev" + Path.DirectorySeparatorChar))
			.Where(f => File.ReadAllText(f).Contains(".Grant("))
			.Select(f => Path.GetFileName(f)).Order().ToList();

		Assert.Equal(new[] { "DungeonRewards.cs", "MasterChallenge.cs" }, callers.ToArray());
	}
}
