using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// A bolsa de números do Matemático, como um baralho: mão pequena, descarte que volta embaralhado, ordem só pela seed.
// Slot vazio (null) é peça que está no cajado e ainda pode ser desfeita.
public sealed class MathBag
{
	readonly Pcg32 rng;
	readonly List<int> pile = [];
	readonly List<int> discard = [];
	readonly int?[] hand;
	readonly int?[] hidden;

	public MathBag(CombatRules rules, Pcg32 rng)
	{
		this.rng = rng;
		foreach (var number in rules.BagNumbers)
			for (var copy = 0; copy < rules.BagCopies; copy++)
				pile.Add(number);
		rng.Shuffle(pile);

		hand = new int?[rules.HandSize];
		hidden = new int?[rules.HandSize];
		for (var slot = 0; slot < hand.Length; slot++) hand[slot] = Draw();
	}

	public IReadOnlyList<int?> Hand => hand;

	public int HiddenCount => hidden.Count(p => p is not null);

	public bool IsHidden(int slot) => hidden[slot] is not null;

	// Travessura do Boneco: embaralha as peças da mão ou esconde uma, que volta com Reveal.
	// Peças que estão no cajado ficam onde estão: é a criança quem decide o que fazer com elas.
	public Prank PlayPrank()
	{
		var present = Enumerable.Range(0, hand.Length).Where(slot => hand[slot] is not null).ToList();
		if (present.Count > 1 && rng.NextInt(2) == 0)
		{
			var pieces = present.Select(slot => hand[slot]).ToList();
			rng.Shuffle(pieces);
			for (var i = 0; i < present.Count; i++) hand[present[i]] = pieces[i];
			return Prank.Shuffled;
		}
		if (present.Count == 0) return Prank.Shuffled;

		var chosen = present[rng.NextInt(present.Count)];
		hidden[chosen] = hand[chosen];
		hand[chosen] = null;
		return Prank.Hidden;
	}

	public void Reveal()
	{
		for (var slot = 0; slot < hand.Length; slot++)
		{
			if (hidden[slot] is not { } piece) continue;
			hand[slot] = piece;
			hidden[slot] = null;
		}
	}

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
			if (hand[slot] is null && hidden[slot] is null) hand[slot] = Draw();
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

// O cajado: onde a criança monta a conta antes de confirmar. Só Confirm gasta as peças e avalia.
// Quem soma é a criança: o total nunca aparece antes da confirmação.
public sealed class Staff
{
	readonly MathBag bag;
	readonly IReadOnlyList<string> operators;
	readonly List<(int Slot, Token Token)> placed = [];

	public Staff(MathBag bag, CombatRules rules)
	{
		this.bag = bag;
		operators = rules.Operators;
	}

	public IReadOnlyList<Token> Tokens => placed.Select(p => p.Token).ToList();

	// Um número vem depois de um sinal (ou abre a conta); um sinal vem depois de um número.
	public bool CanPlaceNumber => placed.Count == 0 || placed[^1].Token.IsOperator;

	public bool CanPlaceOperator => placed.Count > 0 && placed[^1].Token.Kind == TokenKind.Number;

	public bool Place(int slot)
	{
		if (!CanPlaceNumber || bag.Take(slot) is not { } piece) return false;
		placed.Add((slot, Token.Number(piece)));
		return true;
	}

	public bool PlaceOperator(string symbol)
	{
		if (!CanPlaceOperator || !operators.Contains(symbol)) return false;
		placed.Add((-1, symbol == "-" ? Token.Minus : Token.Plus));
		return true;
	}

	public bool Undo()
	{
		if (placed.Count == 0) return false;
		var (slot, token) = placed[^1];
		placed.RemoveAt(placed.Count - 1);
		if (token.Kind == TokenKind.Number) bag.Return(slot, token.Value);
		return true;
	}

	public void Cancel()
	{
		var more = true;
		while (more) more = Undo();
	}

	public Result<ExpressionResult> Confirm(int shield)
	{
		var result = ExpressionEvaluator.Evaluate(Tokens, shield);
		if (!result.IsOk) return result;

		// Passar do valor não vale: a conta fica à vista no resultado, mas as peças voltam para a mão e a criança tenta outra combinação.
		if (result.Value!.Comparison == Comparison.Above)
		{
			Cancel();
			return result;
		}

		var used = placed.Where(p => p.Token.Kind == TokenKind.Number).Select(p => p.Token.Value).ToList();
		placed.Clear();
		bag.Spend(used);
		return result;
	}
}

// A travessura do Boneco quando a criança erra; nunca é violenta.
public enum Prank { Shuffled, Hidden }
