namespace ArquipelagoDoCeu.Rules;

// Um lugar ao redor do Lobby (torre ou ilha) com a placa que a criança lê. Sem Model, o jogo desenha uma torre simples.
public sealed record LobbyPlace(string Id, string Name, string Sign, string? Model, float X, float Z);

// Objeto com que a criança interage na ilha central; Id é o que o jogo reconhece ("album", "portal").
// Lines são as falas de um objeto que conversa (o Guia); vazio nos demais.
public sealed record LobbyObject(string Id, string Name, string Prompt, string? Model, int Q, int R, IReadOnlyList<string> Lines);

// A câmera do Lobby: tamanho inicial e limites do zoom (em unidades do mundo vistas na altura da tela), quanto cada passo de zoom muda,
// a suavidade do acompanhamento, a folga (a câmera só segue quando o Personagem sai dela) e até onde a câmera pode se afastar do centro.
public sealed record CameraRules(float Size, float MinSize, float MaxSize, float ZoomStep, float FollowSharpness, float DeadZone, float MaxPan);

// Um hexágono da ilha (q, r em coordenadas axiais), em dados.
public sealed record LobbyCell(int Q, int R);

// Uma ilha além da principal (de uma torre ou do Mercador): o centro, o raio e o hexágono da ilha principal de onde sai a ponte a pé.
// `Solid` é a ilha cujo centro não se pisa, por causa da torre que está nele.
public sealed record LobbyIsland(string Id, int Q, int R, int Radius, int FromQ, int FromR, bool Solid);

// Um ponto da ilha principal, em fração do raio dela (x para leste, z para o sul): assim o desenho acompanha o tamanho da ilha.
public sealed record LobbySpot(float X, float Z);

// Uma zona do Lobby (de um objeto: o Guia, o Álbum, o treino, o Portal, o Mestre).
public sealed record LobbyZone(string Id, float X, float Z);

// O desenho em zonas, ajustado à mão: a lagoa no centro, o caminho em anel e as árvores. Raios em fração do raio da ilha.
public sealed record LobbyLayout(float LakeRadius, float RingRadius, IReadOnlyList<LobbyZone> Zones, IReadOnlyList<LobbySpot> Trees);

public sealed record LobbyRules(float InteractionReach, IReadOnlyList<LobbyPlace> Places, IReadOnlyList<LobbyObject> Objects, CameraRules Camera, float DayCycleSeconds,
	int IslandRadius, IReadOnlyList<LobbyCell> Carved, IReadOnlyList<LobbyIsland> Islands, LobbyLayout? Layout = null);

public sealed record TextSizeOption(string Id, string Name, float Scale);

public sealed record ControlOption(string Id, string Name);

public sealed record VoiceSpeedOption(string Id, string Name, float Rate);

public sealed record SettingsDefaults(string TextSize, bool Voice, bool ReduceMotion, bool DyslexiaFont, bool CalmMode, string Controls, string VoiceSpeed, bool ReduceEffects);

public sealed record SettingsRules(IReadOnlyList<TextSizeOption> TextSizes, IReadOnlyList<ControlOption> Controls, IReadOnlyList<VoiceSpeedOption> VoiceSpeeds, SettingsDefaults Defaults);

sealed record LobbyDto(float? InteractionReach, float? DayCycleSeconds, int? IslandRadius, List<int[]?>? Carved, List<IslandDto?>? Islands, List<PlaceDto?>? Places, List<ObjectDto?>? Objects, CameraDto? Camera, LayoutDto? Layout = null);

sealed record LayoutDto(float? LakeRadius, float? RingRadius, List<ZoneDto?>? Zones, List<SpotDto?>? Trees);

sealed record ZoneDto(string? Id, float? X, float? Z);

sealed record SpotDto(float? X, float? Z);

sealed record IslandDto(string? Id, int? Q, int? R, int? Radius, int[]? From, bool? Solid);

sealed record CameraDto(float? Size, float? MinSize, float? MaxSize, float? ZoomStep, float? FollowSharpness, float? DeadZone, float? MaxPan);

sealed record PlaceDto(string? Id, string? Name, string? Sign, string? Model, float? X, float? Z);

