using System.Globalization;
using ArquipelagoDoCeu.Data;

namespace ArquipelagoDoCeu.Combat;

// Uma moeda ou cédula do Real. O valor é em centavos; `Kind` é "moeda" ou "nota".
public sealed record Denomination(int Value, string Kind, string Name);

// O dinheiro do Real, em dados (game/data/dinheiro.json): moedas e cédulas, do menor valor ao maior.
public sealed class MoneyCatalog
{
	static readonly string[] Kinds = ["moeda", "nota"];

	MoneyCatalog(IReadOnlyList<Denomination> denominations) => Denominations = denominations;

	public IReadOnlyList<Denomination> Denominations { get; }

	public static MoneyCatalog Empty => new([]);

	public int Smallest => Denominations.Count == 0 ? 1 : Denominations[0].Value;

	// Escrito como o Real: "R$ 3,50".
	public static string Format(int cents) => $"R$ {(cents / 100).ToString(CultureInfo.InvariantCulture)},{(cents % 100).ToString("00", CultureInfo.InvariantCulture)}";

	// Dito como a criança fala: "2 reais e 50 centavos".
	public static string Spoken(int cents)
	{
		var reais = cents / 100;
		var centavos = cents % 100;
		var parts = new List<string>();
		if (reais > 0) parts.Add(reais == 1 ? "1 real" : $"{reais} reais");
		if (centavos > 0) parts.Add($"{centavos} centavos");
		return parts.Count == 0 ? "zero" : string.Join(" e ", parts);
	}

	public static Result<MoneyCatalog> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de dinheiro");
		if (!read.IsOk) return Result<MoneyCatalog>.Fail(read.Error!);
		if (read.Value!.Denominations is not { Count: > 0 } list) return Result<MoneyCatalog>.Fail("O catálogo de dinheiro não traz nenhuma moeda nem cédula.");

		var items = list.Select(d => new Denomination(d.Value, d.Kind ?? "", d.Name ?? "")).OrderBy(d => d.Value).ToList();
		if (items.Any(d => d.Value < 1 || d.Name.Length == 0)) return Result<MoneyCatalog>.Fail("Uma moeda ou cédula do catálogo está sem valor ou sem nome.");
		if (items.Any(d => !Kinds.Contains(d.Kind))) return Result<MoneyCatalog>.Fail("Uma moeda ou cédula do catálogo não é 'moeda' nem 'nota'.");
		return CatalogJson.Duplicate(items, d => d.Value.ToString(CultureInfo.InvariantCulture)) is { } repeated
			? Result<MoneyCatalog>.Fail($"O catálogo de dinheiro repete o valor {repeated}.")
			: Result<MoneyCatalog>.Ok(new MoneyCatalog(items));
	}

	sealed record FileDto(List<DenominationDto>? Denominations);

	sealed record DenominationDto(int Value, string? Kind, string? Name);
}
