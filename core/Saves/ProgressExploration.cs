using ArquipelagoDoCeu.Dungeon;

namespace ArquipelagoDoCeu.Saves;

// O que a criança explorou em cada Andar. Ver o mapa sumir seria punição, então isto é gravado a cada passo.
public sealed class ExplorationService(ProgressStore store)
{
	public FloorExploration? Get(string profileId, FloorKey floor, int version = 0) =>
		store.Of(profileId)?.Explored?.FirstOrDefault(e => e.Key == floor.ToString() && e.Version == version) is { } saved
			? new FloorExploration(saved.Visited, saved.Cleared)
			: null;

	// A versão do gerador em que a criança já explorou este Nível, para os Andares salvos seguirem abrindo iguais (ADR 0008);
	// um Nível ainda não explorado usa a versão atual.
	public int VersionFor(string profileId, LevelKey level, int current) =>
		store.Of(profileId)?.Explored?.Where(e => e.Version >= 1 && FloorKey.TryParse(e.Key)?.Of == level).Select(e => (int?)e.Version).Min() ?? current;

	// Guarda o que foi explorado no Andar, no lugar do que havia.
	public Result<ProfileSave> Save(string profileId, FloorKey floor, FloorExploration exploration, int version = 0)
	{
		var key = floor.ToString();
		var entry = new ExploredFloor(key, exploration.Visited.ToList(), exploration.Cleared.ToList(), version);
		return store.Update(profileId, p => p with { Explored = [.. (p.Explored ?? []).Where(e => e.Key != key), entry] });
	}

	// Andar completo é registrado pelo gatilho "floor-complete" das recompensas; aqui só se lê.
	public bool IsFloorDone(string profileId, FloorKey floor) => store.Has(profileId, p => p.FloorsDone, floor.ToString());
}
