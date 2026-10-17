using System.Text.RegularExpressions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class ArquiteturaTests
{
	// Só o compilador impede o vazamento do Godot para as regras (ADR 0001), e isso só vale
	// se core/ nunca referenciar o motor. Enquanto core/ não existe, o teste não encontra nada a violar.
	[Fact]
	public void Core_nao_referencia_o_Godot()
	{
		var core = Path.Combine(RaizDoRepositorio(), "core");
		var dependenciaDoMotor = new Regex(@"using\s+(static\s+)?(\w+\s*=\s*)?Godot\b|\bGodot\.[A-Z]|GodotSharp|Godot\.NET\.Sdk|ProjectReference[^>]*game[\\/]");

		var violacoes = ArquivosDeCodigo(core)
			.Where(f => dependenciaDoMotor.IsMatch(File.ReadAllText(f)))
			.ToList();

		Assert.Empty(violacoes);
	}

	const int LimiteDeLinhas = 400;

	// Arquivos que já passavam do limite quando o portão nasceu; não podem crescer, e saem daqui quando forem decompostos (#170).
	static readonly Dictionary<string, int> AindaGrandes = new()
	{
		["game/Dev/DungeonScenarios.cs"] = 800,
		["game/Dungeon/DungeonRun.cs"] = 730,
		["game/Dev/DevDriver.cs"] = 640,
		["game/Combat/TrainingArea.cs"] = 610,
		["game/Lobby/Lobby.cs"] = 520,
		["game/Main.cs"] = 480,
		["game/Ui/StaffPanel.cs"] = 450,
	};

	[Fact]
	public void Arquivo_de_codigo_nao_passa_do_limite_de_linhas_e_os_grandes_nao_crescem()
	{
		var raiz = RaizDoRepositorio();
		var tamanhos = new[] { "core", "game" }
			.SelectMany(p => ArquivosDeCodigo(Path.Combine(raiz, p)))
			.Where(f => f.EndsWith(".cs"))
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}"))
			.ToDictionary(f => Path.GetRelativePath(raiz, f).Replace(Path.DirectorySeparatorChar, '/'), f => File.ReadAllLines(f).Length);

		var acimaDoLimite = tamanhos.Where(t => t.Value > LimiteDeLinhas && !AindaGrandes.ContainsKey(t.Key)).Select(t => $"{t.Key}: {t.Value}");
		var cresceram = AindaGrandes.Where(g => tamanhos.TryGetValue(g.Key, out var linhas) && linhas > g.Value).Select(g => $"{g.Key}: {tamanhos[g.Key]} > {g.Value}");
		var jaCabem = AindaGrandes.Where(g => !tamanhos.TryGetValue(g.Key, out var linhas) || linhas <= LimiteDeLinhas).Select(g => g.Key);

		Assert.Empty(acimaDoLimite);
		Assert.Empty(cresceram);
		Assert.Empty(jaCabem);
	}

	[Fact]
	public void Os_testes_nao_dependem_do_projeto_do_Godot()
	{
		var projeto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "game.tests", "ArquipelagoDoCeu.Tests.csproj"));

		Assert.DoesNotContain("game" + Path.DirectorySeparatorChar + "ArquipelagoDoCeu.csproj", projeto.Replace('/', Path.DirectorySeparatorChar));
	}

	// Relógio e sorteio do sistema só entram por adaptadores; as regras recebem uma fonte por parâmetro (IRandomSource).
	static readonly Regex RelogioOuSorteio = new(@"\bTime\.GetTicks|\bDateTime\.(Now|UtcNow)|\bnew\s+Random\s*\(|\bRandom\.Shared|\bStopwatch\b|Environment\.TickCount|\.Rand(f|i)(Range)?\(|GD\.Rand");

	// Adaptadores e telas que medem tempo real do motor ou sorteiam só para efeito visual; o piloto (game/Dev) também.
	static readonly string[] UsamRelogio =
	[
		"game/GodotRandomSource.cs", "game/Voice/GodotSpeechEngine.cs", "game/Lobby/CameraController.cs", "game/Lobby/GuideIdle.cs",
		"game/Lobby/LobbyAmbience.cs", "game/Character/DynamicCape.cs", "game/Main.cs", "game/Main.PullTest.cs", "game/Dev/",
	];

	[Fact]
	public void Core_nao_le_o_relogio_nem_sorteia_por_conta_propria()
	{
		var raiz = RaizDoRepositorio();

		var violacoes = ArquivosDeCodigo(Path.Combine(raiz, "core")).Where(f => RelogioOuSorteio.IsMatch(File.ReadAllText(f)));

		Assert.Empty(violacoes);
	}

	[Fact]
	public void No_game_so_os_adaptadores_conhecidos_leem_o_relogio_ou_sorteiam()
	{
		var raiz = RaizDoRepositorio();

		var violacoes = ArquivosDeCodigo(Path.Combine(raiz, "game"))
			.Where(f => f.EndsWith(".cs") && !f.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}"))
			.Select(f => Path.GetRelativePath(raiz, f).Replace(Path.DirectorySeparatorChar, '/'))
			.Where(f => !UsamRelogio.Any(a => f.StartsWith(a, StringComparison.Ordinal)))
			.Where(f => RelogioOuSorteio.IsMatch(File.ReadAllText(Path.Combine(raiz, f))))
			.ToList();

		Assert.Empty(violacoes);
	}

	// Cada pasta de core/ só depende das pastas listadas aqui. Dependência nova tem de ser decidida e entrar na lista.
	static readonly Dictionary<string, string[]> DependenciasPermitidas = new()
	{
		["Combat"] = ["Generation", "Missions", "Rules"],
		["Data"] = [],
		["Dungeon"] = ["Combat", "Generation", "Missions"],
		["Equipment"] = ["Data", "Saves"],
		["Generation"] = ["Data", "Rules"],
		["Missions"] = ["Data"],
		["Profiles"] = ["Rules", "Saves"],
		["Rewards"] = ["Data", "Saves"],
		["Rules"] = ["Data"],
		["Saves"] = ["Data", "Dungeon", "Equipment"],
		["Settings"] = ["Rules", "Saves"],
		["Voice"] = ["Settings"],
		["World"] = ["Rules"],
	};

	[Fact]
	public void As_pastas_do_core_so_dependem_das_pastas_permitidas()
	{
		var atuais = DependenciasDoCore();

		var novas = atuais.SelectMany(p => p.Value.Where(d => !DependenciasPermitidas.GetValueOrDefault(p.Key, []).Contains(d)).Select(d => $"{p.Key} -> {d}"));

		Assert.Empty(novas);
	}

	// Equipment e Saves se enxergam hoje (o Perfil guarda o equipamento); é a única volta permitida, e sai no #169.
	[Fact]
	public void As_pastas_do_core_nao_formam_novas_voltas()
	{
		var grafo = DependenciasDoCore();
		bool Alcanca(string de, string para, HashSet<string> visitadas) =>
			grafo.GetValueOrDefault(de, []).Any(d => d == para || (visitadas.Add(d) && Alcanca(d, para, visitadas)));

		var voltas = grafo.Keys.Where(p => Alcanca(p, p, [])).Order().ToList();

		Assert.Equal(new[] { "Equipment", "Saves" }, voltas.ToArray());
	}

	static Dictionary<string, HashSet<string>> DependenciasDoCore()
	{
		var core = Path.Combine(RaizDoRepositorio(), "core");
		var usos = new Regex(@"using ArquipelagoDoCeu\.(\w+);|(?<!namespace )(?<!using )ArquipelagoDoCeu\.(\w+)\.[A-Z]");
		var grafo = new Dictionary<string, HashSet<string>>();
		foreach (var arquivo in ArquivosDeCodigo(core).Where(f => f.EndsWith(".cs")))
		{
			var pasta = Path.GetFileName(Path.GetDirectoryName(arquivo))!;
			if (Path.GetDirectoryName(arquivo) == core) continue;
			if (!grafo.ContainsKey(pasta)) grafo[pasta] = [];
			var dependencias = grafo[pasta];
			foreach (Match uso in usos.Matches(File.ReadAllText(arquivo)))
			{
				var alvo = uso.Groups[1].Success ? uso.Groups[1].Value : uso.Groups[2].Value;
				if (alvo != pasta) dependencias.Add(alvo);
			}
		}
		return grafo;
	}

	// bin/ e obj/ trazem arquivos gerados que não são código nosso.
	static IEnumerable<string> ArquivosDeCodigo(string pasta) =>
		!Directory.Exists(pasta)
			? []
			: Directory.EnumerateFiles(pasta, "*.*", SearchOption.AllDirectories)
				.Where(f => f.EndsWith(".cs") || f.EndsWith(".csproj"))
				.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
					&& !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

	// .git pode ser pasta ou arquivo (worktree), por isso os dois testes.
	static string RaizDoRepositorio()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
			dir = dir.Parent;
		return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
	}
}
