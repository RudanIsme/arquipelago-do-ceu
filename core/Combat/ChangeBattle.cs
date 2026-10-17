using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

public enum MoneyTask { Pay, Change }

// O que o jogo conferiu: o valor montado, o que se pedia e se bateu.
public sealed record ChangeResult(bool Correct, int Total, int Target);

// Um desafio de dinheiro: pagar um preço ou dar o troco de uma nota. Quem calcula o valor certo é o jogo, nunca a IA nem o JSON.
public sealed record ChangeChallenge(MoneyTask Task, int Price, int Given)
{
	// O valor que a criança monta: o preço, ou o que sobra da nota entregue.
	public int Target => Task == MoneyTask.Pay ? Price : Given - Price;

	public ChangeResult Check(IReadOnlyList<int> values) => new(values.Sum() == Target, values.Sum(), Target);

	// A compra do Mercador: com a menor nota que cobre o preço, a criança confere o troco; se o preço é a própria nota, paga exato.
	public static ChangeChallenge ForPurchase(int price, MoneyCatalog money)
	{
		var bill = money.Denominations.FirstOrDefault(d => d.Kind == "nota" && d.Value >= price);
		return bill is null || bill.Value == price ? new ChangeChallenge(MoneyTask.Pay, price, price) : new ChangeChallenge(MoneyTask.Change, price, bill.Value);
	}

	// A peça que a criança deve pôr agora, dado o que já está na mesa: a maior que ainda cabe e deixa o resto montável.
	// Se já passou do valor, ou nenhuma serve, a sugestão é desfazer. Pedir nunca custa nada, e seguir as sugestões sempre chega lá.
	public (int? Piece, bool Undo) Suggest(IReadOnlyList<int> table, IReadOnlyList<int> pieceValues)
	{
		var left = Target - table.Sum();
		if (left < 0) return (null, true);
		var next = Enumerable.Range(0, pieceValues.Count).Where(i => pieceValues[i] <= left).OrderByDescending(i => pieceValues[i])
			.Cast<int?>().FirstOrDefault(i => CanMake(left - pieceValues[i!.Value], pieceValues));
		return next is null ? (null, true) : (next, false);
	}

	// Se as peças montam o valor (cada peça pode ser usada quantas vezes quiser).
	public static bool CanMake(int amount, IReadOnlyList<int> pieceValues)
	{
		if (pieceValues.Count == 0) return false;
		var step = pieceValues.Aggregate(Gcd);
		var reach = new bool[amount / step + 1];
		reach[0] = true;
		for (var units = 1; units < reach.Length; units++)
			reach[units] = pieceValues.Any(v => v / step <= units && reach[units - v / step]);
		return amount % step == 0 && reach[amount / step];
	}

	static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}

// A luta "Pagar e dar troco": a criança monta um valor com moedas e cédulas do Real. A dica nunca custa Energia e a criança nunca
// fica presa: a sugestão sempre leva ao valor certo. O JSON só diz a tarefa, o preço e a nota; o troco é calculado aqui.
public sealed class ChangeBattle : IBattle
{
	public const string Mechanic = "pagar_troco";

	readonly IReadOnlyList<Mission> missions;
	readonly MoneyCatalog money;
	readonly Pcg32 rng;
	readonly MissionHistory? history;
	readonly List<int> table = [];
	List<Denomination> pieces = [];
	bool solved;

	ChangeBattle(IReadOnlyList<Mission> missions, MoneyCatalog money, Pcg32 rng, MissionHistory? history)
	{
		this.missions = missions;
		this.money = money;
		this.rng = rng;
		this.history = history;
		Mission = missions[0];
		Challenge = new ChangeChallenge(MoneyTask.Pay, 0, 0);
	}

	public Mission Mission { get; private set; }

	public ChangeChallenge Challenge { get; private set; }

	public MoneyTask Task => Challenge.Task;

	public int Price => Challenge.Price;

	public int Given => Challenge.Given;

	public int Target => Challenge.Target;

	public IReadOnlyList<Denomination> Pieces => pieces;

	// As peças postas na mesa, na ordem; cada uma é o índice em `Pieces`.
	public IReadOnlyList<Denomination> Table => [.. table.Select(i => pieces[i])];

	public int Total => table.Sum(i => pieces[i].Value);

	// Enquanto o valor não for montado certo falta 1 acerto; é o "escudo" desta luta.
	public int Remaining => solved ? 0 : 1;

	public static Result<ChangeBattle> Start(IEnumerable<Mission> available, MoneyCatalog money, Pcg32 rng, MissionHistory? history = null)
	{
		var usable = available.Where(m => m.Mechanic == Mechanic).ToList();
		if (usable.Count == 0) return Result<ChangeBattle>.Fail("Não há Missão de pagar e dar troco.");

		var battle = new ChangeBattle(usable, money, rng, history);
		var first = MissionHistory.Narrow(history, usable);
		var dealt = battle.Deal(first[rng.NextInt(first.Count)]);
		return dealt.IsOk ? Result<ChangeBattle>.Ok(battle) : Result<ChangeBattle>.Fail(dealt.Error!);
	}

