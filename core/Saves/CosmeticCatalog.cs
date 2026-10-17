using ArquipelagoDoCeu.Data;
using System.Text.Json;

namespace ArquipelagoDoCeu.Saves;

// Um Cosmético: só aparência, sem vantagem. Vem de completar um Andar, e nunca do treino no Lobby.
public sealed record Cosmetic(string Id, string Name, string Description, string Discipline, int Level, int Floor);

// Os Cosméticos do jogo, em dados, e o Andar completo que rende cada um.
public sealed class CosmeticCatalog
{
	readonly IReadOnlyList<Cosmetic> all;

	CosmeticCatalog(IReadOnlyList<Cosmetic> all) => this.all = all;

	public IReadOnlyList<Cosmetic> All => all;

	public Cosmetic? For(string discipline, int level, int floor) =>
		all.FirstOrDefault(c => c.Discipline == discipline && c.Level == level && c.Floor == floor);

	public Cosmetic? Find(string id) => all.FirstOrDefault(c => c.Id == id);

	public static CosmeticCatalog Empty => new([]);

	public static Result<CosmeticCatalog> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de Cosméticos");
		if (!read.IsOk) return Result<CosmeticCatalog>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.Cosmetics is not { Count: > 0 }) return Result<CosmeticCatalog>.Fail("O catálogo de Cosméticos não traz nenhum Cosmético.");

		var items = dto.Cosmetics.Select(c => new Cosmetic(c.Id ?? "", c.Name ?? "", c.Description ?? "", c.Discipline ?? "", c.Level, c.Floor)).ToList();
		if (items.Any(c => c.Id.Length == 0 || c.Name.Length == 0 || c.Discipline.Length == 0 || c.Floor < 1 || c.Level < 1))
			return Result<CosmeticCatalog>.Fail("Um Cosmético do catálogo não tem id, nome, torre, Nível ou Andar.");
		if (CatalogJson.Duplicate(items, c => c.Id) is { } repeated)
			return Result<CosmeticCatalog>.Fail($"O catálogo de Cosméticos repete o id {repeated}.");
		if (CatalogJson.Duplicate(items, c => $"{c.Discipline}:{c.Level}:{c.Floor}") is { } sameFloor)
			return Result<CosmeticCatalog>.Fail($"Dois Cosméticos do catálogo estão ligados ao mesmo Andar ({sameFloor}).");
		return Result<CosmeticCatalog>.Ok(new CosmeticCatalog(items));
	}

	sealed record FileDto(List<CosmeticDto>? Cosmetics);

	sealed record CosmeticDto(string? Id, string? Name, string? Description, string? Discipline, int Level, int Floor);
}
