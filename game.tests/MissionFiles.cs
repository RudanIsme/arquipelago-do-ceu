namespace ArquipelagoDoCeu.Tests;

// Onde ficam o schema e as Missões do repositório; os testes e a CI leem os mesmos arquivos do build.
static class MissionFiles
{
	public static string SchemaPath => Path.Combine(RepositoryRoot(), "content", "mission.schema.json");

	public static IEnumerable<(string Source, string Json)> All() =>
		Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "content", "missions"), "*.json", SearchOption.AllDirectories)
			.OrderBy(f => f, StringComparer.Ordinal)
			.Select(f => (Path.GetRelativePath(RepositoryRoot(), f).Replace('\\', '/'), File.ReadAllText(f)));

	public static string RepositoryRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
			dir = dir.Parent;
		return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
	}
}
