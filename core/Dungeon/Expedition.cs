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

	Expedition(FloorProvider floors, LevelPlan plan, ulong world, int version, Energy energy, Func<bool> hasItem)
	{
		this.floors = floors;
		this.plan = plan;
		this.world = world;
		this.version = version;
		this.hasItem = hasItem;
		Energy = energy;
		Floor = BuildFloor(1);
		Begin();
	}

	// `hasItem` diz se a criança tem o Item de troca do Nível da outra Dungeon, que abre o Marco.
	public static Expedition Start(FloorProvider floors, LevelPlan plan, ulong world, int version, Energy energy, Func<bool>? hasItem = null) =>
		new(floors, plan, world, version, energy, hasItem ?? (() => false));

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

	void Begin()
	{
		cleared.Clear();
		visited.Clear();
		CurrentRoom = Floor.Entrance;
		visited.Add(CurrentRoom);
	}
}
