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
