using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// A bolsa de números do Matemático, como um baralho: a mão tem poucas peças, as usadas vão para o
// descarte e, quando a pilha acaba, o descarte é embaralhado de volta. A ordem vem só da seed.
// Slot vazio (null) é peça que está no cajado e ainda pode ser desfeita.
public sealed class MathBag
{
	readonly Pcg32 rng;
	readonly List<int> pile = [];
	readonly List<int> discard = [];
	readonly int?[] hand;

	public MathBag(CombatRules rules, Pcg32 rng)
	{
		this.rng = rng;
		foreach (var number in rules.BagNumbers)
			for (var copy = 0; copy < rules.BagCopies; copy++)
				pile.Add(number);
		rng.Shuffle(pile);

		hand = new int?[rules.HandSize];
		for (var slot = 0; slot < hand.Length; slot++) hand[slot] = Draw();
	}

	public IReadOnlyList<int?> Hand => hand;

	internal int? Take(int slot)
	{
		if (slot < 0 || slot >= hand.Length || hand[slot] is not { } piece) return null;
		hand[slot] = null;
		return piece;
	}

	internal void Return(int slot, int piece) => hand[slot] = piece;

	// Peças usadas num ataque: vão para o descarte e os slots recebem peças novas.
	internal void Spend(IEnumerable<int> used)
	{
		discard.AddRange(used);
		for (var slot = 0; slot < hand.Length; slot++)
			if (hand[slot] is null) hand[slot] = Draw();
	}


	int? Draw()
	{
		if (pile.Count == 0)
		{
			pile.AddRange(discard);
			discard.Clear();
			rng.Shuffle(pile);
		}
		if (pile.Count == 0) return null;
		var piece = pile[^1];
		pile.RemoveAt(pile.Count - 1);
		return piece;
	}
}

// O cajado: onde a criança monta a conta antes de confirmar. Montar e desfazer não são resposta;
// só Confirm gasta as peças e devolve a avaliação.
public sealed class Staff
{
	readonly MathBag bag;
	readonly List<(int Slot, int Piece)> placed = [];

	public Staff(MathBag bag) => this.bag = bag;

	public IReadOnlyList<int> Pieces => placed.Select(p => p.Piece).ToList();

	public int Total => placed.Sum(p => p.Piece);

	public bool Place(int slot)
	{
		if (bag.Take(slot) is not { } piece) return false;
		placed.Add((slot, piece));
		return true;
	}

	public bool Undo()
	{
		if (placed.Count == 0) return false;
		var (slot, piece) = placed[^1];
		placed.RemoveAt(placed.Count - 1);
		bag.Return(slot, piece);
		return true;
	}

	public void Cancel()
	{
		while (Undo()) { }
	}

	public Result<ExpressionResult> Confirm(int shield)
	{
		var result = ExpressionEvaluator.Evaluate(Pieces, shield);
		if (!result.IsOk) return result;

		// Passar do valor não vale: a conta fica à vista no resultado, mas as peças voltam para a mão e a criança tenta outra combinação.
		if (result.Value!.Comparison == Comparison.Above)
		{
			Cancel();
			return result;
		}

		var used = Pieces;
		placed.Clear();
		bag.Spend(used);
		return result;
	}
}
