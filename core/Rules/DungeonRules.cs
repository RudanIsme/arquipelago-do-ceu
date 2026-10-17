namespace ArquipelagoDoCeu.Rules;

// Quantos Andares tem um Nível, quantas Salas tem um Andar, quanta Energia o Descanso devolve e quantos Desafios tem o
// Atalho de Nível e o Desafio mestre. Também quantas vezes se vence um Conceito para dominá-lo (o que sobe a Maestria) e
// quantas moedas rende cada criatura acalmada, Sala do Item e Andar completo. Tudo vem de dados.
public sealed record DungeonRules(int FloorsMin, int FloorsMax, int RoomsMin, int RoomsMax, int RestEnergy, int ShortcutChallenges, int MasterChallenges,
	int ConceptSolves, int CoinsCreature, int CoinsItem, int CoinsFloor);

sealed record DungeonDto(int? FloorsMin, int? FloorsMax, int? RoomsMin, int? RoomsMax, int? RestEnergy, int? ShortcutChallenges, int? MasterChallenges,
	int? ConceptSolves, int? CoinsCreature, int? CoinsItem, int? CoinsFloor);

static class DungeonChecks
{
	// O Item tem de ficar num Andar anterior ao do Marco, então um Nível precisa de ao menos 2 Andares;
	// um Andar precisa de 6 Salas para caber entrada, saída, Fast travel, Descanso, Marco ou Item e mais uma;
	// e no máximo 18, porque cada porta ocupa um dos 18 hexágonos da borda da Sala, e uma Sala pode ligar a todas as outras.
	const int FloorsFloor = 2, FloorsLimit = 10, RoomsFloor = 6, RoomsLimit = 18, RestLimit = 9, ShortcutLimit = 9, MasterFloor = 2, MasterLimit = 12, SolvesLimit = 20, CoinsLimit = 1000;

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
			?? Range("dungeon.conceptSolves", dungeon.ConceptSolves, 1, SolvesLimit)
			?? Range("dungeon.coinsCreature", dungeon.CoinsCreature, 0, CoinsLimit)
			?? Range("dungeon.coinsItem", dungeon.CoinsItem, 0, CoinsLimit)
			?? Range("dungeon.coinsFloor", dungeon.CoinsFloor, 0, CoinsLimit);
	}

	public static DungeonRules Build(DungeonDto dungeon) =>
		new(dungeon.FloorsMin!.Value, dungeon.FloorsMax!.Value, dungeon.RoomsMin!.Value, dungeon.RoomsMax!.Value, dungeon.RestEnergy!.Value, dungeon.ShortcutChallenges!.Value, dungeon.MasterChallenges!.Value,
			dungeon.ConceptSolves!.Value, dungeon.CoinsCreature!.Value, dungeon.CoinsItem!.Value, dungeon.CoinsFloor!.Value);

	static string? Range(string field, int? value, int min, int max)
	{
		if (value is null) return Required(field);
		return value < min || value > max ? $"Regras inválidas: '{field}' deve estar entre {min} e {max} (veio {value})." : null;
	}

	static string? Order(string minField, int min, string maxField, int max) =>
		min > max ? $"Regras inválidas: '{minField}' ({min}) não pode ser maior que '{maxField}' ({max})." : null;

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";
}
