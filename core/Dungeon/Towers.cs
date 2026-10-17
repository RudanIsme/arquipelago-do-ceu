using ArquipelagoDoCeu.Data;

namespace ArquipelagoDoCeu.Dungeon;

// Uma torre (Dungeon), em dados: o id é também a disciplina das Missões e a chave do save. `ExchangeWith` é a torre que dá o
// Item de troca que abre o Marco desta.
public sealed record Tower(string Id, string Name, string Role, string Subject, IReadOnlyList<string> Mechanics, int Levels, string ExchangeWith);

// As torres do jogo (game/data/torres.json). Uma torre nova é uma entrada nos dados; a Mecânica nova é que pede código.
public sealed class TowerCatalog
{
	TowerCatalog(IReadOnlyList<Tower> towers) => All = towers;

	public IReadOnlyList<Tower> All { get; }

	// O último Nível do jogo é o da torre que mais tem.
	public int MaxLevel => All.Count == 0 ? 1 : All.Max(t => t.Levels);

	public static TowerCatalog Empty => new([]);

	public Tower? Find(string id) => All.FirstOrDefault(t => t.Id == id);

	public Tower? Exchange(Tower tower) => Find(tower.ExchangeWith);

	// A torre cujo Marco abre com o Item desta.
	public Tower? Beneficiary(Tower tower) => All.FirstOrDefault(t => t.ExchangeWith == tower.Id);

	public static Result<TowerCatalog> Parse(string json, IEnumerable<string> knownMechanics)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O arquivo das torres");
		if (!read.IsOk) return Result<TowerCatalog>.Fail(read.Error!);
		if (read.Value!.Towers is not { Count: > 0 } list) return Result<TowerCatalog>.Fail("O arquivo das torres não traz nenhuma torre.");

		var towers = list.Select(t => new Tower(t.Id ?? "", t.Name ?? "", t.Role ?? "", t.Subject ?? "", t.Mechanics ?? [], t.Levels, t.ExchangeWith ?? "")).ToList();
		if (CatalogJson.Duplicate(towers, t => t.Id) is { } repeated) return Result<TowerCatalog>.Fail($"O arquivo das torres repete o id {repeated}.");

		var known = knownMechanics.ToHashSet();
		var problem = towers.Select(t => Problem(t, towers, known)).FirstOrDefault(p => p is not null);
		return problem is null ? Result<TowerCatalog>.Ok(new TowerCatalog(towers)) : Result<TowerCatalog>.Fail(problem);
	}

	static string? Problem(Tower tower, IReadOnlyList<Tower> all, HashSet<string> known)
	{
		if (tower.Id.Length == 0 || tower.Name.Length == 0 || tower.Role.Length == 0 || tower.Subject.Length == 0) return $"A torre {tower.Id} está sem id, nome, Papel ou assunto.";
		if (tower.Levels < 1) return $"A torre {tower.Id} precisa de ao menos 1 Nível.";
		if (tower.Mechanics.Count == 0) return $"A torre {tower.Id} não aceita nenhuma Mecânica.";
		if (tower.Mechanics.FirstOrDefault(m => !known.Contains(m)) is { } unknown) return $"A torre {tower.Id} usa a Mecânica {unknown}, que o jogo não conhece.";
		if (tower.ExchangeWith == tower.Id || all.All(t => t.Id != tower.ExchangeWith)) return $"A torre {tower.Id} troca Item com {tower.ExchangeWith}, que não é outra torre do arquivo.";
		return null;
	}

	sealed record FileDto(List<TowerDto>? Towers);

	sealed record TowerDto(string? Id, string? Name, string? Role, string? Subject, List<string>? Mechanics, int Levels, string? ExchangeWith);
}
