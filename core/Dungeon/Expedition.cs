using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;

namespace ArquipelagoDoCeu.Dungeon;

// Por que a criança pode ou não passar de uma Sala para outra.
public enum Passage { Open, NotLinked, MustClearRoom, NeedsItem }

// O quanto a criança enxerga de uma Sala no mapa: a visitada, a vizinha sob névoa (sem dizer o que é) ou nada.
public enum Sight { Hidden, Fog, Seen }

// Salas resolvidas sobre o total do Andar. Entrada e saída não contam: são só passagem.
public sealed record FloorProgress(int Resolved, int Total);

// As Salas que a criança já visitou e venceu num Andar; é o que o save guarda para a exploração sobreviver à Energia zerada.
public sealed record FloorExploration(IReadOnlyList<int> Visited, IReadOnlyList<int> Cleared);

// Uma Expedição numa Dungeon: onde a criança está, o que já venceu em cada Andar e a Energia da Expedição (que segue de
// uma luta para a outra). Só regras, sem Godot. Combate, Puzzle, Marco e épico precisam ser vencidos para seguir a uma Sala
// nova; voltar a uma Sala já visitada é sempre permitido, para a criança nunca ficar presa numa Sala que ainda não resolveu.
// A escada vale nos dois sentidos e cada Andar guarda o próprio estado (ADR 0004).
public sealed class Expedition
{
	sealed class FloorState
	{
		public readonly HashSet<int> Cleared = [];
		public readonly HashSet<int> Visited = [];
		public readonly HashSet<int> Rested = [];
	}

	readonly FloorProvider floors;
	readonly LevelPlan plan;
	readonly ulong world;
	readonly int version;
	readonly Func<bool> hasItem;
	readonly Func<int, FloorExploration?> explored;
	readonly Dictionary<int, FloorState> states = [];
	readonly Dictionary<int, Floor> built = [];
	readonly int startNumber;
	readonly bool legacyBelow;

	Expedition(FloorProvider floors, LevelPlan plan, ulong world, int version, Energy energy, Func<bool> hasItem, int startFloor, bool atFastTravel, bool atEpic,
		Func<int, FloorExploration?> explored)
	{
		this.floors = floors;
		this.plan = plan;
		this.world = world;
		this.version = version;
		this.hasItem = hasItem;
		this.explored = explored;
		Energy = energy;
		startNumber = atEpic ? plan.Floors.Count : plan.Endless ? Math.Max(startFloor, 1) : Math.Clamp(startFloor, 1, plan.Floors.Count);
		// Quem recomeça num Andar alto sem ter exploração guardada dos de baixo (save antigo) já os percorreu; no Atalho não.
		legacyBelow = !atEpic;
		Floor = FloorOf(startNumber);
		CurrentRoom = Floor.Entrance;
		State.Visited.Add(CurrentRoom);
		if (atEpic) MoveToRoom(RoomKind.Epic);
		else if (atFastTravel) MoveToRoom(RoomKind.FastTravel);
	}

	// `hasItem` diz se a criança tem o Item de troca do Nível da outra Dungeon, que abre o Marco.
	// Recomeça na entrada do Andar `startFloor` ou, com `atFastTravel`, no Fast travel dele; com `atEpic`, direto no
	// Desafio épico do último Andar (o Atalho de Nível). Quem chega assim já passou pelas Salas do caminho, então elas contam
	// como vencidas. `explored` devolve o que o save guardou de cada Andar (ou nada, num save antigo); os Descansos nunca são
	// guardados, então cada Expedição nova os tem de volta.
	public static Expedition Start(FloorProvider floors, LevelPlan plan, ulong world, int version, Energy energy, Func<bool>? hasItem = null,
		int startFloor = 1, bool atFastTravel = false, bool atEpic = false, Func<int, FloorExploration?>? explored = null) =>
		new(floors, plan, world, version, energy, hasItem ?? (() => false), startFloor, atFastTravel, atEpic, explored ?? (_ => null));

	public Energy Energy { get; }

	public LevelPlan Plan => plan;

	public Floor Floor { get; private set; }

	public int CurrentRoom { get; private set; }

