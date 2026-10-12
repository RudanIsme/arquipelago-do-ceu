using System.Text.Json;

namespace ArquipelagoDoCeu.Rules;

public sealed record RoleNames(string Mathematics, string Portuguese);

// Todo valor de regra vem de dados (docs/tecnico), nunca fica fixo no código.
public sealed record GameRules(string GameName, RoleNames Roles)
{
	public static Result<GameRules> Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return Fail("O arquivo de regras está vazio.");

		Dto? dto;
		try
		{
			dto = JsonSerializer.Deserialize<Dto>(json, Options);
		}
		catch (JsonException e)
		{
			return Fail($"O arquivo de regras não é um JSON válido: {e.Message}");
		}
		if (dto is null)
			return Fail("O arquivo de regras não é um JSON válido.");

		if (Missing(dto.GameName)) return Fail(Required("gameName"));
		if (dto.Roles is null) return Fail(Required("roles"));
		if (Missing(dto.Roles.Mathematics)) return Fail(Required("roles.mathematics"));
		if (Missing(dto.Roles.Portuguese)) return Fail(Required("roles.portuguese"));

		return Result<GameRules>.Ok(new GameRules(
			dto.GameName!,
			new RoleNames(dto.Roles.Mathematics!, dto.Roles.Portuguese!)));
	}

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	static bool Missing(string? text) => string.IsNullOrWhiteSpace(text);

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";

	static Result<GameRules> Fail(string error) => Result<GameRules>.Fail(error);

	sealed record Dto(string? GameName, RolesDto? Roles);

	sealed record RolesDto(string? Mathematics, string? Portuguese);
}
