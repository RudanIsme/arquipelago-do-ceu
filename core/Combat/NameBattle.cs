using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;

namespace ArquipelagoDoCeu.Combat;

// O que a conferência do nome decidiu. `NewStrikes` conta só as sílabas certas ainda não golpeadas;
// `Mistaken` é verdadeiro quando alguma sílaba posta está errada (sílaba que falta não é erro).
public sealed record NameVerdict(IReadOnlyList<bool> Correct, int NewStrikes, bool Freed, bool Mistaken);

// A luta do Escriba: a criatura esqueceu o nome. A criança monta as sílabas da bolsa; cada sílaba certa é um
// golpe e o nome completo liberta a criatura. O nome vem de uma Missão de formar palavras; o jogo só o confere.
public sealed class NameBattle
{
	readonly IReadOnlyList<Mission> missions;
	readonly Pcg32 rng;
	readonly List<int> chosen = [];
	readonly HashSet<int> hidden = [];
	List<string> pieces = [];
	bool[] struck = [];

	NameBattle(IReadOnlyList<Mission> missions, Pcg32 rng)
	{
		this.missions = missions;
		this.rng = rng;
		Mission = missions[0];
		Name = [];
	}

	public Mission Mission { get; private set; }

	public IReadOnlyList<string> Name { get; private set; }

	// A palavra inteira, para a voz dizer o nome da criatura.
	public string Word => string.Concat(Name);

	// A imagem da criatura (um nome de arquivo sem extensão): a criança não precisa ler.
	public string Image { get; private set; } = "";

	public IReadOnlyList<string> Pieces => pieces;

	// Quantas sílabas ainda faltam golpear: é o "escudo" do Escriba.
	public int Remaining => struck.Count(s => !s);

	// Os índices das peças já postas, na ordem em que foram postas.
	public IReadOnlyList<int> Chosen => chosen;

	public bool IsHidden(int piece) => hidden.Contains(piece);

	public static Result<NameBattle> Start(IEnumerable<Mission> available, Pcg32 rng)
	{
		var usable = available.Where(m => m.Mechanic == "formar_palavras").ToList();
		if (usable.Count == 0) return Result<NameBattle>.Fail("Não há Missão de formar palavras para o Boneco.");

		var battle = new NameBattle(usable, rng);
		var dealt = battle.Deal(usable[rng.NextInt(usable.Count)]);
		return dealt.IsOk ? Result<NameBattle>.Ok(battle) : Result<NameBattle>.Fail(dealt.Error!);
	}

	// Outro nome, de outra Missão quando houver mais de uma. Missão mal escrita é pulada, nunca trava a luta;
	// se nenhuma serve, o mesmo nome recomeça do zero.
	public void Next()
	{
		var others = missions.Where(m => m != Mission).ToList();
		var pool = others.Count > 0 ? others : missions;
		for (var attempt = 0; attempt < pool.Count; attempt++)
		{
			var pick = pool[(rng.NextInt(pool.Count) + attempt) % pool.Count];
			if (Deal(pick).IsOk) return;
		}
		Deal(Mission);
	}

	public bool Place(int piece)
	{
		if (piece < 0 || piece >= pieces.Count || chosen.Contains(piece) || hidden.Contains(piece) || chosen.Count >= Name.Count) return false;
		chosen.Add(piece);
		return true;
	}

	public bool Undo()
	{
		if (chosen.Count == 0) return false;
		chosen.RemoveAt(chosen.Count - 1);
		return true;
	}

	public NameVerdict Check()
	{
		var check = NameChecker.Check(Name, chosen.Select(i => pieces[i]).ToList());
		var strikes = 0;
		for (var i = 0; i < Name.Count; i++)
			if (check.Correct[i] && !struck[i])
			{
				struck[i] = true;
				strikes++;
			}
		var mistaken = Enumerable.Range(0, chosen.Count).Any(i => !check.Correct[i]);
		return new NameVerdict(check.Correct, strikes, check.IsComplete, mistaken);
	}

	// Travessura do Boneco: embaralha as sílabas da bolsa ou esconde uma por alguns segundos.
	public Prank PlayPrank()
	{
		var free = Enumerable.Range(0, pieces.Count).Where(i => !chosen.Contains(i) && !hidden.Contains(i)).ToList();
		if (free.Count > 1 && rng.NextInt(2) == 0)
		{
			var order = Enumerable.Range(0, pieces.Count).ToList();
			rng.Shuffle(order);
			Remap(order);
			return Prank.Shuffled;
		}
		if (free.Count > 0) hidden.Add(free[rng.NextInt(free.Count)]);
		return free.Count > 0 ? Prank.Hidden : Prank.Shuffled;
	}

	public void Reveal() => hidden.Clear();

	// `order[i]` diz de qual posição antiga vem a peça que passa a ficar em `i`; as escolhas acompanham as peças.
	void Remap(List<int> order)
	{
		var moved = order.Select(old => pieces[old]).ToList();
		for (var k = 0; k < chosen.Count; k++) chosen[k] = order.IndexOf(chosen[k]);
		var newHidden = hidden.Select(h => order.IndexOf(h)).ToList();
		hidden.Clear();
		foreach (var h in newHidden) hidden.Add(h);
		pieces = moved;
	}

	Result<bool> Deal(Mission mission)
	{
		var parsed = Parse(mission);
		if (!parsed.IsOk) return Result<bool>.Fail(parsed.Error!);

		var (name, pool, distractors, image) = parsed.Value;
		var dealt = SyllableBag.Deal(name, pool, distractors, rng);
		if (!dealt.IsOk) return Result<bool>.Fail($"{mission.Id}: {dealt.Error}");

		Mission = mission;
		Name = name;
		Image = image;
		pieces = [.. dealt.Value!];
		chosen.Clear();
		hidden.Clear();
		struck = new bool[name.Count];
		return Result<bool>.Ok(true);
	}

	static Result<(List<string> Name, List<string> Pool, int Distractors, string Image)> Parse(Mission mission)
	{
		try
		{
			var name = mission.Params["syllables"].EnumerateArray().Select(s => s.GetString()!).ToList();
			var pool = mission.Params["pool"].EnumerateArray().Select(s => s.GetString()!).ToList();
			var distractors = mission.Params["distractors"].GetInt32();
			var image = mission.Params["image"].GetString()!;
			return name.Count == 0
				? Result<(List<string>, List<string>, int, string)>.Fail($"{mission.Id}: a Missão não tem sílabas.")
				: Result<(List<string>, List<string>, int, string)>.Ok((name, pool, distractors, image));
		}
		catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
		{
			return Result<(List<string>, List<string>, int, string)>.Fail($"{mission.Id}: parâmetros de formar palavras inválidos ({e.Message}).");
		}
	}
}
