using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

public enum GuessKind { Sure, Ambiguous, Unknown }

// O palpite sobre um símbolo. `Other` é o segundo colocado quando a dúvida é entre dois (por exemplo 1 e 7).
public sealed record GlyphGuess(GuessKind Kind, string? Glyph, string? Other = null);

// O palpite sobre o desenho todo: `Text` tem um caractere por símbolo ("?" onde não deu para ler).
public sealed record TextGuess(GuessKind Kind, string Text);

// Reconhece um símbolo comparando o traço com as trilhas em JSON, sem IA e sem rede; ordem e direção dos traços não importam.
// Na dúvida entre dois glifos, ou longe de todos, nunca escolhe: quem chama pede para refazer, sem custo.
public sealed class GlyphRecognizer
{
	readonly Dictionary<string, Vector2[]> templates = [];
	readonly FreeDrawRules rules;

	public GlyphRecognizer(TraceTrails trails, FreeDrawRules rules)
	{
		this.rules = rules;
		foreach (var glyph in trails.Glyphs)
			if (Cloud([.. trails.Find(glyph)!.Strokes.Select(s => s.Points)]) is { } cloud) templates[glyph] = cloud;
	}

	// `allowed` são os caracteres que a Missão aceita: o 0 e a letra O têm a mesma forma, e quem decide é o que se pede.
	public GlyphGuess Recognize(IReadOnlyList<IReadOnlyList<Vector2>> strokes, string allowed)
	{
		var ranked = Rank(strokes, allowed);
		if (ranked.Count == 0) return new GlyphGuess(GuessKind.Unknown, null);
		var best = ranked[0];
		if (best.Distance > rules.MaxDistance) return new GlyphGuess(GuessKind.Unknown, null);
		if (ranked.Count > 1 && best.Distance > ranked[1].Distance * rules.AmbiguityRatio)
			return new GlyphGuess(GuessKind.Ambiguous, best.Glyph, ranked[1].Glyph);
		return new GlyphGuess(GuessKind.Sure, best.Glyph);
	}

	// Os glifos permitidos do mais parecido ao menos, com a distância de cada um.
	public IReadOnlyList<(string Glyph, float Distance)> Rank(IReadOnlyList<IReadOnlyList<Vector2>> strokes, string allowed)
	{
		var drawn = Cloud(strokes);
		var candidates = allowed.Select(c => c.ToString().ToUpperInvariant()).Distinct().Where(templates.ContainsKey).ToList();
		if (drawn is null) return [];
		return [.. candidates.Select(g => (Glyph: g, Distance: Distance(drawn, templates[g]))).OrderBy(r => r.Distance)];
	}

	// Lê o desenho símbolo por símbolo. Basta um símbolo duvidoso para o desenho todo pedir outra tentativa.
	public TextGuess Read(DrawnReading reading, string allowed)
	{
		var guesses = reading.Symbols.Select(s => Recognize(s.Strokes, allowed)).ToList();
		var text = string.Concat(guesses.Select(g => g.Kind == GuessKind.Sure ? g.Glyph : "?"));
		var kind = guesses.Any(g => g.Kind == GuessKind.Unknown) ? GuessKind.Unknown : guesses.Any(g => g.Kind == GuessKind.Ambiguous) ? GuessKind.Ambiguous : GuessKind.Sure;
		return new TextGuess(guesses.Count == 0 ? GuessKind.Unknown : kind, text);
	}

	// Pontos espalhados pelo traço todo, com o glifo escalado para ter lado 1 e centrado na origem.
	Vector2[]? Cloud(IReadOnlyList<IReadOnlyList<Vector2>> strokes)
	{
		var total = strokes.Sum(Length);
		if (total <= 0f) return null;

		var dense = new List<Vector2>();
		var step = total / (rules.Points * 4f);
		foreach (var stroke in strokes.Where(s => s.Count > 1)) dense.AddRange(Densify(stroke, step));

		var picked = Enumerable.Range(0, rules.Points).Select(i => dense[i * (dense.Count - 1) / (rules.Points - 1)]).ToArray();
		var min = new Vector2(picked.Min(p => p.X), picked.Min(p => p.Y));
		var max = new Vector2(picked.Max(p => p.X), picked.Max(p => p.Y));
		var scale = Math.Max(max.X - min.X, max.Y - min.Y);
		if (scale <= 0f) return null;
		var center = (min + max) / 2f;
		return [.. picked.Select(p => (p - center) / scale)];
	}

	static IEnumerable<Vector2> Densify(IReadOnlyList<Vector2> stroke, float step)
	{
		for (var i = 0; i < stroke.Count; i++)
		{
			yield return stroke[i];
			if (i == stroke.Count - 1) continue;
			var length = Vector2.Distance(stroke[i], stroke[i + 1]);
			for (var d = step; d < length; d += step)
				yield return Vector2.Lerp(stroke[i], stroke[i + 1], d / length);
		}
	}

	static float Length(IReadOnlyList<Vector2> stroke) => stroke.Zip(stroke.Skip(1), Vector2.Distance).Sum();

	static float Distance(Vector2[] a, Vector2[] b) => (Worst(a, b) + Worst(b, a)) / 2f;

	// A média do quarto mais distante: um pedaço que falta no desenho (a barra do H num M) pesa mais que o tremor do traço.
	static float Worst(Vector2[] from, Vector2[] to)
	{
		var far = from.Select(p => to.Min(q => Vector2.Distance(p, q))).OrderByDescending(d => d).Take(Math.Max(1, from.Length / 4));
		return far.Average();
	}
}
