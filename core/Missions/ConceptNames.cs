using System.Text.Json;

namespace ArquipelagoDoCeu.Missions;

// Os nomes dos Conceitos para a criança ler ("adicao-ate-10" vira "Adição até 10"). Vêm de dados: o jogo nunca inventa o objetivo.
public sealed class ConceptNames
{
	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	readonly IReadOnlyDictionary<string, string> names;

	ConceptNames(IReadOnlyDictionary<string, string> names) => this.names = names;

	public static ConceptNames Empty => new(new Dictionary<string, string>());

	public IReadOnlyCollection<string> Ids => names.Keys.ToList();

	// Sem nome em dados, o id vira texto legível, para a placa nunca ficar em branco.
	public string NameOf(string concept)
	{
		if (names.TryGetValue(concept, out var name)) return name;
		var plain = concept.Replace('-', ' ');
		return plain.Length == 0 ? plain : char.ToUpperInvariant(plain[0]) + plain[1..];
	}

	public static Result<ConceptNames> Parse(string json)
	{
		FileDto? dto;
		try
		{
			dto = JsonSerializer.Deserialize<FileDto>(json, Options);
		}
		catch (JsonException e)
		{
			return Result<ConceptNames>.Fail($"O arquivo dos nomes dos Conceitos não é um JSON válido: {e.Message}");
		}
		if (dto?.Concepts is not { Count: > 0 }) return Result<ConceptNames>.Fail("O arquivo dos nomes dos Conceitos não traz nenhum Conceito.");
		if (dto.Concepts.Any(p => string.IsNullOrWhiteSpace(p.Value))) return Result<ConceptNames>.Fail("Um Conceito do arquivo de nomes está sem nome.");
		return Result<ConceptNames>.Ok(new ConceptNames(dto.Concepts));
	}

	sealed record FileDto(Dictionary<string, string>? Concepts);
}
