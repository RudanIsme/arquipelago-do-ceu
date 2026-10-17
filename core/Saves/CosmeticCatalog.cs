using ArquipelagoDoCeu.Data;
using System.Text.Json;

namespace ArquipelagoDoCeu.Saves;

// Um Cosmético: só aparência, sem vantagem. Vem de uma recompensa da Dungeon, e nunca do treino no Lobby.
public sealed record Cosmetic(string Id, string Name, string Description);

// Os Cosméticos do jogo, em dados. Quando e como se ganha cada um é do catálogo de recompensas.
public sealed class CosmeticCatalog
{
	readonly IReadOnlyList<Cosmetic> all;

	CosmeticCatalog(IReadOnlyList<Cosmetic> all) => this.all = all;

	public IReadOnlyList<Cosmetic> All => all;

	public Cosmetic? Find(string id) => all.FirstOrDefault(c => c.Id == id);

	public static CosmeticCatalog Empty => new([]);

	public static Result<CosmeticCatalog> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de Cosméticos");
		if (!read.IsOk) return Result<CosmeticCatalog>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.Cosmetics is not { Count: > 0 }) return Result<CosmeticCatalog>.Fail("O catálogo de Cosméticos não traz nenhum Cosmético.");

		var items = dto.Cosmetics.Select(c => new Cosmetic(c.Id ?? "", c.Name ?? "", c.Description ?? "")).ToList();
		if (items.Any(c => c.Id.Length == 0 || c.Name.Length == 0))
			return Result<CosmeticCatalog>.Fail("Um Cosmético do catálogo não tem id ou nome.");
		if (CatalogJson.Duplicate(items, c => c.Id) is { } repeated)
			return Result<CosmeticCatalog>.Fail($"O catálogo de Cosméticos repete o id {repeated}.");
		return Result<CosmeticCatalog>.Ok(new CosmeticCatalog(items));
	}

	sealed record FileDto(List<CosmeticDto>? Cosmetics);

	sealed record CosmeticDto(string? Id, string? Name, string? Description);
}
