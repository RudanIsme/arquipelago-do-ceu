using System.Text.Json;

namespace ArquipelagoDoCeu.Saves;

// Usa só System.IO, sem Godot, para ser testado sem o motor; o Godot só informa o caminho.
public sealed class SaveStore(string path)
{
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

	public Result<SaveData> Load()
	{
		if (!File.Exists(path))
			return Result<SaveData>.Ok(SaveData.Empty);

		try
		{
			var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options);
			return data is null
				? Result<SaveData>.Fail("O save está vazio.")
				: Result<SaveData>.Ok(data);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
		{
			return Result<SaveData>.Fail($"Não foi possível ler o save: {e.Message}");
		}
	}
}
