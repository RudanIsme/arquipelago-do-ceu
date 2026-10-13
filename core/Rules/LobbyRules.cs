namespace ArquipelagoDoCeu.Rules;

// Um lugar ao redor do Lobby (torre ou ilha) com a placa que a criança lê. Sem Model, o jogo desenha uma torre simples.
public sealed record LobbyPlace(string Id, string Name, string Sign, string? Model, float X, float Z);

// Objeto com que a criança interage na ilha central; Id é o que o jogo reconhece ("album", "portal").
public sealed record LobbyObject(string Id, string Name, string Prompt, string? Model, int Q, int R);

public sealed record LobbyRules(float InteractionReach, IReadOnlyList<LobbyPlace> Places, IReadOnlyList<LobbyObject> Objects);

public sealed record TextSizeOption(string Id, string Name, float Scale);

public sealed record ControlOption(string Id, string Name);

public sealed record SettingsDefaults(string TextSize, bool Voice, bool ReduceMotion, bool DyslexiaFont, bool CalmMode, string Controls);

public sealed record SettingsRules(IReadOnlyList<TextSizeOption> TextSizes, IReadOnlyList<ControlOption> Controls, SettingsDefaults Defaults);

sealed record LobbyDto(float? InteractionReach, List<PlaceDto?>? Places, List<ObjectDto?>? Objects);

sealed record PlaceDto(string? Id, string? Name, string? Sign, string? Model, float? X, float? Z);

sealed record ObjectDto(string? Id, string? Name, string? Prompt, string? Model, int? Q, int? R);

sealed record SettingsDto(List<TextSizeDto?>? TextSizes, List<ControlDto?>? Controls, SettingsDefaultsDto? Defaults);

sealed record TextSizeDto(string? Id, string? Name, float? Scale);

sealed record ControlDto(string? Id, string? Name);

sealed record SettingsDefaultsDto(string? TextSize, bool? Voice, bool? ReduceMotion, bool? DyslexiaFont, bool? CalmMode, string? Controls);

// Validação do Lobby e das configurações; devolve a primeira mensagem de erro, ou null se está tudo certo.
static class LobbyChecks
{
	const float MinReach = 0.5f;
	const float MaxReach = 10f;
	const float MinScale = 0.75f;
	const float MaxScale = 2f;

	public static string? CheckLobby(LobbyDto? lobby)
	{
		if (lobby is null) return Required("lobby");
		if (lobby.InteractionReach is null) return Required("lobby.interactionReach");
		if (lobby.InteractionReach < MinReach || lobby.InteractionReach > MaxReach)
			return $"Regras inválidas: '{"lobby.interactionReach"}' deve estar entre {MinReach} e {MaxReach} (veio {lobby.InteractionReach}).";
		if (lobby.Places is null) return Required("lobby.places");
		if (lobby.Objects is null) return Required("lobby.objects");

		var placeIds = new HashSet<string>();
		for (var i = 0; i < lobby.Places.Count; i++)
		{
			var path = $"lobby.places[{i}]";
			var place = lobby.Places[i];
			if (place is null || Blank(place.Id)) return Required($"{path}.id");
			if (!placeIds.Add(place.Id!)) return Duplicate("lugar", place.Id!, "lobby.places");
			if (Blank(place.Name)) return Required($"{path}.name");
			if (Blank(place.Sign)) return Required($"{path}.sign");
			if (place.X is null) return Required($"{path}.x");
			if (place.Z is null) return Required($"{path}.z");
		}

		var objectIds = new HashSet<string>();
		for (var i = 0; i < lobby.Objects.Count; i++)
		{
			var path = $"lobby.objects[{i}]";
			var item = lobby.Objects[i];
			if (item is null || Blank(item.Id)) return Required($"{path}.id");
			if (!objectIds.Add(item.Id!)) return Duplicate("objeto", item.Id!, "lobby.objects");
			if (Blank(item.Name)) return Required($"{path}.name");
			if (Blank(item.Prompt)) return Required($"{path}.prompt");
			if (item.Q is null) return Required($"{path}.q");
			if (item.R is null) return Required($"{path}.r");
		}
		return null;
	}

