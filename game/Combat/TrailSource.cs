using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// Lê as trilhas de traço que vieram no build (res://data) uma vez só. Sem elas a criança segue sem o desenho: o jogo nunca trava por isso.
public static class TrailSource
{
	static TraceTrails? shared;

	public static TraceTrails Shared => shared ??= Load();

	static TraceTrails Load()
	{
		var json = Read("res://data/trilhas.json");
		var schema = Read("res://data/trilhas.schema.json");
		if (json is null || schema is null)
		{
			GD.PushWarning("Não foi possível ler as trilhas de traço.");
			return TraceTrails.Empty;
		}
		var parsed = TraceTrails.Parse(json, schema);
		if (parsed.IsOk) return parsed.Value!;
		GD.PushWarning(parsed.Error!);
		return TraceTrails.Empty;
	}

	static string? Read(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return file?.GetAsText();
	}
}
