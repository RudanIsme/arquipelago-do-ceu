using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;

namespace ArquipelagoDoCeu.Dungeon;

// Por que a criança pode ou não passar de uma Sala para outra.
public enum Passage { Open, NotLinked, MustClearRoom, NeedsItem }

// Uma Expedição numa Dungeon: onde a criança está, o que já venceu e a Energia da Expedição (que segue de uma luta
// para a outra). Só regras, sem Godot. Combate, Puzzle, Marco e épico precisam ser vencidos para seguir a uma Sala nova;
// voltar a uma Sala já visitada é sempre permitido, para a criança nunca ficar presa numa Sala que ainda não resolveu.
public sealed class Expedition
{
	readonly FloorProvider floors;
	readonly LevelPlan plan;
	readonly ulong world;
	readonly int version;
	readonly Func<bool> hasItem;
	readonly HashSet<int> cleared = [];
	readonly HashSet<int> visited = [];
	readonly HashSet<int> rested = [];

	Expedition(FloorProvider floors, LevelPlan plan, ulong world, int version, Energy energy, Func<bool> hasItem, int startFloor, bool atFastTravel, bool atEpic)
	{
		this.floors = floors;
		this.plan = plan;
		this.world = world;
		this.version = version;
		this.hasItem = hasItem;
		Energy = energy;
		Floor = BuildFloor(atEpic ? plan.Floors.Count : Math.Clamp(startFloor, 1, plan.Floors.Count));
		Begin();
		if (atEpic) MoveToRoom(RoomKind.Epic);
		else if (atFastTravel) MoveToRoom(RoomKind.FastTravel);
	}

	// `hasItem` diz se a criança tem o Item de troca do Nível da outra Dungeon, que abre o Marco.
	// Recomeça na entrada do Andar `startFloor` ou, com `atFastTravel`, no Fast travel dele; com `atEpic`, direto no
	// Desafio épico do último Andar (o Atalho de Nível). Quem chega assim já passou pelas Salas do caminho, então elas contam
	// como vencidas.
	public static Expedition Start(FloorProvider floors, LevelPlan plan, ulong world, int version, Energy energy, Func<bool>? hasItem = null,
		int startFloor = 1, bool atFastTravel = false, bool atEpic = false) =>
		new(floors, plan, world, version, energy, hasItem ?? (() => false), startFloor, atFastTravel, atEpic);

	public Energy Energy { get; }

	public LevelPlan Plan => plan;

	public Floor Floor { get; private set; }

	public int CurrentRoom { get; private set; }

	public Room Current => Floor.Rooms[CurrentRoom];

	public IReadOnlySet<int> Cleared => cleared;

	public IReadOnlySet<int> Visited => visited;

	// A Expedição acaba quando o Desafio épico do último Andar é vencido.
	public bool Finished { get; private set; }

	public bool Defeated => Energy.IsEmpty;

	public bool IsCleared(int room) => cleared.Contains(room);

	public bool Rested(int room) => rested.Contains(room);

	// O Descanso devolve `amount` de Energia, uma vez só por Sala e nunca acima do máximo. Com a Energia cheia ele
	// não é gasto: a criança pode voltar quando precisar.
	public Result<int> UseRest(int amount)
	{
		if (Current.Kind != RoomKind.Rest) return Result<int>.Fail("Aqui não dá para descansar.");
		if (rested.Contains(CurrentRoom)) return Result<int>.Fail("Você já descansou aqui.");
		var gained = Math.Min(amount, Energy.Max - Energy.Current);
		if (gained <= 0) return Result<int>.Fail("Sua Energia já está cheia.");

		Energy.Recover(amount);
		rested.Add(CurrentRoom);
		return Result<int>.Ok(gained);
	}

	public bool NeedsClearing(Room room) => room.Kind is RoomKind.Combat or RoomKind.Puzzle or RoomKind.Marco or RoomKind.Epic;

	public Passage CanMoveTo(int room)
	{
		if (room < 0 || room >= Floor.Rooms.Count || !Floor.Neighbors(CurrentRoom).Contains(room)) return Passage.NotLinked;
		if (visited.Contains(room)) return Passage.Open;
		if (!NeedsClearing(Current) || cleared.Contains(CurrentRoom)) return Passage.Open;
		return Current.Kind == RoomKind.Marco ? (hasItem() ? Passage.Open : Passage.NeedsItem) : Passage.MustClearRoom;
	}

	public Passage MoveTo(int room)
	{
		var passage = CanMoveTo(room);
		if (passage != Passage.Open) return passage;

		if (Current.Kind == RoomKind.Marco) cleared.Add(CurrentRoom);
		CurrentRoom = room;
		visited.Add(room);
		return Passage.Open;
	}

	// Vence a Sala em que a criança está. O Marco não se vence: ele abre sozinho para quem tem o Item.
	public Result<Room> ClearCurrent()
	{
		if (!NeedsClearing(Current) || Current.Kind == RoomKind.Marco) return Result<Room>.Fail("Esta Sala não se vence.");
		cleared.Add(CurrentRoom);
		if (Current.Kind == RoomKind.Epic) Finished = true;
		return Result<Room>.Ok(Current);
	}

	public bool CanAscend => Current.Kind == RoomKind.Exit && Floor.Number < plan.Floors.Count;

	public bool Ascend()
	{
		if (!CanAscend) return false;
		Floor = BuildFloor(Floor.Number + 1);
		Begin();
		return true;
	}

	Floor BuildFloor(int number) => floors.Get(world, plan.Discipline, plan.Level, plan.Floors[number - 1], version);

	void MoveToRoom(RoomKind kind)
	{
		var target = Floor.Rooms.First(r => r.Kind == kind).Id;
		var previous = new Dictionary<int, int> { [Floor.Entrance] = -1 };
		var queue = new Queue<int>([Floor.Entrance]);
		while (queue.Count > 0 && !previous.ContainsKey(target))
		{
			var room = queue.Dequeue();
			foreach (var next in Floor.Neighbors(room).Where(n => !previous.ContainsKey(n)))
			{
				previous[next] = room;
				queue.Enqueue(next);
			}
		}
		for (var room = target; room != -1; room = previous[room])
		{
			visited.Add(room);
			if (NeedsClearing(Floor.Rooms[room]) && room != target) cleared.Add(room);
		}
		CurrentRoom = target;
	}

	void Begin()
	{
		cleared.Clear();
		visited.Clear();
		rested.Clear();
		CurrentRoom = Floor.Entrance;
		visited.Add(CurrentRoom);
	}
}