	public static LobbyRules BuildLobby(LobbyDto lobby) => new(
		lobby.InteractionReach!.Value,
		lobby.Places!.Select(p => new LobbyPlace(p!.Id!, p.Name!, p.Sign!, Blank(p.Model) ? null : p.Model, p.X!.Value, p.Z!.Value)).ToList(),
		lobby.Objects!.Select(o => new LobbyObject(o!.Id!, o.Name!, o.Prompt!, Blank(o.Model) ? null : o.Model, o.Q!.Value, o.R!.Value)).ToList());

	public static string? CheckSettings(SettingsDto? settings)
	{
		if (settings is null) return Required("settings");
		if (settings.TextSizes is null || settings.TextSizes.Count == 0) return Required("settings.textSizes");
		if (settings.Controls is null || settings.Controls.Count == 0) return Required("settings.controls");
		if (settings.Defaults is null) return Required("settings.defaults");

		var sizeIds = new HashSet<string>();
		for (var i = 0; i < settings.TextSizes.Count; i++)
		{
			var path = $"settings.textSizes[{i}]";
			var size = settings.TextSizes[i];
			if (size is null || Blank(size.Id)) return Required($"{path}.id");
			if (!sizeIds.Add(size.Id!)) return Duplicate("tamanho de texto", size.Id!, "settings.textSizes");
			if (Blank(size.Name)) return Required($"{path}.name");
			if (size.Scale is null) return Required($"{path}.scale");
			if (size.Scale < MinScale || size.Scale > MaxScale)
				return $"Regras inválidas: '{path}.scale' deve estar entre {MinScale} e {MaxScale} (veio {size.Scale}).";
		}

		var controlIds = new HashSet<string>();
		for (var i = 0; i < settings.Controls.Count; i++)
		{
			var path = $"settings.controls[{i}]";
			var control = settings.Controls[i];
			if (control is null || Blank(control.Id)) return Required($"{path}.id");
			if (!controlIds.Add(control.Id!)) return Duplicate("controle", control.Id!, "settings.controls");
			if (Blank(control.Name)) return Required($"{path}.name");
		}

		var defaults = settings.Defaults;
		if (defaults.Voice is null) return Required("settings.defaults.voice");
		if (defaults.ReduceMotion is null) return Required("settings.defaults.reduceMotion");
		if (defaults.DyslexiaFont is null) return Required("settings.defaults.dyslexiaFont");
		if (defaults.CalmMode is null) return Required("settings.defaults.calmMode");
		if (defaults.TextSize is null || !sizeIds.Contains(defaults.TextSize))
			return $"Regras inválidas: '{"settings.defaults.textSize"}' deve ser um dos tamanhos de 'settings.textSizes' (veio '{defaults.TextSize}').";
		if (defaults.Controls is null || !controlIds.Contains(defaults.Controls))
			return $"Regras inválidas: '{"settings.defaults.controls"}' deve ser um dos controles de 'settings.controls' (veio '{defaults.Controls}').";
		return null;
	}

	public static SettingsRules BuildSettings(SettingsDto settings) => new(
		settings.TextSizes!.Select(t => new TextSizeOption(t!.Id!, t.Name!, t.Scale!.Value)).ToList(),
		settings.Controls!.Select(c => new ControlOption(c!.Id!, c.Name!)).ToList(),
		new SettingsDefaults(
			settings.Defaults!.TextSize!,
			settings.Defaults.Voice!.Value,
			settings.Defaults.ReduceMotion!.Value,
			settings.Defaults.DyslexiaFont!.Value,
			settings.Defaults.CalmMode!.Value,
			settings.Defaults.Controls!));

	static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";

	static string Duplicate(string what, string id, string list) => $"Regras inválidas: o {what} '{id}' aparece mais de uma vez em '{list}'.";
}
