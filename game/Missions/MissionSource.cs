using System.Collections.Generic;
using ArquipelagoDoCeu.Missions;
using Godot;

namespace ArquipelagoDoCeu;

// Lê o schema e as Missões que vieram no build (res://content) e entrega só as válidas.
// As inválidas são ignoradas e aparecem no log; nada vindo delas é executado.
public static class MissionSource
{
	const string Root = "res://content";

	public static MissionLoad Load()
	{
		var schema = ReadText($"{Root}/mission.schema.json");
		if (schema is null) return Failed($"Não foi possível ler {Root}/mission.schema.json.");

		var loader = MissionLoader.Create(schema);
		if (!loader.IsOk) return Failed(loader.Error!);

		var files = new List<(string, string)>();
		foreach (var path in JsonFiles($"{Root}/missions"))
			if (ReadText(path) is { } text) files.Add((path, text));
			else GD.PushWarning($"Não foi possível ler a Missão {path}.");

		var load = loader.Value!.Load(files);
		foreach (var rejected in load.Rejected) GD.PushWarning($"Missão ignorada: {rejected.Reason}");
		GD.Print($"Missões carregadas: {load.Missions.Count} ({load.Rejected.Count} ignoradas)");
		return load;
	}

	static MissionLoad Failed(string reason)
	{
		GD.PushWarning(reason);
		return new MissionLoad([], [new RejectedMission(Root, reason)]);
	}

	static IEnumerable<string> JsonFiles(string folder)
	{
		using var dir = DirAccess.Open(folder);
		if (dir is null) yield break;
		foreach (var file in dir.GetFiles())
			if (file.EndsWith(".json")) yield return $"{folder}/{file}";
		foreach (var sub in dir.GetDirectories())
			foreach (var path in JsonFiles($"{folder}/{sub}")) yield return path;
	}

	static string? ReadText(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return file?.GetAsText();
	}
}
