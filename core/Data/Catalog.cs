using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquipelagoDoCeu.Data;

// De onde vêm os arquivos de dados do jogo. O Godot é o adaptador (res://data); os testes leem a pasta do repositório.
public interface IDataSource
{
	Result<string> Read(string name);
}

// A leitura que todo catálogo em dados compartilha: o mesmo JSON, o mesmo aviso de erro e a mesma checagem de id repetido.
public static class CatalogJson
{
	static readonly JsonSerializerOptions Options = new()
	{
		PropertyNameCaseInsensitive = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
	};

	// `what` é o nome do catálogo na mensagem ("O catálogo de equipamentos não é um JSON válido...").
	public static Result<T> Deserialize<T>(string json, string what)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(json, Options) is { } value
				? Result<T>.Ok(value)
				: Result<T>.Fail($"{what} está vazio.");
		}
		catch (JsonException e)
		{
			return Result<T>.Fail($"{what} não é um JSON válido: {e.Message}");
		}
	}

	public static string? Duplicate<T>(IEnumerable<T> items, Func<T, string> key) =>
		items.GroupBy(key).FirstOrDefault(g => g.Count() > 1)?.Key;
}

// Lê, valida e guarda cada catálogo uma vez só. O que o jogo não pode viver sem é `Required`; um extra (Cosméticos, equipamento,
// nomes de Conceito) é `Optional`: se falhar, o jogo segue com o padrão e a falha é avisada uma vez.
public sealed class CatalogLoader(IDataSource source, Action<string> warn)
{
	readonly Dictionary<(string File, Type Kind), object> loaded = [];
	readonly HashSet<string> warned = [];

	// Só o que deu certo fica guardado: uma leitura que falhou é tentada de novo na próxima vez.
	public Result<T> Required<T>(string file, Func<string, Result<T>> parse)
	{
		if (loaded.TryGetValue((file, typeof(T)), out var cached)) return Result<T>.Ok((T)cached);

		var text = source.Read(file);
		var parsed = text.IsOk ? parse(text.Value!) : Result<T>.Fail(text.Error!);
		if (!parsed.IsOk) return Result<T>.Fail($"{file}: {parsed.Error}");
		loaded[(file, typeof(T))] = parsed.Value!;
		return parsed;
	}

	// A falha é avisada uma vez por arquivo, mesmo que o jogo peça o catálogo muitas vezes.
	public T Optional<T>(string file, Func<string, Result<T>> parse, T fallback)
	{
		var result = Required(file, parse);
		if (result.IsOk) return result.Value!;
		if (warned.Add(file)) warn(result.Error!);
		return fallback;
	}
}