	// Outro desafio, de outra Missão quando houver mais de uma. Missão mal escrita é pulada, nunca trava a luta.
	public void Next()
	{
		var others = missions.Where(m => m != Mission).ToList();
		var pool = MissionHistory.Narrow(history, others.Count > 0 ? others : missions);
		for (var attempt = 0; attempt < pool.Count; attempt++)
			if (Deal(pool[(rng.NextInt(pool.Count) + attempt) % pool.Count]).IsOk) return;
		Deal(Mission);
	}

	public bool Add(int piece)
	{
		if (piece < 0 || piece >= pieces.Count) return false;
		table.Add(piece);
		return true;
	}

	public bool Undo()
	{
		if (table.Count == 0) return false;
		table.RemoveAt(table.Count - 1);
		return true;
	}

	public void Clear() => table.Clear();

	public ChangeResult Confirm()
	{
		var result = Challenge.Check([.. table.Select(i => pieces[i].Value)]);
		solved |= result.Correct;
		return result;
	}

	// A peça que a criança deve pôr agora: a maior que ainda cabe no que falta. Se já passou do valor, a sugestão é desfazer.
	// Pedir nunca custa Energia, e seguir as sugestões sempre chega ao valor certo.
	public (int? Piece, bool Undo) Suggest()
	{
		return Challenge.Suggest([.. table.Select(i => pieces[i].Value)], [.. pieces.Select(p => p.Value)]);
	}

	public void Reveal()
	{
	}

	// A travessura do Boneco: embaralha a ordem das peças na mesa de dinheiro; nunca esconde dinheiro.
	public Prank PlayPrank()
	{
		var order = pieces.ToList();
		rng.Shuffle(order);
		pieces = order;
		table.Clear();
		return Prank.Shuffled;
	}

	Result<bool> Deal(Mission mission)
	{
		var parsed = Parse(mission);
		if (!parsed.IsOk) return Result<bool>.Fail(parsed.Error!);

		var (challenge, available) = parsed.Value;
		Mission = mission;
		history?.Add(mission.Id);
		Challenge = challenge;
		pieces = available;
		table.Clear();
		solved = false;
		return Result<bool>.Ok(true);
	}

	// Os parâmetros da Mecânica: a tarefa, o preço em centavos, a nota entregue (no troco) e, se quiser, quais peças aparecem.
	Result<(ChangeChallenge, List<Denomination>)> Parse(Mission mission)
	{
		try
		{
			var p = mission.Params;
			var task = p["task"].GetString() switch { "pagar" => MoneyTask.Pay, "troco" => MoneyTask.Change, _ => (MoneyTask?)null };
			var price = p["price"].GetInt32();
			var given = p.TryGetValue("given", out var g) ? g.GetInt32() : price;
			var problem = task is null ? "a tarefa precisa ser 'pagar' ou 'troco'"
				: price < money.Smallest || price % money.Smallest != 0 ? $"o preço precisa ser múltiplo da menor moeda ({MoneyCatalog.Format(money.Smallest)})"
				: task == MoneyTask.Change && given <= price ? "a nota entregue precisa ser maior que o preço"
				: task == MoneyTask.Change && given % money.Smallest != 0 ? "a nota entregue precisa ser múltiplo da menor moeda"
				: null;
			if (problem is not null) return Result<(ChangeChallenge, List<Denomination>)>.Fail($"{mission.Id}: {problem}.");

			var available = Available(p);
			var challenge = new ChangeChallenge(task!.Value, price, task == MoneyTask.Change ? given : price);
			return available.Count == 0 || !ChangeChallenge.CanMake(challenge.Target, [.. available.Select(d => d.Value)])
				? Result<(ChangeChallenge, List<Denomination>)>.Fail($"{mission.Id}: as peças de dinheiro não montam o valor pedido.")
				: Result<(ChangeChallenge, List<Denomination>)>.Ok((challenge, available));
		}
		catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
		{
			return Result<(ChangeChallenge, List<Denomination>)>.Fail($"{mission.Id}: parâmetros de pagar e dar troco inválidos ({e.Message}).");
		}
	}

	List<Denomination> Available(IReadOnlyDictionary<string, System.Text.Json.JsonElement> p)
	{
		if (!p.TryGetValue("pieces", out var list)) return [.. money.Denominations];
		var wanted = list.EnumerateArray().Select(v => v.GetInt32()).ToList();
		var chosen = money.Denominations.Where(d => wanted.Contains(d.Value)).ToList();
		// Uma peça pedida que não existe no Real deixa a Missão inválida, em vez de sumir sem avisar.
		return chosen.Count == wanted.Distinct().Count() ? chosen : [];
	}
}

// A Mecânica "Pagar e dar troco" no registro: só reconhece filas em que toda Missão é de dinheiro.
public sealed class ChangeMechanic(MoneyCatalog money) : IBattleMechanic
{
	public string Id => "troco";

	public bool Handles(IReadOnlyList<Mission> pool) => pool.Count > 0 && pool.All(m => m.Mechanic == ChangeBattle.Mechanic);

	public Result<IBattle> Start(CombatRules rules, IReadOnlyList<Mission> pool, Pcg32 rng, MissionHistory? history) =>
		BattleStart.Wrap(ChangeBattle.Start(pool, money, rng, history));
}
