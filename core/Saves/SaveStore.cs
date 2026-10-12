using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArquipelagoDoCeu.Saves;

public enum SaveNotice
{
	None,
	Migrated,
	Recovered,
	NewerVersion,
}

// CopyPath é onde ficou o save original quando o jogo precisou guardá-lo (Migrated, Recovered, NewerVersion).
public sealed record LoadOutcome(SaveData Data, SaveNotice Notice, string? CopyPath = null);

// Usa só System.IO, sem Godot, para ser testado sem o motor; o Godot só informa o caminho.
// migrations leva a versão N para a N+1, indexada por N.
public sealed class SaveStore(string path, IReadOnlyDictionary<int, Func<JsonObject, JsonObject>>? migrations = null)
{
	const int MaxCopies = 1000;

	static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};

	string TempPath => path + ".tmp";

	// Grava num temporário e só então troca o arquivo: uma queda no meio deixa o save anterior intacto.
	public Result<SaveData> Write(SaveData data)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
			using (var file = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				JsonSerializer.Serialize(file, data, Options);
				file.Flush(flushToDisk: true);
			}
			File.Move(TempPath, path, overwrite: true);
			return Result<SaveData>.Ok(data);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return Result<SaveData>.Fail($"Não foi possível gravar o save: {e.Message}");
		}
	}

	// Falha só quando não dá para ler nem guardar uma cópia; nesse caso o jogo não pode gravar por cima.
	public Result<LoadOutcome> Load()
	{
		if (!File.Exists(path))
			return Result<LoadOutcome>.Ok(new LoadOutcome(SaveData.Empty, SaveNotice.None));

		string text;
		try
		{
			text = File.ReadAllText(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return Result<LoadOutcome>.Fail($"Não foi possível ler o save: {e.Message}");
		}

		var root = ParseObject(text);
		var version = root is null ? null : ReadVersion(root);
		if (root is null || version is null)
			return StartOver("corrompido", SaveNotice.Recovered);

		if (version > SaveData.CurrentVersion)
			return StartOver("versao-nova", SaveNotice.NewerVersion);

		var migrated = version < SaveData.CurrentVersion;
		if (migrated) root = Migrate(root, version.Value);

		var data = root is null ? null : ToSaveData(root);
		if (data is null)
			return StartOver("corrompido", SaveNotice.Recovered);

		if (!migrated)
			return Result<LoadOutcome>.Ok(new LoadOutcome(data, SaveNotice.None));

		// Gravar o formato novo por cima apagaria o antigo, então ele é guardado antes.
		var copy = Preserve("versao-antiga");
		return copy.IsOk
			? Result<LoadOutcome>.Ok(new LoadOutcome(data, SaveNotice.Migrated, copy.Value))
			: Result<LoadOutcome>.Fail(copy.Error!);
	}

	Result<LoadOutcome> StartOver(string label, SaveNotice notice)
	{
		var copy = Preserve(label);
		return copy.IsOk
			? Result<LoadOutcome>.Ok(new LoadOutcome(SaveData.Empty, notice, copy.Value))
			: Result<LoadOutcome>.Fail(copy.Error!);
	}

	// Nunca sobrescreve uma cópia anterior: cada recuperação ganha o próximo número livre.
	Result<string> Preserve(string label)
	{
		for (var n = 1; n <= MaxCopies; n++)
		{
			var destination = $"{path}.{label}-{n}";
			if (Path.Exists(destination)) continue;
			try
			{
				File.Copy(path, destination, overwrite: false);
				return Result<string>.Ok(destination);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return Result<string>.Fail($"Não foi possível guardar uma cópia do save: {e.Message}");
			}
		}
		return Result<string>.Fail("Não foi possível guardar uma cópia do save: já existem cópias demais.");
	}

	static JsonObject? ParseObject(string text)
	{
		try
		{
			return JsonNode.Parse(text) as JsonObject;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	static int? ReadVersion(JsonObject root) =>
		root["version"] is JsonValue value && value.TryGetValue<int>(out var version) ? version : null;

	// O passo de migração lê dado externo, então qualquer falha dele vira "save ilegível", e não queda do jogo.
	JsonObject? Migrate(JsonObject root, int version)
	{
		try
		{
			while (version < SaveData.CurrentVersion)
			{
				if (migrations is null || !migrations.TryGetValue(version, out var step)) return null;
				root = step(root);
				var next = ReadVersion(root);
				if (next is null || next <= version) return null;
				version = next.Value;
			}
			return root;
		}
		catch (Exception)
		{
			return null;
		}
	}

	static SaveData? ToSaveData(JsonObject root)
	{
		try
		{
			var data = root.Deserialize<SaveData>(Options);
			return data?.Profiles is null ? null : data;
		}
		catch (Exception e) when (e is JsonException or NotSupportedException)
		{
			return null;
		}
	}
}