sealed record ObjectDto(string? Id, string? Name, string? Prompt, string? Model, int? Q, int? R, List<string?>? Lines);

sealed record SettingsDto(List<TextSizeDto?>? TextSizes, List<ControlDto?>? Controls, List<VoiceSpeedDto?>? VoiceSpeeds, SettingsDefaultsDto? Defaults);

sealed record TextSizeDto(string? Id, string? Name, float? Scale);

sealed record ControlDto(string? Id, string? Name);

sealed record VoiceSpeedDto(string? Id, string? Name, float? Rate);

sealed record SettingsDefaultsDto(string? TextSize, bool? Voice, bool? ReduceMotion, bool? DyslexiaFont, bool? CalmMode, string? Controls, string? VoiceSpeed, bool? ReduceEffects);

// Validação do Lobby e das configurações; devolve a primeira mensagem de erro, ou null se está tudo certo.
static class LobbyChecks
{
	const float MinReach = 0.5f;
	const float MaxReach = 10f;
	const float MinScale = 0.75f;
	const float MaxScale = 2f;
	const float MinDayCycle = 60f;
	const float MaxDayCycle = 3600f;
	const float MinRate = 0.5f;
	const float MaxRate = 2f;

	public static string? CheckLobby(LobbyDto? lobby)
	{
		if (lobby is null) return Required("lobby");
		if (lobby.InteractionReach is null) return Required("lobby.interactionReach");
		if (lobby.InteractionReach < MinReach || lobby.InteractionReach > MaxReach)
			return $"Regras inválidas: '{"lobby.interactionReach"}' deve estar entre {MinReach} e {MaxReach} (veio {lobby.InteractionReach}).";
		if (lobby.DayCycleSeconds is null) return Required("lobby.dayCycleSeconds");
		if (lobby.DayCycleSeconds < MinDayCycle || lobby.DayCycleSeconds > MaxDayCycle)
			return $"Regras inválidas: 'lobby.dayCycleSeconds' deve estar entre {MinDayCycle} e {MaxDayCycle} (veio {lobby.DayCycleSeconds}).";
		if (lobby.IslandRadius is not (>= 4 and <= 16)) return "Regras inválidas: 'lobby.islandRadius' deve estar entre 4 e 16.";
		if (CheckIslands(lobby) is { } islandError) return islandError;
		if (CheckLayout(lobby.Layout) is { } layoutError) return layoutError;
		if (lobby.Places is null) return Required("lobby.places");
		if (lobby.Objects is null) return Required("lobby.objects");
		if (CheckCamera(lobby.Camera) is { } cameraError) return cameraError;

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
			var inZone = lobby.Layout?.Zones?.Any(z => z?.Id == item.Id) == true;
			if (item.Q is null && !inZone) return Required($"{path}.q");
			if (item.R is null && !inZone) return Required($"{path}.r");
			if (item.Lines?.Any(Blank) == true) return Required($"{path}.lines");
		}
		return null;
	}

	static string? CheckIslands(LobbyDto lobby)
	{
		if (lobby.Carved?.Any(c => c is not { Length: 2 }) == true) return "Regras inválidas: cada item de 'lobby.carved' deve ser [q, r].";
		var ids = new HashSet<string>();
		for (var i = 0; i < (lobby.Islands?.Count ?? 0); i++)
		{
			var path = $"lobby.islands[{i}]";
			var island = lobby.Islands![i];
			if (island is null || Blank(island.Id)) return Required($"{path}.id");
			if (!ids.Add(island.Id!)) return Duplicate("ilha", island.Id!, "lobby.islands");
			if (island.Q is null) return Required($"{path}.q");
			if (island.R is null) return Required($"{path}.r");
			if (island.Radius is not (>= 1 and <= 4)) return $"Regras inválidas: '{path}.radius' deve estar entre 1 e 4.";
			if (island.From is not { Length: 2 }) return $"Regras inválidas: '{path}.from' deve ser [q, r], o hexágono da ilha principal de onde sai a ponte.";
		}
		return null;
	}

	// O desenho em zonas é opcional; quando existe, tudo é fração do raio da ilha, entre -1 e 1, e a lagoa fica dentro do anel.
	static string? CheckLayout(LayoutDto? layout)
	{
		if (layout is null) return null;
		if (layout.LakeRadius is not (> 0f and < 1f)) return "Regras inválidas: 'lobby.layout.lakeRadius' deve estar entre 0 e 1.";
		if (layout.RingRadius is not (> 0f and < 1f) || layout.RingRadius <= layout.LakeRadius) return "Regras inválidas: 'lobby.layout.ringRadius' deve estar entre 0 e 1 e passar do raio da lagoa.";
		var ids = new HashSet<string>();
		foreach (var zone in layout.Zones ?? [])
		{
			if (zone is null || Blank(zone.Id) || zone.X is not (>= -1f and <= 1f) || zone.Z is not (>= -1f and <= 1f)) return "Regras inválidas: cada zona de 'lobby.layout.zones' precisa de id, x e z entre -1 e 1.";
			if (!ids.Add(zone.Id!)) return Duplicate("zona", zone.Id!, "lobby.layout.zones");
		}
		if (layout.Trees?.Any(t => t is null || t.X is not (>= -1f and <= 1f) || t.Z is not (>= -1f and <= 1f)) == true) return "Regras inválidas: cada árvore de 'lobby.layout.trees' precisa de x e z entre -1 e 1.";
		return null;
	}

	static string? CheckCamera(CameraDto? camera)
	{
		if (camera is null) return Required("lobby.camera");
		foreach (var (field, value, min, max) in new (string, float?, float, float)[]
		{
			("size", camera.Size, 4f, 100f), ("minSize", camera.MinSize, 4f, 100f), ("maxSize", camera.MaxSize, 4f, 100f), ("zoomStep", camera.ZoomStep, 0.5f, 20f),
			("followSharpness", camera.FollowSharpness, 0.5f, 30f), ("deadZone", camera.DeadZone, 0f, 10f), ("maxPan", camera.MaxPan, 0f, 30f),
		})
		{
			if (value is null) return Required($"lobby.camera.{field}");
			if (value < min || value > max) return $"Regras inválidas: 'lobby.camera.{field}' deve estar entre {min} e {max} (veio {value}).";
		}
		if (camera.MinSize > camera.Size || camera.Size > camera.MaxSize) return "Regras inválidas: 'lobby.camera' precisa de minSize <= size <= maxSize.";
		return null;
	}

	public static LobbyRules BuildLobby(LobbyDto lobby) => new(
		lobby.InteractionReach!.Value,
		lobby.Places!.Select(p => new LobbyPlace(p!.Id!, p.Name!, p.Sign!, Blank(p.Model) ? null : p.Model, p.X!.Value, p.Z!.Value)).ToList(),
		lobby.Objects!.Select(o => BuildObject(o!, lobby)).ToList(),
		new CameraRules(lobby.Camera!.Size!.Value, lobby.Camera.MinSize!.Value, lobby.Camera.MaxSize!.Value, lobby.Camera.ZoomStep!.Value, lobby.Camera.FollowSharpness!.Value, lobby.Camera.DeadZone!.Value, lobby.Camera.MaxPan!.Value),
		lobby.DayCycleSeconds!.Value,
		lobby.IslandRadius!.Value,
		(lobby.Carved ?? []).Select(c => new LobbyCell(c![0], c[1])).ToList(),
		(lobby.Islands ?? []).Select(i => new LobbyIsland(i!.Id!, i.Q!.Value, i.R!.Value, i.Radius!.Value, i.From![0], i.From[1], i.Solid ?? false)).ToList(),
		lobby.Layout is null ? null : new LobbyLayout(lobby.Layout.LakeRadius!.Value, lobby.Layout.RingRadius!.Value,
			(lobby.Layout.Zones ?? []).Select(z => new LobbyZone(z!.Id!, z.X!.Value, z.Z!.Value)).ToList(), (lobby.Layout.Trees ?? []).Select(t => new LobbySpot(t!.X!.Value, t.Z!.Value)).ToList()));

	// Um objeto de zona não traz q e r: o hexágono sai do ponto dele em fração do raio da ilha.
	static LobbyObject BuildObject(ObjectDto o, LobbyDto lobby)
	{
		var zone = lobby.Layout?.Zones?.FirstOrDefault(z => z?.Id == o.Id);
		var cell = zone is null ? new World.HexCell(o.Q!.Value, o.R!.Value) : World.LobbyPlan.CellOf(zone.X!.Value, zone.Z!.Value, lobby.IslandRadius!.Value);
		return new LobbyObject(o.Id!, o.Name!, o.Prompt!, Blank(o.Model) ? null : o.Model, cell.Q, cell.R, o.Lines?.Select(l => l!).ToList() ?? []);
	}

	public static string? CheckSettings(SettingsDto? settings)
	{
		if (settings is null) return Required("settings");
		if (settings.TextSizes is null || settings.TextSizes.Count == 0) return Required("settings.textSizes");
		if (settings.Controls is null || settings.Controls.Count == 0) return Required("settings.controls");
		if (settings.VoiceSpeeds is null || settings.VoiceSpeeds.Count == 0) return Required("settings.voiceSpeeds");
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

		var speedIds = new HashSet<string>();
		for (var i = 0; i < settings.VoiceSpeeds.Count; i++)
		{
			var path = $"settings.voiceSpeeds[{i}]";
			var speed = settings.VoiceSpeeds[i];
			if (speed is null || Blank(speed.Id)) return Required($"{path}.id");
			if (!speedIds.Add(speed.Id!)) return Duplicate("velocidade de voz", speed.Id!, "settings.voiceSpeeds");
			if (Blank(speed.Name)) return Required($"{path}.name");
			if (speed.Rate is null) return Required($"{path}.rate");
			if (speed.Rate < MinRate || speed.Rate > MaxRate)
				return $"Regras inválidas: '{path}.rate' deve estar entre {MinRate} e {MaxRate} (veio {speed.Rate}).";
		}

		var defaults = settings.Defaults;
		if (defaults.Voice is null) return Required("settings.defaults.voice");
		if (defaults.ReduceMotion is null) return Required("settings.defaults.reduceMotion");
		if (defaults.DyslexiaFont is null) return Required("settings.defaults.dyslexiaFont");
		if (defaults.CalmMode is null) return Required("settings.defaults.calmMode");
		if (defaults.ReduceEffects is null) return Required("settings.defaults.reduceEffects");
		if (defaults.TextSize is null || !sizeIds.Contains(defaults.TextSize))
			return $"Regras inválidas: '{"settings.defaults.textSize"}' deve ser um dos tamanhos de 'settings.textSizes' (veio '{defaults.TextSize}').";
		if (defaults.Controls is null || !controlIds.Contains(defaults.Controls))
			return $"Regras inválidas: '{"settings.defaults.controls"}' deve ser um dos controles de 'settings.controls' (veio '{defaults.Controls}').";
		if (defaults.VoiceSpeed is null || !speedIds.Contains(defaults.VoiceSpeed))
			return $"Regras inválidas: '{"settings.defaults.voiceSpeed"}' deve ser uma das velocidades de 'settings.voiceSpeeds' (veio '{defaults.VoiceSpeed}').";
		return null;
	}

	public static SettingsRules BuildSettings(SettingsDto settings) => new(
		settings.TextSizes!.Select(t => new TextSizeOption(t!.Id!, t.Name!, t.Scale!.Value)).ToList(),
		settings.Controls!.Select(c => new ControlOption(c!.Id!, c.Name!)).ToList(),
		settings.VoiceSpeeds!.Select(v => new VoiceSpeedOption(v!.Id!, v.Name!, v.Rate!.Value)).ToList(),
		new SettingsDefaults(
			settings.Defaults!.TextSize!,
			settings.Defaults.Voice!.Value,
			settings.Defaults.ReduceMotion!.Value,
			settings.Defaults.DyslexiaFont!.Value,
			settings.Defaults.CalmMode!.Value,
			settings.Defaults.Controls!,
			settings.Defaults.VoiceSpeed!,
			settings.Defaults.ReduceEffects!.Value));

	static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";

	static string Duplicate(string what, string id, string list) => $"Regras inválidas: o {what} '{id}' aparece mais de uma vez em '{list}'.";
}
