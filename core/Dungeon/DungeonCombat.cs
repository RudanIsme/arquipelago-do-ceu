using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;

namespace ArquipelagoDoCeu.Dungeon;

// Escolhe o que se luta numa Sala: a dificuldade sobe com o Andar, e o que não existe em dados cai no mais perto.
public static class DungeonCombat
{
	// O Atalho de Nível: `count` Desafios com dificuldades sorteadas de todo o Nível. As dificuldades não se repetem enquanto
	// houver outras; quando acabam, o sorteio recomeça.
	public static Result<IReadOnlyList<TrainingSetup>> ShortcutSetups(IEnumerable<Mission> missions, string discipline, int level, int count, Pcg32 rng)
	{
		var first = Setup(missions, discipline, level, 1);
		if (!first.IsOk) return Result<IReadOnlyList<TrainingSetup>>.Fail(first.Error!);
		var year = first.Value!.Year;
		var difficulties = missions.Where(m => m.Discipline == discipline && m.Year == year).Select(m => m.Difficulty).Distinct().OrderBy(d => d).ToList();

		var drawn = new List<TrainingSetup>();
		var bag = new List<int>();
		while (drawn.Count < count)
		{
			if (bag.Count == 0)
			{
				bag = [.. difficulties];
				rng.Shuffle(bag);
			}
			drawn.Add(new TrainingSetup(discipline, year, bag[^1]));
			bag.RemoveAt(bag.Count - 1);
		}
		return Result<IReadOnlyList<TrainingSetup>>.Ok(drawn);
	}

	public static Result<TrainingSetup> Setup(IEnumerable<Mission> missions, string discipline, int level, int floorNumber)
	{
		var ofDiscipline = missions.Where(m => m.Discipline == discipline).ToList();

		// Matemática do 3º ano ainda não tem Missões próprias: usa as do ano mais alto que já existe.
		var years = ofDiscipline.Select(m => m.Year).Where(y => y <= level).ToList();
		if (years.Count == 0) return Result<TrainingSetup>.Fail("Ainda não há Desafios para essa Dungeon.");
		var year = years.Max();

		var wanted = Math.Clamp(floorNumber, 1, TrainingSetup.LastDifficulty);
		var difficulty = ofDiscipline.Where(m => m.Year == year).Select(m => m.Difficulty).Distinct()
			.OrderBy(d => Math.Abs(d - wanted)).ThenBy(d => d).First();
		return Result<TrainingSetup>.Ok(new TrainingSetup(discipline, year, difficulty));
	}
}
