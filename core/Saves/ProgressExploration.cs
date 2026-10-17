using ArquipelagoDoCeu.Dungeon;

namespace ArquipelagoDoCeu.Saves;

// O que a criança explorou em cada Andar. Ver o mapa sumir seria punição, então isto é gravado a cada passo.
public sealed class ExplorationService(ProgressStore store)
{
	public FloorExploration? Get(string profileId, FloorKey floor, int version = 0) =>
		store.Of(profileId)?.Explored?.FirstOrDefault(e => e.Key == floor.ToString() && e.Version == version) is { } saved
			? new FloorExploration(saved.Visited, saved.Cleared)
			: null;

	// Guarda o que foi explorado no Andar, no lugar do que havia.
	public Result<ProfileSave> Save(string profileId, FloorKey floor, FloorExploration exploration, int version = 0)
	{
		var key = floor.ToString();
		var entry = new ExploredFloor(key, exploration.Visited.ToList(), exploration.Cleared.ToList(), version);
		return store.Update(profileId, p => p with { Explored = [.. (p.Explored ?? []).Where(e => e.Key != key), entry] });
	}

	public bool IsFloorDone(string profileId, FloorKey floor) => store.Has(profileId, p => p.FloorsDone, floor.ToString());
}

// O que aconteceu ao registrar um Andar completo: se foi a primeira vez e qual Cosmético a criança ganhou.
public sealed record FloorCompletion(bool NewlyDone, Cosmetic? Cosmetic);

// O que a criança ganha por aprender e explorar: o Andar completo e o Cosmético dele.
public sealed class RewardService(ProgressStore store)
{
	public IReadOnlyList<string> Cosmetics(string profileId) => store.Of(profileId)?.Cosmetics ?? [];

	// Registra o Andar completo e dá o Cosmético dele, juntos num só salvamento e só na primeira vez: voltar ao Andar ou
	// recomeçar a Expedição não repete o prêmio. Se o save falha, nada vale e a criança completa de novo no próximo passo.
	public Result<FloorCompletion> CompleteFloor(string profileId, FloorKey floor, CosmeticCatalog catalog)
	{
		var key = floor.ToString();
		if (store.Has(profileId, p => p.FloorsDone, key)) return Result<FloorCompletion>.Ok(new FloorCompletion(false, null));

		// Sem catálogo (o arquivo não abriu) o Andar fica por registrar, para o Cosmético não se perder para sempre.
		if (catalog.All.Count == 0) return Result<FloorCompletion>.Ok(new FloorCompletion(false, null));
		var reward = catalog.For(floor.Discipline, floor.Level, floor.Floor);
		var saved = store.Update(profileId, p => p with
		{
			FloorsDone = [.. p.FloorsDone ?? [], key],
			Cosmetics = reward is null || (p.Cosmetics ?? []).Contains(reward.Id) ? p.Cosmetics : [.. p.Cosmetics ?? [], reward.Id],
		});
		return saved.IsOk ? Result<FloorCompletion>.Ok(new FloorCompletion(true, reward)) : Result<FloorCompletion>.Fail(saved.Error!);
	}
}
