using ArquipelagoDoCeu.World;

namespace ArquipelagoDoCeu.Generation;

// Decide se o Andar no espaço pode ser mostrado: nenhuma Sala sobre outra, cada ligação com um corredor inteiro e sem atalho
// que passe por fora da porta. Falha esperada volta como resultado, e quem chama troca o Andar pelo de reserva (ADR 0008).
public static class FloorLayoutValidator
{
	public static Result<Floor> Validate(Floor floor)
	{
		var error = floor.Layout is null ? "não tem layout." : Check(floor, floor.Layout);
		return error is null ? Result<Floor>.Ok(floor) : Result<Floor>.Fail($"O layout do Andar {floor.Number} é inválido: {error}");
	}

	static string? Check(Floor floor, FloorLayout layout)
	{
		if (layout.Rooms.Count != floor.Rooms.Count || layout.Rooms.Select(r => r.Room).Order().Where((id, i) => id != i).Any())
			return "falta a posição de uma Sala.";
		if (layout.Corridors.Count != floor.Links.Count || floor.Links.Any(l => layout.CorridorOf(l.A, l.B) is null))
			return "falta o corredor de uma ligação.";

		var owner = new Dictionary<HexCell, int>();
		foreach (var room in layout.Rooms)
			foreach (var cell in room.Cells)
				if (!owner.TryAdd(cell, room.Room)) return $"as Salas {owner[cell]} e {room.Room} se sobrepõem.";

		var used = new Dictionary<HexCell, Corridor>();
		foreach (var corridor in layout.Corridors)
			if (Check(corridor, owner, used) is { } problem) return problem;
		return null;
	}

	static string? Check(Corridor corridor, Dictionary<HexCell, int> rooms, Dictionary<HexCell, Corridor> used)
	{
		var cells = corridor.Cells;
		var name = $"corredor {corridor.From}-{corridor.To}";
		if (cells.Count == 0) return $"{name} está vazio.";
		for (var i = 1; i < cells.Count; i++)
			if (cells[i - 1].DistanceTo(cells[i]) != 1) return $"{name} tem um buraco.";

		for (var i = 0; i < cells.Count; i++)
		{
			if (rooms.ContainsKey(cells[i])) return $"{name} passa por dentro de uma Sala.";
			if (used.TryGetValue(cells[i], out var other)) return $"{name} cruza o corredor {other.From}-{other.To}.";
			used[cells[i]] = corridor;

			// Só as portas das pontas encostam numa Sala, e só na própria: no meio, encostar seria um atalho sem porta.
			var touching = cells[i].Neighbors().Where(rooms.ContainsKey).Select(n => rooms[n]).Distinct().ToList();
			var allowed = new List<int>();
			if (i == 0) allowed.Add(corridor.From);
			if (i == cells.Count - 1) allowed.Add(corridor.To);
			if (touching.Any(t => !allowed.Contains(t))) return $"{name} encosta numa Sala que não é a dele.";
		}
		if (!cells[0].Neighbors().Any(n => rooms.GetValueOrDefault(n, -1) == corridor.From)) return $"{name} não encosta na Sala {corridor.From}.";
		if (!cells[^1].Neighbors().Any(n => rooms.GetValueOrDefault(n, -1) == corridor.To)) return $"{name} não encosta na Sala {corridor.To}.";

		foreach (var cell in cells)
			foreach (var neighbor in cell.Neighbors())
				if (used.TryGetValue(neighbor, out var other) && other != corridor) return $"{name} encosta no corredor {other.From}-{other.To}.";
		return null;
	}
}
