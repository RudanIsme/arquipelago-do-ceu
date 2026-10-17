namespace ArquipelagoDoCeu.Saves;

// Chaves de texto ("matematica:1:2") e ausente valendo vazio, para saves antigos abrirem; o Nível liberado é o `Level` do Perfil.
// `Solved` conta vitórias por Conceito, `Mastered` os dominados (a Maestria) e `Coins` o saldo; o treino nunca escreve aqui.
public sealed record ProgressSave(
	IReadOnlyList<string>? EpicDone = null,
	IReadOnlyList<string>? Items = null,
	IReadOnlyList<string>? Floors = null,
	IReadOnlyList<string>? FastTravels = null,
	IReadOnlyList<string>? ShortcutsTried = null,
	IReadOnlyList<ExploredFloor>? Explored = null,
	IReadOnlyList<string>? FloorsDone = null,
	IReadOnlyList<string>? Cosmetics = null,
	IReadOnlyDictionary<string, int>? Solved = null,
	IReadOnlyList<string>? Mastered = null,
	int? Coins = null);

// As Salas visitadas e vencidas de um Andar, pela chave "matematica:1:2".
// `Version` é a do gerador que desenhou o Andar: com outra versão os números das Salas não valem mais.
public sealed record ExploredFloor(string Key, IReadOnlyList<int> Visited, IReadOnlyList<int> Cleared, int Version = 0);

// Os serviços do progresso, cada um com uma responsabilidade, sobre o mesmo save.
public sealed record ProgressServices(LevelProgress Levels, ExplorationService Explored, RewardService Rewards, MasteryService Mastery, WalletService Wallet)
{
	public static ProgressServices Create(SaveSession session)
	{
		var store = new ProgressStore(session);
		return new(new LevelProgress(store), new ExplorationService(store), new RewardService(store), new MasteryService(store), new WalletService(store));
	}
}

// Lê e grava o progresso de um Perfil. Cada mudança só vale depois de gravada, como no resto do save.
public sealed class ProgressStore(SaveSession session)
{
	public ProfileSave? Find(string profileId) => session.Data.Profiles.FirstOrDefault(p => p.Id == profileId);

	public ProgressSave? Of(string profileId) => Find(profileId)?.Progress;

	public bool Has(string profileId, Func<ProgressSave, IReadOnlyList<string>?> list, string key) =>
		Of(profileId) is { } progress && list(progress)?.Contains(key) == true;

	public Result<ProfileSave> Add(string profileId, string key, Func<ProgressSave, IReadOnlyList<string>?> list,
		Func<ProgressSave, IReadOnlyList<string>, ProgressSave> set)
	{
		if (Has(profileId, list, key)) return Result<ProfileSave>.Ok(Find(profileId)!);
		return Update(profileId, p => set(p, [.. list(p) ?? [], key]));
	}

	public Result<ProfileSave> Update(string profileId, Func<ProgressSave, ProgressSave> change) =>
		Replace(profileId, p => p with { Progress = change(p.Progress ?? new ProgressSave()) });

	// Troca o Perfil inteiro (o Nível liberado não mora em Progress).
	public Result<ProfileSave> Replace(string profileId, Func<ProfileSave, ProfileSave> change)
	{
		var current = Find(profileId);
		if (current is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");

		var updated = change(current);
		var saved = session.Commit(data => data with { Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList() });
		return saved.IsOk ? Result<ProfileSave>.Ok(updated) : Result<ProfileSave>.Fail(saved.Error!);
	}
}

// O Plano do Nível no save: épicos concluídos, Itens de troca, Atalhos tentados, Andares descobertos, Fast travels e o Nível liberado.
public sealed class LevelProgress(ProgressStore store)
{
	public bool EpicDone(string profileId, LevelKey level) => store.Has(profileId, p => p.EpicDone, level.ToString());

	// O Item de troca é do Nível da Dungeon que o deu, e não se perde quando a Energia zera.
	public bool HasItem(string profileId, LevelKey level) => store.Has(profileId, p => p.Items, level.ToString());

	public Result<ProfileSave> MarkEpicDone(string profileId, LevelKey level) =>
		store.Add(profileId, level.ToString(), p => p.EpicDone, (p, list) => p with { EpicDone = list });

	public Result<ProfileSave> GiveItem(string profileId, LevelKey level) =>
		store.Add(profileId, level.ToString(), p => p.Items, (p, list) => p with { Items = list });

	// O Desafio mestre abre quando os dois Desafios épicos do Nível estão concluídos, pelo jogo ou pelo Atalho de Nível.
	public bool MasterOpen(string profileId, int level) =>
		EpicDone(profileId, new LevelKey("matematica", level)) && EpicDone(profileId, new LevelKey("portugues", level));

	// Vencer o Desafio mestre sobe o Nível liberado em 1, até `maxLevel`; no último Nível ele fica onde está.
	public Result<ProfileSave> RaiseLevel(string profileId, int maxLevel) =>
		store.Replace(profileId, p => p with { Level = Math.Min(p.Level + 1, Math.Max(p.Level, maxLevel)) });

	// O Atalho de Nível vale uma só tentativa por Nível em cada Dungeon, acertando ou errando.
	public bool ShortcutTried(string profileId, LevelKey level) => store.Has(profileId, p => p.ShortcutsTried, level.ToString());

	public Result<ProfileSave> MarkShortcutTried(string profileId, LevelKey level) =>
		store.Add(profileId, level.ToString(), p => p.ShortcutsTried, (p, list) => p with { ShortcutsTried = list });

	public bool FloorDiscovered(string profileId, FloorKey floor) => store.Has(profileId, p => p.Floors, floor.ToString());

	public Result<ProfileSave> DiscoverFloor(string profileId, FloorKey floor) =>
		store.Add(profileId, floor.ToString(), p => p.Floors, (p, list) => p with { Floors = list });

	// O Fast travel de um Andar, achado pela criança; é por ele que se retoma pelo portal do Lobby.
	public Result<ProfileSave> FindFastTravel(string profileId, FloorKey floor) =>
		store.Add(profileId, floor.ToString(), p => p.FastTravels, (p, list) => p with { FastTravels = list });

	// O último Fast travel que a criança achou nesta torre e Nível, em qualquer Andar: é onde ela recomeça.
	public FloorKey? LastFastTravel(string profileId, LevelKey level)
	{
		var found = Parse(store.Of(profileId)?.FastTravels).Where(p => p.Of == level).ToList();
		return found.Count == 0 ? null : found[^1];
	}

	public IReadOnlyList<FloorKey> FastTravels(string profileId) =>
		Parse(store.Of(profileId)?.FastTravels)
			.OrderBy(p => p.Discipline, StringComparer.Ordinal).ThenBy(p => p.Level).ThenBy(p => p.Floor).ToList();

	// O Andar mais fundo que a criança já descobriu neste Nível; sem nenhum, é o 1º.
	public int HighestFloor(string profileId, LevelKey level) =>
		Parse(store.Of(profileId)?.Floors).Where(p => p.Of == level).Select(p => p.Floor).DefaultIfEmpty(1).Max();

	static IEnumerable<FloorKey> Parse(IReadOnlyList<string>? keys) => (keys ?? []).Select(FloorKey.TryParse).OfType<FloorKey>();
}
