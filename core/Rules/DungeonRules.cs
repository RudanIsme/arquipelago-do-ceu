namespace ArquipelagoDoCeu.Rules;

// Tamanho do Nível e do Andar, Descanso, desafios do Atalho e do mestre, vitórias para dominar um Conceito.
// Tudo vem de dados.
public sealed record DungeonRules(int FloorsMin, int FloorsMax, int RoomsMin, int RoomsMax, int RestEnergy, int ShortcutChallenges, int MasterChallenges,
	int ConceptSolves);

sealed record DungeonDto(int? FloorsMin, int? FloorsMax, int? RoomsMin, int? RoomsMax, int? RestEnergy, int? ShortcutChallenges, int? MasterChallenges,
	int? ConceptSolves);

static class DungeonChecks
{
	// Limites: 2 Andares no mínimo (o Item vem antes do Marco), 6 Salas no mínimo (fixas mais uma livre)
	// e 18 no máximo, os hexágonos da borda da Sala onde cada porta fica.
	const int FloorsFloor = 2, FloorsLimit = 10, RoomsFloor = 6, RoomsLimit = 18, RestLimit = 9, ShortcutLimit = 9, MasterFloor = 2, MasterLimit = 12, SolvesLimit = 20;

	public static string? Check(DungeonDto? dungeon)
	{
		if (dungeon is null) return Required("dungeon");
		return Range("dungeon.floorsMin", dungeon.FloorsMin, FloorsFloor, FloorsLimit)
			?? Range("dungeon.floorsMax", dungeon.FloorsMax, FloorsFloor, FloorsLimit)
			?? Order("dungeon.floorsMin", dungeon.FloorsMin!.Value, "dungeon.floorsMax", dungeon.FloorsMax!.Value)
			?? Range("dungeon.roomsMin", dungeon.RoomsMin, RoomsFloor, RoomsLimit)
			?? Range("dungeon.roomsMax", dungeon.RoomsMax, RoomsFloor, RoomsLimit)
			?? Order("dungeon.roomsMin", dungeon.RoomsMin!.Value, "dungeon.roomsMax", dungeon.RoomsMax!.Value)
			?? Range("dungeon.restEnergy", dungeon.RestEnergy, 1, RestLimit)
			?? Range("dungeon.shortcutChallenges", dungeon.ShortcutChallenges, 1, ShortcutLimit)
			?? Range("dungeon.masterChallenges", dungeon.MasterChallenges, MasterFloor, MasterLimit)
			?? Range("dungeon.conceptSolves", dungeon.ConceptSolves, 1, SolvesLimit);
	}

	public static DungeonRules Build(DungeonDto dungeon) =>
		new(dungeon.FloorsMin!.Value, dungeon.FloorsMax!.Value, dungeon.RoomsMin!.Value, dungeon.RoomsMax!.Value, dungeon.RestEnergy!.Value, dungeon.ShortcutChallenges!.Value, dungeon.MasterChallenges!.Value,
			dungeon.ConceptSolves!.Value);

	static string? Range(string field, int? value, int min, int max)
	{
		if (value is null) return Required(field);
		return value < min || value > max ? $"Regras inválidas: '{field}' deve estar entre {min} e {max} (veio {value})." : null;
	}

	static string? Order(string minField, int min, string maxField, int max) =>
		min > max ? $"Regras inválidas: '{minField}' ({min}) não pode ser maior que '{maxField}' ({max})." : null;

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";
}
