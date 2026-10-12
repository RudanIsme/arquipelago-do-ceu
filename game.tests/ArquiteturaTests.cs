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