	public Room Current => Floor.Rooms[CurrentRoom];

	FloorState State => StateOf(Floor.Number);

	public IReadOnlySet<int> Cleared => State.Cleared;

	public IReadOnlySet<int> Visited => State.Visited;

	// A Expedição acaba quando o Desafio épico do último Andar é vencido.
	public bool Finished { get; private set; }

	public bool Defeated => Energy.IsEmpty;

	public bool IsCleared(int room) => Cleared.Contains(room);

	public bool Rested(int room) => State.Rested.Contains(room);

	// O Descanso devolve `amount` de Energia, uma vez só por Sala e nunca acima do máximo. Com a Energia cheia ele
	// não é gasto: a criança pode voltar quando precisar.
	public Result<int> UseRest(int amount)
	{
		if (Current.Kind != RoomKind.Rest) return Result<int>.Fail("Aqui não dá para descansar.");
		if (State.Rested.Contains(CurrentRoom)) return Result<int>.Fail("Você já descansou aqui.");
		var gained = Math.Min(amount, Energy.Max - Energy.Current);
		if (gained <= 0) return Result<int>.Fail("Sua Energia já está cheia.");

		Energy.Recover(amount);
		State.Rested.Add(CurrentRoom);
		return Result<int>.Ok(gained);
	}

	public bool NeedsClearing(Room room) => room.Kind is RoomKind.Combat or RoomKind.Puzzle or RoomKind.Marco or RoomKind.Epic;

	public Passage CanMoveTo(int room)
	{
		if (room < 0 || room >= Floor.Rooms.Count || !Floor.Neighbors(CurrentRoom).Contains(room)) return Passage.NotLinked;
		return Visited.Contains(room) ? Passage.Open : Leaving(CurrentRoom);
	}

	// Se dá para seguir de `from` a uma Sala nova: só depois de vencer a Sala, ou com o Item no caso do Marco.
	Passage Leaving(int from)
	{
		var room = Floor.Rooms[from];
		if (!NeedsClearing(room) || Cleared.Contains(from)) return Passage.Open;
		return room.Kind == RoomKind.Marco ? (hasItem() ? Passage.Open : Passage.NeedsItem) : Passage.MustClearRoom;
	}

	public Passage MoveTo(int room)
	{
		var passage = CanMoveTo(room);
		if (passage != Passage.Open) return passage;

		// O Marco só vale como aberto se a criança tem o Item; voltar atrás sem ele não pode deixá-lo aberto.
		if (Current.Kind == RoomKind.Marco && hasItem()) State.Cleared.Add(CurrentRoom);
		CurrentRoom = room;
		State.Visited.Add(room);
		return Passage.Open;
	}

	// Vence a Sala em que a criança está. O Marco não se vence: ele abre sozinho para quem tem o Item.
	public Result<Room> ClearCurrent()
	{
		if (!NeedsClearing(Current) || Current.Kind == RoomKind.Marco) return Result<Room>.Fail("Esta Sala não se vence.");
		State.Cleared.Add(CurrentRoom);
		if (Current.Kind == RoomKind.Epic) Finished = true;
		return Result<Room>.Ok(Current);
	}

	// A escada está sempre aberta: sobe-se pela saída do Andar e desce-se pela entrada. O que trava o avanço é o Marco e o épico.
	public bool CanAscend => Current.Kind == RoomKind.Exit && plan.HasFloor(Floor.Number + 1);

	public bool CanDescend => Current.Kind == RoomKind.Entrance && Floor.Number > 1;

	public bool Ascend()
	{
		if (!CanAscend) return false;
		GoToFloor(Floor.Number + 1, fromAbove: false);
		return true;
	}

	public bool Descend()
	{
		if (!CanDescend) return false;
		GoToFloor(Floor.Number - 1, fromAbove: true);
		return true;
	}

	// O que a criança enxerga de uma Sala do Andar atual. A dois passos ou mais de qualquer Sala visitada, nem aparece.
	public Sight SightOf(int room)
	{
		if (Visited.Contains(room)) return Sight.Seen;
		return Floor.Neighbors(room).Any(Visited.Contains) ? Sight.Fog : Sight.Hidden;
	}

