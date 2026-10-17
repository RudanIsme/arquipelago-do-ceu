using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class TowerTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-torres-" + Guid.NewGuid().ToString("N"));

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	static readonly IReadOnlyList<Mission> Missions =
		MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!.Load(MissionFiles.All()).Missions;

	static readonly TowerCatalog GameTowers = TowerCatalog.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "torres.json")), MissionLoader.KnownMechanics).Value!;

	static string Json(params string[] towers) => "{\"towers\":[" + string.Join(",", towers) + "]}";

	static string Tower(string id, string mechanics = "\"compor_numero\"", int levels = 3, string exchange = "other", string role = "Papel") =>
		"{\"id\":\"" + id + "\",\"name\":\"Torre " + id + "\",\"role\":\"" + role + "\",\"subject\":\"Assunto\",\"mechanics\":[" + mechanics + "],\"levels\":" + levels + ",\"exchangeWith\":\"" + exchange + "\"}";

	[Fact]
	public void As_duas_torres_do_jogo_vem_dos_dados_com_nome_e_Papel()
	{
		var math = GameTowers.Find("matematica")!;
		var letters = GameTowers.Find("portugues")!;

		Assert.Equal(2, GameTowers.All.Count);
		Assert.Equal("Torre dos Números", math.Name);
		Assert.Equal("Matemático", math.Role);
		Assert.Equal("Torre das Letras", letters.Name);
		Assert.Equal("Escriba", letters.Role);
		Assert.Equal(letters, GameTowers.Exchange(math));
		Assert.Equal(math, GameTowers.Exchange(letters));
		Assert.Equal(3, GameTowers.MaxLevel);
	}

	[Fact]
	public void Toda_Mecanica_que_o_jogo_sabe_executar_pertence_a_uma_torre()
	{
		var covered = GameTowers.All.SelectMany(t => t.Mechanics).ToHashSet();

		Assert.All(MissionLoader.KnownMechanics, m => Assert.Contains(m, covered));
	}

	[Fact]
	public void O_catalogo_recusa_json_quebrado_vazio_id_repetido_Mecanica_desconhecida_e_troca_invalida()
	{
		string[] known = ["compor_numero"];

		Assert.False(TowerCatalog.Parse("{", known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", exchange: "a"), Tower("a", exchange: "a")), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", "\"nao_existe\"", exchange: "b"), Tower("b", exchange: "a")), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", exchange: "c"), Tower("b", exchange: "a")), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", exchange: "a"), Tower("b", exchange: "a")), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", levels: 0, exchange: "b"), Tower("b", exchange: "a")), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", role: "", exchange: "b"), Tower("b", exchange: "a")), known).IsOk);
		Assert.False(TowerCatalog.Parse(Json(Tower("a", "", exchange: "b"), Tower("b", exchange: "a")), known).IsOk);
		Assert.True(TowerCatalog.Parse(Json(Tower("a", exchange: "b"), Tower("b", exchange: "a")), known).IsOk);
	}

	static readonly TowerCatalog Three = TowerCatalog.Parse(
		Json(Tower("matematica", exchange: "portugues"), Tower("portugues", exchange: "ciencias"), Tower("ciencias", levels: 5, exchange: "matematica")),
		MissionLoader.KnownMechanics).Value!;

	[Fact]
	public void Uma_torre_nova_entra_so_com_dados_e_muda_o_ultimo_Nivel()
	{
		Assert.Equal(new[] { "matematica", "portugues", "ciencias" }, Three.All.Select(t => t.Id).ToArray());
		Assert.Equal(5, Three.MaxLevel);
		Assert.Equal(new[] { 1, 2, 3, 4, 5 }, TrainingSetup.Levels(9, Three.MaxLevel).ToArray());
		Assert.Equal(new[] { 1, 2 }, TrainingSetup.Levels(2, Three.MaxLevel).ToArray());
		Assert.Equal(new[] { 1, 2, 3 }, TrainingSetup.Levels(9, GameTowers.MaxLevel).ToArray());
		Assert.Equal(new[] { 1 }, TrainingSetup.Levels(0, GameTowers.MaxLevel).ToArray());
	}

	[Fact]
	public void O_Desafio_mestre_mistura_todas_as_torres_do_catalogo()
	{
		var extra = Missions.Where(m => m.Discipline == "matematica").Select(m => m with { Discipline = "ciencias", Id = "c-" + m.Id }).ToList();

		var setups = DungeonCombat.MasterSetups([.. Missions, .. extra], Three.All.Select(t => t.Id).ToList(), 1, 6, new Pcg32(3)).Value!;

		Assert.Equal(6, setups.Count);
		Assert.Equal(new[] { "ciencias", "matematica", "portugues" }, setups.Select(s => s.Discipline).Distinct().Order().ToArray());
	}

	[Fact]
	public void O_Desafio_mestre_so_abre_com_o_epico_de_todas_as_torres()
	{
		Directory.CreateDirectory(folder);
		var session = new SaveSession(new SaveStore(Path.Combine(folder, "save.json")), new SaveData(1, [new ProfileSave("p", "Luna", 1)]));
		var levels = ProgressServices.Create(session).Levels;
		string[] all = ["matematica", "portugues", "ciencias"];

		levels.MarkEpicDone("p", new LevelKey("matematica", 1));
		levels.MarkEpicDone("p", new LevelKey("portugues", 1));
		Assert.True(levels.MasterOpen("p", 1, all.Take(2)));
		Assert.False(levels.MasterOpen("p", 1, all));

		levels.MarkEpicDone("p", new LevelKey("ciencias", 1));
		Assert.True(levels.MasterOpen("p", 1, all));
		Assert.False(levels.MasterOpen("p", 2, all));
	}

	// Disciplina e último Nível vêm das torres em dados: o core não guarda nenhuma delas como constante.
	[Fact]
	public void O_core_nao_guarda_disciplina_nem_ultimo_Nivel_como_constante()
	{
		var core = Path.Combine(MissionFiles.RepositoryRoot(), "core");
		var literal = new System.Text.RegularExpressions.Regex("\"(matematica|portugues)\"|\\bLastLevel\\b|\\bTrainingSetup\\.(Mathematics|Portuguese)\\b|\\bMathematics\\s*=|\\bPortuguese\\s*=");

		var violations = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
			.Where(f => literal.IsMatch(File.ReadAllText(f)))
			.Select(f => Path.GetRelativePath(core, f));

		Assert.Empty(violations);
	}
}
