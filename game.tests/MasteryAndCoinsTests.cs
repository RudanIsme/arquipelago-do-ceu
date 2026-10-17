using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class MasteryAndCoinsTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-maestria-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");
	SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	ProgressServices NewProgress(SaveStore store) => ProgressServices.Create(new SaveSession(store, Example));

	[Fact]
	public void A_Maestria_comeca_em_zero_e_sobe_quando_um_Conceito_e_dominado()
	{
		var progress = NewProgress(new SaveStore(PathName));
		Assert.Equal(0, progress.Mastery.Of("p", "matematica"));

		var first = progress.Mastery.Record("p", new ConceptKey("matematica", "adicao-ate-10"), solvesToMaster: 2).Value!;
		var second = progress.Mastery.Record("p", new ConceptKey("matematica", "adicao-ate-10"), solvesToMaster: 2).Value!;

		Assert.False(first.NewlyMastered);
		Assert.Equal(0, first.Mastery);
		Assert.True(second.NewlyMastered);
		Assert.Equal(1, second.Mastery);
		Assert.Equal(1, progress.Mastery.Of("p", "matematica"));
	}

	[Fact]
	public void Um_Conceito_dominado_conta_uma_vez_so_e_cada_Papel_tem_a_sua_Maestria()
	{
		var progress = NewProgress(new SaveStore(PathName));
		for (var i = 0; i < 5; i++) progress.Mastery.Record("p", new ConceptKey("matematica", "adicao-ate-10"), solvesToMaster: 2);
		progress.Mastery.Record("p", new ConceptKey("matematica", "composicao-ate-5"), solvesToMaster: 1);

		Assert.Equal(2, progress.Mastery.Of("p", "matematica"));
		Assert.Equal(0, progress.Mastery.Of("p", "portugues"));
		Assert.Equal(0, progress.Mastery.Of("q", "matematica"));
	}

	[Fact]
	public void A_Maestria_nunca_cai_nem_quando_a_regra_pede_mais_vitorias_depois()
	{
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);
		progress.Mastery.Record("p", new ConceptKey("portugues", "adjetivo"), solvesToMaster: 1);

		progress.Mastery.Record("p", new ConceptKey("portugues", "adjetivo"), solvesToMaster: 10);
		var reopened = ProgressServices.Create(new SaveSession(store, store.Load().Value!.Data));

		Assert.Equal(1, reopened.Mastery.Of("p", "portugues"));
	}

	[Fact]
	public void As_vitorias_contadas_sobrevivem_a_gravar_e_reabrir()
	{
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);
		progress.Mastery.Record("p", new ConceptKey("matematica", "adicao-ate-10"), solvesToMaster: 3);
		progress.Mastery.Record("p", new ConceptKey("matematica", "adicao-ate-10"), solvesToMaster: 3);

		var reopened = ProgressServices.Create(new SaveSession(store, store.Load().Value!.Data));
		var third = reopened.Mastery.Record("p", new ConceptKey("matematica", "adicao-ate-10"), solvesToMaster: 3).Value!;

		Assert.True(third.NewlyMastered);
		Assert.Equal(1, reopened.Mastery.Of("p", "matematica"));
	}

	[Fact]
	public void Perfil_inexistente_falha_sem_gravar()
	{
		var progress = NewProgress(new SaveStore(PathName));

		Assert.False(progress.Mastery.Record("nao-existe", new ConceptKey("matematica", "x"), 1).IsOk);
		Assert.False(progress.Wallet.Add("nao-existe", 3).IsOk);
	}

	[Fact]
	public void As_moedas_somam_sem_teto_e_sobrevivem_a_gravar_e_reabrir()
	{
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);
		Assert.Equal(0, progress.Wallet.Balance("p"));

		Assert.True(progress.Wallet.Add("p", 5).IsOk);
		Assert.True(progress.Wallet.Add("p", 1_000_000).IsOk);

		var reopened = ProgressServices.Create(new SaveSession(store, store.Load().Value!.Data));
		Assert.Equal(1_000_005, reopened.Wallet.Balance("p"));
		Assert.Equal(0, reopened.Wallet.Balance("q"));
	}

	[Fact]
	public void Ganhar_zero_ou_menos_nao_grava_nada_e_nao_tira_moedas()
	{
		Directory.CreateDirectory(folder);
		var store = new SaveStore(PathName);
		var progress = NewProgress(store);
		progress.Wallet.Add("p", 4);
		var before = File.ReadAllText(PathName);

		Assert.True(progress.Wallet.Add("p", 0).IsOk);
		Assert.True(progress.Wallet.Add("p", -3).IsOk);

		Assert.Equal(4, progress.Wallet.Balance("p"));
		Assert.Equal(before, File.ReadAllText(PathName));
	}

	[Fact]
	public void Save_antigo_sem_Maestria_nem_moedas_abre_com_zero()
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(PathName, """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"progress":{"items":["matematica:1"]}}]}""");
		var store = new SaveStore(PathName);
		var progress = ProgressServices.Create(new SaveSession(store, store.Load().Value!.Data));

		Assert.Equal(0, progress.Mastery.Of("p", "matematica"));
		Assert.Equal(0, progress.Wallet.Balance("p"));
	}

	// O treino no Lobby não conta progresso: só a Expedição soma moedas e vitórias de Conceito. O piloto de depuração (game/Dev) fica de fora.
	[Fact]
	public void So_a_Dungeon_da_moedas_e_Maestria()
	{
		var game = Path.Combine(MissionFiles.RepositoryRoot(), "game");
		List<string> Callers(string call) => Directory.EnumerateFiles(game, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "Dev" + Path.DirectorySeparatorChar))
			.Where(f => File.ReadAllText(f).Contains(call))
			.Select(f => Path.GetFileName(f)).Order().ToList();

		Assert.Empty(Callers(".Wallet.Add("));
		Assert.Equal(new[] { "DungeonRewards.cs" }, Callers(".Mastery.Record(").ToArray());
	}
}
