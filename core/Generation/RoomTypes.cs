namespace ArquipelagoDoCeu.Generation;

public enum RoomShape { Circle, Square, Diamond, Hexagon }

// Como a Sala entra no progresso do Andar: não conta (só passagem), conta ao visitar ou conta ao vencer.
public enum RoomCounting { None, Visit, Victory }

public enum RoomStairs { None, Up, Down }

// O que um tipo de Sala é (ADR 0006). O jogo lê estes campos e nunca pergunta "que tipo é?": um tipo novo é uma definição
// aqui mais o desenho dele. `Free` são as Salas que o gerador sorteia e que podem guardar o Item de troca.
public sealed record RoomType(
	string Id,
	string Name,
	string Letter,
	string Color,
	RoomShape Shape,
	bool InProgress = true,
	bool NeedsVictory = false,
	bool OpensWithItem = false,
	bool EndsExpedition = false,
	bool Restores = false,
	bool CalmsCreature = false,
	bool IsDoor = false,
	bool Free = false,
	bool BeforeGate = false,
	bool SavesPoint = false,
	RoomStairs Stairs = RoomStairs.None,
	string? Line = null,
	string? LineOpen = null,
	string? Intro = null,
	string? WonLine = null)
{
	// Tem uma criatura ou porta a vencer na Sala (o Marco se vence com o Item, não com um Desafio).
	public bool HasChallenge => NeedsVictory && !OpensWithItem;

	// Só a vitória faz o Andar contar a Sala quando ela pede vitória; as outras contam ao visitar.
	public RoomCounting Counts => !InProgress ? RoomCounting.None : NeedsVictory ? RoomCounting.Victory : RoomCounting.Visit;
}

// Os tipos de Sala que o jogo conhece, por id.
public sealed class RoomTypes
{
	public static readonly RoomType Entrance = new("entrance", "Entrada", "E", "#8BC34A", RoomShape.Circle, InProgress: false, Stairs: RoomStairs.Down);

	public static readonly RoomType Combat = new("combat", "Combate", "C", "#E57373", RoomShape.Circle,
		NeedsVictory: true, CalmsCreature: true, Free: true,
		Intro: "Uma criatura da névoa! Chegue perto dela e escolha-a (clique ou F) para atacar.");

	public static readonly RoomType Puzzle = new("puzzle", "Puzzle", "P", "#64B5F6", RoomShape.Square,
		NeedsVictory: true, IsDoor: true, Free: true, BeforeGate: true,
		Intro: "Uma porta trancada! Chegue perto dela e escolha-a (clique ou F) para abrir.", WonLine: "A porta abriu! O caminho está livre.");

	public static readonly RoomType Rest = new("rest", "Descanso", "D", "#BA9BEA", RoomShape.Hexagon, Restores: true, BeforeGate: true,
		Line: "Sala de descanso. Descanse um pouco, se quiser.");

	public static readonly RoomType FastTravel = new("fastTravel", "Fast travel", "F", "#4DD0C4", RoomShape.Hexagon, BeforeGate: true, SavesPoint: true);

	// {other} é o nome da outra torre, que dá o Item de troca.
	public static readonly RoomType Marco = new("marco", "Marco", "M", "#FFB74D", RoomShape.Diamond,
		NeedsVictory: true, OpensWithItem: true,
		Line: "Este é o Marco. Ele só abre com o Item de troca da {other}.", LineOpen: "O Marco brilhou! O Item de troca abriu o caminho.");

	public static readonly RoomType Epic = new("epic", "Épico", "É", "#FFD54F", RoomShape.Diamond,
		NeedsVictory: true, EndsExpedition: true, CalmsCreature: true,
		Intro: "Desafio épico! Chegue perto da criatura e escolha-a (clique ou F) para atacar.");

	// A escada para o Andar seguinte; no último Andar a saída é o Desafio épico.
	public static readonly RoomType Exit = new("exit", "Saída", "S", "#B0BEC5", RoomShape.Hexagon, InProgress: false, Stairs: RoomStairs.Up,
		Line: "Sala de saída. A escada leva ao próximo Andar.");

	public static RoomTypes Default { get; } = new([Entrance, Combat, Puzzle, Rest, FastTravel, Marco, Epic, Exit]);

	readonly IReadOnlyList<RoomType> all;
	readonly IReadOnlyList<RoomType> free;

	RoomTypes(IReadOnlyList<RoomType> all)
	{
		this.all = all;
		free = all.Where(t => t.Free).ToList();
	}

	public IReadOnlyList<RoomType> All => all;

	// As Salas que o gerador sorteia, na ordem do registro (a ordem faz parte da seed).
	public IReadOnlyList<RoomType> Free => free;

	public RoomType? Find(string id) => all.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

	// Troca o tipo de mesmo id no lugar (a ordem entra na seed) ou acrescenta no fim.
	public RoomTypes With(RoomType type) =>
		all.Any(t => SameId(t, type)) ? new([.. all.Select(t => SameId(t, type) ? type : t)]) : new([.. all, type]);

	static bool SameId(RoomType a, RoomType b) => string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
}
