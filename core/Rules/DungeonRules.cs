namespace ArquipelagoDoCeu.Rules;

// Quantos Andares tem um Nível, quantas Salas tem um Andar e quanta Energia o Descanso devolve. Tudo vem de dados.
public sealed record DungeonRules(int FloorsMin, int FloorsMax, int RoomsMin, int RoomsMax, int RestEnergy);

sealed record DungeonDto(int? FloorsMin, int? FloorsMax, int? RoomsMin, int? RoomsMax, int? RestEnergy);

static class DungeonChecks
{
	// O Item tem de ficar num Andar anterior ao do Marco, então um Nível precisa de ao menos 2 Andares;
	// e um Andar precisa de 6 Salas para caber entrada, saída, Fast travel, Descanso, Marco ou Item e mais uma.
	const int FloorsFloor = 2, FloorsLimit = 10, RoomsFloor = 6, RoomsLimit = 30, RestLimit = 9;

	public static string? Check(DungeonDto? dungeon)
	{
		if (dungeon is null) return Required("dungeon");
		return Range("dungeon.floorsMin", dungeon.FloorsMin, FloorsFloor, FloorsLimit)
			?? Range("dungeon.floorsMax", dungeon.FloorsMax, FloorsFloor, FloorsLimit)
			?? Order("dungeon.floorsMin", dungeon.FloorsMin!.Value, "dungeon.floorsMax", dungeon.FloorsMax!.Value)
			?? Range("dungeon.roomsMin", dungeon.RoomsMin, RoomsFloor, RoomsLimit)
			?? Range("dungeon.roomsMax", dungeon.RoomsMax, RoomsFloor, RoomsLimit)
			?? Order("dungeon.roomsMin", dungeon.RoomsMin!.Value, "dungeon.roomsMax", dungeon.RoomsMax!.Value)
			?? Range("dungeon.restEnergy", dungeon.RestEnergy, 1, RestLimit);
	}

	public static DungeonRules Build(DungeonDto dungeon) =>
		new(dungeon.FloorsMin!.Value, dungeon.FloorsMax!.Value, dungeon.RoomsMin!.Value, dungeon.RoomsMax!.Value, dungeon.RestEnergy!.Value);

	static string? Range(string field, int? value, int min, int max)
	{
		if (value is null) return Required(field);
		return value < min || value > max ? $"Regras inválidas: '{field}' deve estar entre {min} e {max} (veio {value})." : null;
	}

	static string? Order(string minField, int min, string maxField, int max) =>
		min > max ? $"Regras inválidas: '{minField}' ({min}) não pode ser maior que '{maxField}' ({max})." : null;

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";
}
