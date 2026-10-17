using ArquipelagoDoCeu.Dungeon;

namespace ArquipelagoDoCeu.Saves;

// O que a criança conquistou nas Dungeons, com chaves de texto ("matematica:1:2") e ausente valendo vazio, para saves antigos abrirem.
// O Nível liberado é o `Level` do Perfil. O treino no Lobby nunca escreve aqui.
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

// O que aconteceu ao registrar uma vitória num Conceito: se ele acabou de ser dominado e a Maestria do Papel agora.
public sealed record MasteryUpdate(bool NewlyMastered, int Mastery);

// O que aconteceu ao registrar um Andar completo: se foi a primeira vez e qual Cosmético a criança ganhou.
public sealed record FloorCompletion(bool NewlyDone, Cosmetic? Cosmetic);

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

	// O Desafio mestre abre quando os dois Desafios épicos do Nível estão concluídos, pelo jogo ou pelo Atalho de Nível.
	public bool MasterOpen(string profileId, int level) =>
		EpicDone(profileId, "matematica", level) && EpicDone(profileId, "portugues", level);

	// Vencer o Desafio mestre sobe o Nível liberado em 1, até `maxLevel`; no último Nível ele fica onde está.
	public Result<ProfileSave> RaiseLevel(string profileId, int maxLevel)
	{
		var current = Find(profileId);
		if (current is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");
		var updated = current with { Level = Math.Min(current.Level + 1, Math.Max(current.Level, maxLevel)) };
		var saved = session.Commit(data => data with { Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList() });
		return saved.IsOk ? Result<ProfileSave>.Ok(updated) : Result<ProfileSave>.Fail(saved.Error!);
	}

	// O Atalho de Nível vale uma só tentativa por Nível em cada Dungeon, acertando ou errando.
	public bool ShortcutTried(string profileId, string discipline, int level) => Has(profileId, p => p.ShortcutsTried, Level(discipline, level));

	public Result<ProfileSave> MarkShortcutTried(string profileId, string discipline, int level) =>
		Add(profileId, Level(discipline, level), p => p.ShortcutsTried, (p, list) => p with { ShortcutsTried = list });

	// O Fast travel de um Andar, achado pela criança; é por ele que se retoma pelo portal do Lobby.
	public Result<ProfileSave> FindFastTravel(string profileId, string discipline, int level, int floor) =>
		Add(profileId, $"{Level(discipline, level)}:{floor}", p => p.FastTravels, (p, list) => p with { FastTravels = list });

	// O último Fast travel que a criança achou nesta torre e Nível, em qualquer Andar: é onde ela recomeça.
	public FastTravelPoint? LastFastTravel(string profileId, string discipline, int level) =>
		(Find(profileId)?.Progress?.FastTravels ?? []).Select(ParsePoint).OfType<FastTravelPoint>()
			.LastOrDefault(p => p.Discipline == discipline && p.Level == level);

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

	public FloorExploration? Exploration(string profileId, string discipline, int level, int floor, int version = 0) =>
		Find(profileId)?.Progress?.Explored?.FirstOrDefault(e => e.Key == $"{Level(discipline, level)}:{floor}" && e.Version == version) is { } saved
			? new FloorExploration(saved.Visited, saved.Cleared)
			: null;

	// Guarda o que foi explorado no Andar, no lugar do que havia. Ver o mapa sumir seria punição, então isto é gravado a cada passo.
	public Result<ProfileSave> SaveExploration(string profileId, string discipline, int level, int floor, FloorExploration exploration, int version = 0)
	{
		var key = $"{Level(discipline, level)}:{floor}";
		var entry = new ExploredFloor(key, exploration.Visited.ToList(), exploration.Cleared.ToList(), version);
		return Update(profileId, p => p with { Explored = [.. (p.Explored ?? []).Where(e => e.Key != key), entry] });
	}

	public bool FloorDone(string profileId, string discipline, int level, int floor) =>
		Has(profileId, p => p.FloorsDone, $"{Level(discipline, level)}:{floor}");

	public IReadOnlyList<string> Cosmetics(string profileId) => Find(profileId)?.Progress?.Cosmetics ?? [];

	// A Maestria do Papel (a torre) é quantos Conceitos ele já dominou. Nunca cai: o Conceito dominado fica guardado, mesmo que a
	// regra de quantas vitórias bastam mude depois, e ficar sem jogar não tira nada.
	public int Mastery(string profileId, string discipline) =>
		(Find(profileId)?.Progress?.Mastered ?? []).Count(key => key.StartsWith(discipline + ":", StringComparison.Ordinal));

	// Conta uma vitória no Conceito, em Dungeon. O que vale é o Desafio vencido, e não a pressa nem as tentativas até vencê-lo.
	public Result<MasteryUpdate> RecordSolved(string profileId, string discipline, string concept, int solvesToMaster)
	{
		var key = $"{discipline}:{concept}";
		var current = Find(profileId)?.Progress;
		if (Find(profileId) is null) return Result<MasteryUpdate>.Fail("Perfil não encontrado.");
		if ((current?.Mastered ?? []).Contains(key)) return Result<MasteryUpdate>.Ok(new MasteryUpdate(false, Mastery(profileId, discipline)));

		var solves = ((current?.Solved ?? new Dictionary<string, int>()).GetValueOrDefault(key)) + 1;
		var mastered = solves >= solvesToMaster;
		var saved = Update(profileId, p => p with
		{
			Solved = new Dictionary<string, int>(p.Solved ?? new Dictionary<string, int>()) { [key] = solves },
			Mastered = mastered ? [.. p.Mastered ?? [], key] : p.Mastered,
		});
		return saved.IsOk ? Result<MasteryUpdate>.Ok(new MasteryUpdate(mastered, Mastery(profileId, discipline))) : Result<MasteryUpdate>.Fail(saved.Error!);
	}

	// Quantas vitórias o Conceito já tem, dominado ou não.
	public int Solves(string profileId, string discipline, string concept) =>
		(Find(profileId)?.Progress?.Solved ?? new Dictionary<string, int>()).GetValueOrDefault($"{discipline}:{concept}");

	public int Coins(string profileId) => Find(profileId)?.Progress?.Coins ?? 0;

	// Moeda é só do jogo. Só soma: gastar ou perder não existe aqui, e o saldo não tem teto de regra (só o do próprio número, que nunca vira negativo).
	public Result<ProfileSave> AddCoins(string profileId, int amount)
	{
		if (Find(profileId) is not { } profile) return Result<ProfileSave>.Fail("Perfil não encontrado.");
		return amount <= 0 ? Result<ProfileSave>.Ok(profile) : Update(profileId, p => p with { Coins = (int)Math.Min((long)(p.Coins ?? 0) + amount, int.MaxValue) });
	}

	// Registra o Andar completo e dá o Cosmético dele, juntos num só salvamento e só na primeira vez: voltar ao Andar ou
	// recomeçar a Expedição não repete o prêmio. Se o save falha, nada vale e a criança completa de novo no próximo passo.
	public Result<FloorCompletion> CompleteFloor(string profileId, string discipline, int level, int floor, CosmeticCatalog catalog)
	{
		if (FloorDone(profileId, discipline, level, floor)) return Result<FloorCompletion>.Ok(new FloorCompletion(false, null));

		// Sem catálogo (o arquivo não abriu) o Andar fica por registrar, para o Cosmético não se perder para sempre.
		if (catalog.All.Count == 0) return Result<FloorCompletion>.Ok(new FloorCompletion(false, null));
		var reward = catalog.For(discipline, level, floor);
		var key = $"{Level(discipline, level)}:{floor}";
		var saved = Update(profileId, p => p with
		{
			FloorsDone = [.. p.FloorsDone ?? [], key],
			Cosmetics = reward is null || (p.Cosmetics ?? []).Contains(reward.Id) ? p.Cosmetics : [.. p.Cosmetics ?? [], reward.Id],
		});
		return saved.IsOk ? Result<FloorCompletion>.Ok(new FloorCompletion(true, reward)) : Result<FloorCompletion>.Fail(saved.Error!);
	}

	static string Level(string discipline, int level) => $"{discipline}:{level}";

	ProfileSave? Find(string profileId) => session.Data.Profiles.FirstOrDefault(p => p.Id == profileId);

	bool Has(string profileId, Func<ProgressSave, IReadOnlyList<string>?> list, string key) =>
		Find(profileId)?.Progress is { } progress && list(progress)?.Contains(key) == true;

	Result<ProfileSave> Add(string profileId, string key, Func<ProgressSave, IReadOnlyList<string>?> list,
		Func<ProgressSave, IReadOnlyList<string>, ProgressSave> set)
	{
		if (Has(profileId, list, key)) return Result<ProfileSave>.Ok(Find(profileId)!);
		return Update(profileId, p => set(p, [.. list(p) ?? [], key]));
	}

	Result<ProfileSave> Update(string profileId, Func<ProgressSave, ProgressSave> change)
	{
		var current = Find(profileId);
		if (current is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");

		var updated = current with { Progress = change(current.Progress ?? new ProgressSave()) };
		var saved = session.Commit(data => data with { Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList() });
		return saved.IsOk ? Result<ProfileSave>.Ok(updated) : Result<ProfileSave>.Fail(saved.Error!);
	}
}
