using System.Text.Json;

namespace ArquipelagoDoCeu.Rules;

public sealed record RoleNames(string Mathematics, string Portuguese);

// Quantos acertos seguidos sobem a dificuldade e quantos erros ou dicas a descem.
public sealed record AdaptiveRules(int StreakToLevelUp, int MistakesToLevelDown, int HintsToLevelDown);

// Todo valor de regra vem de dados (docs/tecnico), nunca fica fixo no código.
public sealed record GameRules(
	string GameName,
	RoleNames Roles,
	int MaxEnergy,
	AdaptiveRules Adaptive,
	IReadOnlyList<int> LeitnerIntervalsDays,
	ProfileRules Profile,
	AppearanceCatalog Appearance,
	SettingsRules Settings,
	CombatRules Combat,
	DungeonRules Dungeon,
	LobbyRules Lobby)
{
	// Limites que impedem um arquivo editado à mão de deixar o jogo injogável (Energia 0, por exemplo).
	const int MaxEnergyLimit = 99;
	const int AdaptiveLimit = 10;
	const int LeitnerBoxesLimit = 10;
	const int NicknameLengthLimit = 32;
	const int ProfilesLimit = 20;

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

		var error = CheckNames(dto)
			?? CheckRange("maxEnergy", dto.MaxEnergy, MaxEnergyLimit)
			?? CheckAdaptive(dto.Adaptive)
			?? CheckLeitner(dto.LeitnerIntervalsDays)
			?? CheckProfile(dto.Profile)
			?? AppearanceChecks.Check(dto.Appearance)
			?? LobbyChecks.CheckSettings(dto.Settings)
			?? CombatChecks.Check(dto.Combat)
			?? DungeonChecks.Check(dto.Dungeon)
			?? LobbyChecks.CheckLobby(dto.Lobby);
		if (error is not null) return Fail(error);

		return Result<GameRules>.Ok(new GameRules(
			dto.GameName!,
			new RoleNames(dto.Roles!.Mathematics!, dto.Roles.Portuguese!),
			dto.MaxEnergy!.Value,
			new AdaptiveRules(
				dto.Adaptive!.StreakToLevelUp!.Value,
				dto.Adaptive.MistakesToLevelDown!.Value,
				dto.Adaptive.HintsToLevelDown!.Value),
			Array.AsReadOnly(dto.LeitnerIntervalsDays!),
			new ProfileRules(dto.Profile!.MaxNicknameLength!.Value, dto.Profile.MaxProfiles!.Value),
			AppearanceChecks.Build(dto.Appearance!),
			LobbyChecks.BuildSettings(dto.Settings!),
			CombatChecks.Build(dto.Combat!),
			DungeonChecks.Build(dto.Dungeon!),
			LobbyChecks.BuildLobby(dto.Lobby!)));
	}

	static string? CheckNames(Dto dto)
	{
		if (Blank(dto.GameName)) return Required("gameName");
		if (dto.Roles is null) return Required("roles");
		if (Blank(dto.Roles.Mathematics)) return Required("roles.mathematics");
		if (Blank(dto.Roles.Portuguese)) return Required("roles.portuguese");
		return null;
	}

	static string? CheckAdaptive(AdaptiveDto? adaptive)
	{
		if (adaptive is null) return Required("adaptive");
		return CheckRange("adaptive.streakToLevelUp", adaptive.StreakToLevelUp, AdaptiveLimit)
			?? CheckRange("adaptive.mistakesToLevelDown", adaptive.MistakesToLevelDown, AdaptiveLimit)
			?? CheckRange("adaptive.hintsToLevelDown", adaptive.HintsToLevelDown, AdaptiveLimit);
	}

	static string? CheckLeitner(int[]? days)
	{
		const string field = "leitnerIntervalsDays";
		if (days is null) return Required(field);
		if (days.Length is 0 or > LeitnerBoxesLimit)
			return $"Regras inválidas: '{field}' deve ter de 1 a {LeitnerBoxesLimit} intervalos (veio {days.Length}).";
		if (days[0] < 1)
			return $"Regras inválidas: os intervalos de '{field}' devem ser maiores que zero.";
		for (var i = 1; i < days.Length; i++)
			if (days[i] <= days[i - 1])
				return $"Regras inválidas: os intervalos de '{field}' devem ser crescentes (veio {string.Join(", ", days)}).";
		return null;
	}

	static string? CheckProfile(ProfileDto? profile)
	{
		if (profile is null) return Required("profile");
		return CheckRange("profile.maxNicknameLength", profile.MaxNicknameLength, NicknameLengthLimit)
			?? CheckRange("profile.maxProfiles", profile.MaxProfiles, ProfilesLimit);
	}

	static string? CheckRange(string field, int? value, int max)
	{
		if (value is null) return Required(field);
		return value < 1 || value > max
			? $"Regras inválidas: '{field}' deve estar entre 1 e {max} (veio {value})."
			: null;
	}

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";

	static Result<GameRules> Fail(string error) => Result<GameRules>.Fail(error);

	sealed record Dto(
		string? GameName,
		RolesDto? Roles,
		int? MaxEnergy,
		AdaptiveDto? Adaptive,
		int[]? LeitnerIntervalsDays,
		ProfileDto? Profile,
		AppearanceDto? Appearance,
		SettingsDto? Settings,
		CombatDto? Combat,
		DungeonDto? Dungeon,
		LobbyDto? Lobby);

	sealed record ProfileDto(int? MaxNicknameLength, int? MaxProfiles);

	sealed record RolesDto(string? Mathematics, string? Portuguese);

	sealed record AdaptiveDto(int? StreakToLevelUp, int? MistakesToLevelDown, int? HintsToLevelDown);
}
