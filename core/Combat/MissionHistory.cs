using ArquipelagoDoCeu.Missions;

namespace ArquipelagoDoCeu.Combat;

// Os problemas (Missões) que já apareceram numa Expedição livre, para o conteúdo variar. Prefere o que ainda não apareceu;
// quando tudo já apareceu, recomeça, mas deixa de fora os últimos vistos (quantos, `avoidRecent`, limitado ao que a lista permite).
public sealed class MissionHistory(int avoidRecent = 3)
{
	readonly List<string> seen = [];

	public IReadOnlyList<string> Seen => seen;

	public void Add(string missionId) => seen.Add(missionId);

	public IReadOnlyList<Mission> Fresh(IReadOnlyList<Mission> pool)
	{
		var unseen = pool.Where(m => !seen.Contains(m.Id)).ToList();
		if (unseen.Count > 0) return unseen;

		var recent = seen.AsEnumerable().Reverse().Distinct().Where(id => pool.Any(m => m.Id == id)).Take(Math.Min(avoidRecent, pool.Count - 1)).ToHashSet();
		return pool.Where(m => !recent.Contains(m.Id)).ToList();
	}

	// Sem histórico (o treino e as Dungeons de Progressão), a lista fica como veio.
	public static IReadOnlyList<Mission> Narrow(MissionHistory? history, IReadOnlyList<Mission> pool) => history?.Fresh(pool) ?? pool;
}
