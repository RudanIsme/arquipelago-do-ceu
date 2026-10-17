using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;

namespace ArquipelagoDoCeu.Dungeon;

// Escolhe o que se luta numa Sala: a dificuldade sobe com o Andar, e o que não existe em dados cai no mais perto.
public static class DungeonCombat
{
	// O Desafio mestre: `count` Desafios alternando as disciplinas das torres, cada um com dificuldade sorteada do Nível.
	// Todas as disciplinas aparecem em partes iguais, e quem começa também é sorteado.
	public static Result<IReadOnlyList<TrainingSetup>> MasterSetups(IEnumerable<Mission> missions, IReadOnlyList<string> disciplines, int level, int count, Pcg32 rng)
	{
		if (disciplines.Count == 0) return Result<IReadOnlyList<TrainingSetup>>.Fail("Não há torres para o Desafio mestre.");
		var list = missions.ToList();
		var perDiscipline = (count + disciplines.Count - 1) / disciplines.Count;
		var lanes = new List<IReadOnlyList<TrainingSetup>>();
		foreach (var discipline in disciplines)
		{
			var lane = ShortcutSetups(list, discipline, level, perDiscipline, rng);
			if (!lane.IsOk) return Result<IReadOnlyList<TrainingSetup>>.Fail(lane.Error!);
			lanes.Add(lane.Value!);
		}

		rng.Shuffle(lanes);
		var mixed = new List<TrainingSetup>();
		for (var i = 0; i < count; i++) mixed.Add(lanes[i % lanes.Count][i / lanes.Count]);
		return Result<IReadOnlyList<TrainingSetup>>.Ok(mixed);
	}

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

	// O Conceito do Andar: o que mais aparece entre as Missões da dificuldade dele (em empate, o primeiro em ordem alfabética).
	// É o objetivo da placa do HUD; o jogo nunca o inventa, só lê o que as Missões já dizem.
	public static string? ConceptOf(IEnumerable<Mission> missions, TrainingSetup setup) =>
		missions.Where(m => m.Discipline == setup.Discipline && m.Year == setup.Year && m.Difficulty == setup.Difficulty)
			.GroupBy(m => m.Concept).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
			.Select(g => g.Key).FirstOrDefault();

	// A Expedição livre: a dificuldade acompanha o Andar, mas procura, por perto, uma que ainda tenha problema novo, para o
	// conteúdo variar mesmo depois de passar do último Andar que as Missões distinguem.
	public static Result<TrainingSetup> FreeSetup(IEnumerable<Mission> missions, MissionHistory history, string discipline, int level, int floorNumber)
	{
		var list = missions.ToList();
		var baseSetup = Setup(list, discipline, level, floorNumber);
		if (!baseSetup.IsOk) return baseSetup;

		var year = baseSetup.Value!.Year;
		var inYear = list.Where(m => m.Discipline == discipline && m.Year == year).ToList();
		var wanted = baseSetup.Value.Difficulty;
		// No máximo uma dificuldade para cima ou para baixo: a criança nunca recebe um salto grande de dificuldade.
		var near = inYear.Select(m => m.Difficulty).Distinct().Where(d => Math.Abs(d - wanted) <= 1).OrderBy(d => Math.Abs(d - wanted)).ThenBy(d => d);
		var fresh = near.FirstOrDefault(d => inYear.Any(m => m.Difficulty == d && !history.Seen.Contains(m.Id)));
		return Result<TrainingSetup>.Ok(new TrainingSetup(discipline, year, fresh == 0 ? wanted : fresh));
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
