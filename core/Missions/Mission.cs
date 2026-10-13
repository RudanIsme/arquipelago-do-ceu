using System.Text.Json;
using Json.Schema;

namespace ArquipelagoDoCeu.Missions;

public sealed record Review(string Status, string? By);

public sealed record Accessibility(bool TimerAllowed);

// Os parâmetros ficam como dados; quem os lê é a Mecânica escolhida em `Mechanic`.
public sealed record Mission(
	string Id,
	int Version,
	string Discipline,
	string Concept,
	IReadOnlyList<string> Bncc,
	int Year,
	bool Stretch,
	int Difficulty,
	string Mechanic,
	IReadOnlyDictionary<string, JsonElement> Params,
	string Prompt,
	IReadOnlyList<string> Hints,
	string Explanation,
	Accessibility Accessibility,
	Review Review);

// Uma Missão ignorada: a origem (arquivo) e o que está errado, para o log e para a CI.
public sealed record RejectedMission(string Source, string Reason);

public sealed record MissionLoad(IReadOnlyList<Mission> Missions, IReadOnlyList<RejectedMission> Rejected);

// Validação na borda: só vira Mission o JSON que passa no schema e cuja Mecânica o jogo conhece.
// Nada aqui executa conteúdo; o JSON só escolhe uma Mecânica e passa parâmetros.
public sealed class MissionLoader
{
	// O schema aceita as Mecânicas planejadas; esta lista é a das que o jogo sabe executar.
	public static readonly IReadOnlyList<string> KnownMechanics = ["compor_numero"];

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	readonly JsonSchema schema;

	MissionLoader(JsonSchema schema) => this.schema = schema;

	public static Result<MissionLoader> Create(string schemaJson)
	{
		try
		{
			return Result<MissionLoader>.Ok(new MissionLoader(JsonSchema.FromText(schemaJson, new BuildOptions { SchemaRegistry = new SchemaRegistry() })));
		}
		catch (Exception e)
		{
			return Result<MissionLoader>.Fail($"O schema de Missão não é válido: {e.Message}");
		}
	}

	// `source` é o nome do arquivo; vai nas mensagens de erro.
	public Result<Mission> Parse(string source, string json)
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException e)
		{
			return Result<Mission>.Fail($"{source}: não é um JSON válido ({e.Message})");
		}

		using var _ = document;
		var errors = SchemaErrors(document.RootElement);
		if (errors.Count > 0) return Result<Mission>.Fail($"{source}: {string.Join("; ", errors)}");

		Mission mission;
		try
		{
			mission = document.RootElement.Deserialize<Mission>(Options)!;
		}
		catch (JsonException e)
		{
			return Result<Mission>.Fail($"{source}: a Missão não tem o formato esperado ({e.Message})");
		}

		return KnownMechanics.Contains(mission.Mechanic)
			? Result<Mission>.Ok(mission)
			: Result<Mission>.Fail($"{source}: /mechanic: a Mecânica \"{mission.Mechanic}\" ainda não existe no jogo");
	}

	public MissionLoad Load(IEnumerable<(string Source, string Json)> files)
	{
		var missions = new List<Mission>();
		var rejected = new List<RejectedMission>();
		foreach (var (source, json) in files)
		{
			var parsed = Parse(source, json);
			if (parsed.IsOk) missions.Add(parsed.Value!);
			else rejected.Add(new RejectedMission(source, parsed.Error!));
		}
		return new MissionLoad(missions, rejected);
	}

	List<string> SchemaErrors(JsonElement root)
	{
		var result = schema.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.List });
		if (result.IsValid) return [];

		return (result.Details ?? [])
			.Where(d => d.Errors is { Count: > 0 })
			.SelectMany(d => d.Errors!.Select(e => $"{Location(d)}: {e.Value}"))
			.Distinct()
			.ToList();
	}

	static string Location(EvaluationResults details)
	{
		var pointer = details.InstanceLocation.ToString();
		return pointer.Length > 0 ? pointer : "/";
	}
}