	// Cadeado: a Sala sob névoa que só se alcança vencendo algo antes, porque toda Sala visitada ao lado dela ainda trava a saída.
	public bool IsLocked(int room) =>
		SightOf(room) == Sight.Fog && Floor.Neighbors(room).Where(Visited.Contains).All(v => Leaving(v) != Passage.Open);

	// Combate, Puzzle, Marco e épico contam quando vencidos; Descanso e Fast travel, quando visitados.
	public FloorProgress ProgressOf(int floorNumber)
	{
		var state = StateOf(floorNumber);
		var rooms = FloorOf(floorNumber).Rooms.Where(r => r.Kind is not (RoomKind.Entrance or RoomKind.Exit)).ToList();
		var resolved = rooms.Count(r => IsResolved(r, state));
		return new FloorProgress(resolved, rooms.Count);
	}

	// O Marco aberto pelo Item já vale como vencido ao chegar nele, sem esperar a criança sair por uma porta nova.
	bool IsResolved(Room room, FloorState state) =>
		!NeedsClearing(room) ? state.Visited.Contains(room.Id)
		: state.Cleared.Contains(room.Id) || (room.Kind == RoomKind.Marco && hasItem() && state.Visited.Contains(room.Id));

	public bool IsFloorComplete(int floorNumber) => ProgressOf(floorNumber) is { Resolved: var done, Total: var total } && total > 0 && done == total;

	// O que o save guarda do Andar atual.
	public FloorExploration Exploration() => new(Visited.Order().ToList(), Cleared.Order().ToList());

	void GoToFloor(int number, bool fromAbove)
	{
		Floor = FloorOf(number);
		CurrentRoom = fromAbove ? Floor.Exit : Floor.Entrance;
		State.Visited.Add(CurrentRoom);
	}

	Floor FloorOf(int number)
	{
		if (!built.TryGetValue(number, out var floor)) built[number] = floor = BuildFloor(number);
		return floor;
	}

	Floor BuildFloor(int number) => floors.Get(world, plan.Discipline, plan.Level, plan.For(number), version);

	FloorState StateOf(int number)
	{
		if (states.TryGetValue(number, out var state)) return state;
		state = new FloorState();
		states[number] = state;
		var floor = FloorOf(number);
		if (explored(number) is { } saved)
		{
			// Sala que não existe mais (o gerador mudou) é ignorada em vez de quebrar a Expedição.
			state.Visited.UnionWith(saved.Visited.Where(id => id >= 0 && id < floor.Rooms.Count));
			state.Cleared.UnionWith(saved.Cleared.Where(id => id >= 0 && id < floor.Rooms.Count));
		}
		else if (legacyBelow && number < startNumber)
			MarkPath(floor, state, floor.Exit);
		return state;
	}

	void MoveToRoom(RoomKind kind)
	{
		// Um Andar guardado sem a Sala pedida (o gerador mudou) cai na entrada, em vez de quebrar a Expedição.
		if (Floor.Rooms.FirstOrDefault(r => r.Kind == kind) is not { } room) return;
		var target = room.Id;
		MarkPath(Floor, State, target);
		CurrentRoom = target;
	}

	// Marca o caminho da entrada até `target` como percorrido: as Salas de passagem contam como vencidas.
	void MarkPath(Floor floor, FloorState state, int target)
	{
		var previous = new Dictionary<int, int> { [floor.Entrance] = -1 };
		var queue = new Queue<int>([floor.Entrance]);
		while (queue.Count > 0 && !previous.ContainsKey(target))
		{
			var room = queue.Dequeue();
			foreach (var next in floor.Neighbors(room).Where(n => !previous.ContainsKey(n)))
			{
				previous[next] = room;
				queue.Enqueue(next);
			}
		}
		for (var room = target; room != -1; room = previous[room])
		{
			state.Visited.Add(room);
			// A Sala do Item fica por vencer: o prêmio só vem de vencê-la, e quem recomeça aqui ainda não o ganhou.
			if (NeedsClearing(floor.Rooms[room]) && room != target && !floor.Rooms[room].HoldsItem) state.Cleared.Add(room);
		}
	}
}
