namespace ArquipelagoDoCeu.Generation;

// Decide se um Andar gerado pode ser mostrado à criança (ADR 0003). Falha esperada volta como resultado:
// quem chama troca o Andar pelo de reserva, e nunca mostra um Andar quebrado.
public static class FloorValidator
{
	public static Result<Floor> Validate(Floor floor, FloorPlan plan)
	{
		var error = Structure(floor)
			?? Contents(floor, plan)
			?? Reachability(floor, plan);
		return error is null ? Result<Floor>.Ok(floor) : Result<Floor>.Fail($"Andar {floor.Number} inválido: {error}");
	}

	// O Item vem antes do Marco e o épico só no último Andar; sem isso as duas torres poderiam travar uma à outra (ADR 0002).
	public static Result<LevelPlan> ValidatePlan(LevelPlan plan)
	{
		var floors = plan.Floors;
		var items = floors.Where(f => f.HoldsItem).ToList();
		var marcos = floors.Where(f => f.HasMarco).ToList();
		var error =
			floors.Count < 2 ? "o Plano precisa de ao menos 2 Andares."
			: items.Count != 1 ? "o Plano precisa de exatamente uma Sala do Item."
			: marcos.Count != 1 ? "o Plano precisa de exatamente um Marco."
			: items[0].Number >= marcos[0].Number ? "a Sala do Item precisa vir num Andar antes do Marco."
			: floors.Count(f => f.HasEpic) != 1 || !floors[^1].HasEpic ? "o Desafio épico precisa ficar só no último Andar."
			: null;
		return error is null ? Result<LevelPlan>.Ok(plan) : Result<LevelPlan>.Fail($"Plano inválido: {error}");
	}

	static string? Structure(Floor floor)
	{
		if (floor.Rooms.Count == 0) return "não tem Salas.";
		if (floor.Rooms.Select((r, i) => r.Id == i).Contains(false)) return "os números das Salas estão fora de ordem.";
		if (!Exists(floor, floor.Entrance) || floor.Rooms[floor.Entrance].Kind != RoomTypes.Entrance) return "a entrada não é uma Sala de entrada.";
		if (!Exists(floor, floor.Exit)) return "a saída não existe.";
		if (floor.Links.Any(l => !Exists(floor, l.A) || !Exists(floor, l.B) || l.A == l.B)) return "há uma ligação com uma Sala que não existe.";
		return null;
	}

	static string? Contents(Floor floor, FloorPlan plan)
	{
		int Count(RoomType kind) => floor.Rooms.Count(r => r.Kind == kind);
		if (Count(RoomTypes.Entrance) != 1) return "precisa de exatamente uma entrada.";
		if (Count(RoomTypes.FastTravel) != 1) return "precisa de exatamente um Fast travel.";
		if (Count(RoomTypes.Marco) != (plan.HasMarco ? 1 : 0)) return plan.HasMarco ? "falta o Marco do Plano." : "tem um Marco que o Plano não manda.";
		var items = floor.Rooms.Where(r => r.HoldsItem).ToList();
		if (items.Count != (plan.HoldsItem ? 1 : 0)) return plan.HoldsItem ? "falta a Sala do Item do Plano." : "tem uma Sala do Item que o Plano não manda.";
		if (items.Any(r => !r.Kind.Free)) return "a Sala do Item precisa ser de combate ou de Puzzle.";
		var exit = floor.Rooms[floor.Exit].Kind;
		if (plan.HasEpic ? exit != RoomTypes.Epic : exit != RoomTypes.Exit) return plan.HasEpic ? "o Desafio épico precisa ser a saída do último Andar." : "a saída precisa ser a escada.";
		return null;
	}

	static string? Reachability(Floor floor, FloorPlan plan)
	{
		var all = Walk(floor, blocked: null);
		if (all.Count != floor.Rooms.Count) return "há Sala que não é alcançável a partir da entrada.";

		var marco = floor.Rooms.FirstOrDefault(r => r.Kind == RoomTypes.Marco)?.Id;
		var free = Walk(floor, marco);
		foreach (var room in floor.Rooms.Where(r => r.Kind.BeforeGate || r.HoldsItem))
			if (!free.Contains(room.Id))
				return $"{room.Kind.Name} só é alcançável passando pelo Marco.";

		// O Marco só vale se for mesmo a passagem: se a saída dá para alcançar sem ele, o Item não serviria para nada.
		if (plan.HasMarco && free.Contains(floor.Exit)) return "a saída é alcançável sem passar pelo Marco.";
		return null;
	}

	// As Salas alcançáveis a partir da entrada, sem entrar na Sala `blocked`.
	static HashSet<int> Walk(Floor floor, int? blocked)
	{
		var seen = new HashSet<int> { floor.Entrance };
		var queue = new Queue<int>([floor.Entrance]);
		while (queue.Count > 0)
			foreach (var next in floor.Neighbors(queue.Dequeue()).Where(n => n != blocked))
				if (seen.Add(next)) queue.Enqueue(next);
		return seen;
	}

	static bool Exists(Floor floor, int id) => id >= 0 && id < floor.Rooms.Count;
}
