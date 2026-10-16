namespace ArquipelagoDoCeu.Saves;

// O que a criança conquistou nas Dungeons. Cada lista guarda chaves de texto ("matematica:1" para um Nível,
// "matematica:1:2" para um Andar), e ausente vale vazio; por isso saves antigos continuam válidos na mesma versão.
// O Nível liberado continua sendo o `Level` do Perfil. O treino no Lobby nunca escreve aqui.
public sealed record ProgressSave(
	IReadOnlyList<string>? EpicDone = null,
	IReadOnlyList<string>? Items = null,
	IReadOnlyList<string>? Floors = null,
	IReadOnlyList<string>? FastTravels = null);

// Um Fast travel que a criança já encontrou: a torre, o Nível e o Andar.
public sealed record FastTravelPoint(string Discipline, int Level, int Floor);

// Lê e grava o progresso de um Perfil. Cada mudança só vale depois de gravada, como no resto do save.
public sealed class ProgressService(SaveSession session)
{
	public bool EpicDone(string profileId, string discipline, int level) => Has(profileId, p => p.EpicDone, Level(discipline, level));

	// O Item de troca é do Nível da Dungeon que o deu, e não se perde quando a Energia zera.
	public bool HasItem(string profileId, string discipline, int level) => Has(profileId, p => p.Items, Level(discipline, level));

	public bool FloorDiscovered(string profileId, string discipline, int level, int floor) =>
		Has(profileId, p => p.Floors, $"{Level(discipline, level)}:{floor}");

	public Result<ProfileSave> MarkEpicDone(string profileId, string discipline, int level) =>
		Add(profileId, Level(discipline, level), p => p.EpicDone, (p, list) => p with { EpicDone = list });

	public Result<ProfileSave> GiveItem(string profileId, string discipline, int level) =>
		Add(profileId, Level(discipline, level), p => p.Items, (p, list) => p with { Items = list });

	// O Fast travel de um Andar, achado pela criança; é por ele que se retoma pelo portal do Lobby.
	public Result<ProfileSave> FindFastTravel(string profileId, string discipline, int level, int floor) =>
		Add(profileId, $"{Level(discipline, level)}:{floor}", p => p.FastTravels, (p, list) => p with { FastTravels = list });

	public IReadOnlyList<FastTravelPoint> FastTravels(string profileId) =>
		(Find(profileId)?.Progress?.FastTravels ?? [])
			.Select(ParsePoint).OfType<FastTravelPoint>()
			.OrderBy(p => p.Discipline, StringComparer.Ordinal).ThenBy(p => p.Level).ThenBy(p => p.Floor).ToList();

	// O Andar mais fundo que a criança já descobriu neste Nível; sem nenhum, é o 1º.
	public int HighestFloor(string profileId, string discipline, int level) =>
		(Find(profileId)?.Progress?.Floors ?? []).Select(ParsePoint).OfType<FastTravelPoint>()
			.Where(p => p.Discipline == discipline && p.Level == level).Select(p => p.Floor).DefaultIfEmpty(1).Max();

	static FastTravelPoint? ParsePoint(string key)
	{
		var parts = key.Split(':');
		return parts.Length == 3 && int.TryParse(parts[1], out var level) && int.TryParse(parts[2], out var floor)
			? new FastTravelPoint(parts[0], level, floor)
			: null;
	}

	public Result<ProfileSave> DiscoverFloor(string profileId, string discipline, int level, int floor) =>
		Add(profileId, $"{Level(discipline, level)}:{floor}", p => p.Floors, (p, list) => p with { Floors = list });

	static string Level(string discipline, int level) => $"{discipline}:{level}";

	ProfileSave? Find(string profileId) => session.Data.Profiles.FirstOrDefault(p => p.Id == profileId);

	bool Has(string profileId, Func<ProgressSave, IReadOnlyList<string>?> list, string key) =>
		Find(profileId)?.Progress is { } progress && list(progress)?.Contains(key) == true;

	Result<ProfileSave> Add(string profileId, string key, Func<ProgressSave, IReadOnlyList<string>?> list,
		Func<ProgressSave, IReadOnlyList<string>, ProgressSave> set)
	{
		var current = Find(profileId);
		if (current is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");

		var progress = current.Progress ?? new ProgressSave();
		var existing = list(progress) ?? [];
		if (existing.Contains(key)) return Result<ProfileSave>.Ok(current);

		var updated = current with { Progress = set(progress, [.. existing, key]) };
		var saved = session.Commit(data => data with { Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList() });
		return saved.IsOk ? Result<ProfileSave>.Ok(updated) : Result<ProfileSave>.Fail(saved.Error!);
	}
}
