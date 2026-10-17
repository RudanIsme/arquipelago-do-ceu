using ArquipelagoDoCeu.Data;
using System.Text.Json;

namespace ArquipelagoDoCeu.Saves;

// A aparência procedural do Cosmético no Personagem: o tipo de peça e a cor. Sem `Look`, ele só aparece na lista.
public sealed record CosmeticLook(string Kind, string Color);

// Um Cosmético: só aparência, sem vantagem. Vem de uma recompensa da Dungeon, e nunca do treino no Lobby.
public sealed record Cosmetic(string Id, string Name, string Description, CosmeticLook? Look = null);

// Os Cosméticos do jogo, em dados. Quando e como se ganha cada um é do catálogo de recompensas.
public sealed class CosmeticCatalog
{
	readonly IReadOnlyList<Cosmetic> all;

	CosmeticCatalog(IReadOnlyList<Cosmetic> all) => this.all = all;

	// As peças que o Personagem sabe desenhar; um Cosmético novo escolhe uma delas e uma cor.
	public static readonly IReadOnlyList<string> LookKinds = ["bow", "backpack", "hat", "cap", "cape", "scarf", "tie", "wings"];

	public IReadOnlyList<Cosmetic> All => all;

	public Cosmetic? Find(string id) => all.FirstOrDefault(c => c.Id == id);

	public static CosmeticCatalog Empty => new([]);

	public static Result<CosmeticCatalog> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de Cosméticos");
		if (!read.IsOk) return Result<CosmeticCatalog>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.Cosmetics is not { Count: > 0 }) return Result<CosmeticCatalog>.Fail("O catálogo de Cosméticos não traz nenhum Cosmético.");

		var items = dto.Cosmetics.Select(c => new Cosmetic(c.Id ?? "", c.Name ?? "", c.Description ?? "", c.Look is null ? null : new CosmeticLook(c.Look.Kind ?? "", c.Look.Color ?? ""))).ToList();
		if (items.FirstOrDefault(c => c.Look is not null && !LookValid(c.Look)) is { } broken)
			return Result<CosmeticCatalog>.Fail($"O Cosmético {broken.Id} tem uma aparência inválida.");
		if (items.Any(c => c.Id.Length == 0 || c.Name.Length == 0))
			return Result<CosmeticCatalog>.Fail("Um Cosmético do catálogo não tem id ou nome.");
		if (CatalogJson.Duplicate(items, c => c.Id) is { } repeated)
			return Result<CosmeticCatalog>.Fail($"O catálogo de Cosméticos repete o id {repeated}.");
		return Result<CosmeticCatalog>.Ok(new CosmeticCatalog(items));
	}

	sealed record FileDto(List<CosmeticDto>? Cosmetics);

	static bool LookValid(CosmeticLook look) =>
		LookKinds.Contains(look.Kind) && look.Color is { Length: 7 } && look.Color[0] == '#' && look.Color.Skip(1).All(Uri.IsHexDigit);

	sealed record LookDto(string? Kind, string? Color);

	sealed record CosmeticDto(string? Id, string? Name, string? Description, LookDto? Look);
}
