using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

public enum GuessKind { Sure, Ambiguous, Unknown }

// O palpite sobre um símbolo. `Other` é o segundo colocado quando a dúvida é entre dois (por exemplo 1 e 7).
public sealed record GlyphGuess(GuessKind Kind, string? Glyph, string? Other = null);

// O palpite sobre o desenho todo: `Text` tem um caractere por símbolo ("?" onde não deu para ler).
public sealed record TextGuess(GuessKind Kind, string Text);

// Reconhece um símbolo comparando o traço com as trilhas em JSON, sem IA e sem rede, em todas as ordens e sentidos possíveis dos traços.
// Na dúvida entre dois glifos, ou longe de todos, nunca escolhe: quem chama pede para refazer, sem custo.
public sealed class GlyphRecognizer
{
	const int MaxPermuted = 4;

	readonly Dictionary<string, List<Vector2[]>> templates = [];
	readonly FreeDrawRules rules;

	public GlyphRecognizer(TraceTrails trails, FreeDrawRules rules)
	{
		this.rules = rules;
		foreach (var glyph in trails.Glyphs)
		{
			var variants = Variants([.. trails.Find(glyph)!.Strokes.Select(s => s.Points)]);
			if (variants.Count > 0) templates[glyph] = variants;
		}
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
		var drawn = Normalize(strokes);
		var candidates = allowed.Select(c => c.ToString().ToUpperInvariant()).Distinct().Where(templates.ContainsKey).ToList();
		if (drawn is null) return [];
		return [.. candidates.Select(g => (Glyph: g, Distance: templates[g].Min(v => Distance(drawn, v)))).OrderBy(r => r.Distance)];
	}

	// Lê o desenho símbolo por símbolo. Basta um símbolo duvidoso para o desenho todo pedir outra tentativa.
	public TextGuess Read(DrawnReading reading, string allowed)
	{
		var guesses = reading.Symbols.Select(s => Recognize(s.Strokes, allowed)).ToList();
		var text = string.Concat(guesses.Select(g => g.Kind == GuessKind.Sure ? g.Glyph : "?"));
		var kind = guesses.Any(g => g.Kind == GuessKind.Unknown) ? GuessKind.Unknown : guesses.Any(g => g.Kind == GuessKind.Ambiguous) ? GuessKind.Ambiguous : GuessKind.Sure;
		return new TextGuess(guesses.Count == 0 ? GuessKind.Unknown : kind, text);
	}

	// Todas as ordens e sentidos em que a criança pode ter feito os traços do glifo: o traço de um 5 pode começar pela barra ou pela barriga.
	List<Vector2[]> Variants(List<IReadOnlyList<Vector2>> strokes)
	{
		var variants = new List<Vector2[]>();
		var orders = strokes.Count <= MaxPermuted ? Permutations(Enumerable.Range(0, strokes.Count).ToList()) : [Enumerable.Range(0, strokes.Count).ToList()];
		var flips = strokes.Count <= MaxPermuted ? 1 << strokes.Count : 1;
		foreach (var order in orders)
			for (var mask = 0; mask < flips; mask++)
			{
				var chain = order.Select(i => Oriented(strokes[i], (mask & 1 << i) != 0)).ToList();
				if (Normalize(chain) is { } normalized) variants.Add(normalized);
			}
		return variants;
	}

	static IReadOnlyList<Vector2> Oriented(IReadOnlyList<Vector2> stroke, bool reversed) => reversed ? [.. stroke.Reverse()] : stroke;

	static IEnumerable<List<int>> Permutations(List<int> items)
	{
		if (items.Count <= 1)
		{
			yield return [.. items];
			yield break;
		}
		foreach (var item in items)
			foreach (var rest in Permutations([.. items.Where(i => i != item)]))
				yield return [item, .. rest];
	}

	// Os traços, na ordem, viram um caminho só (como o dedo se moveu), com pontos a distâncias iguais, glifo com lado 1 e centrado.
	Vector2[]? Normalize(IReadOnlyList<IReadOnlyList<Vector2>> strokes)
	{
		var path = strokes.Where(s => s.Count > 1).SelectMany(s => s).ToList();
		if (path.Count < 2) return null;
		var points = Resample(path, rules.Points);
		var min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
		var max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
		var scale = Math.Max(max.X - min.X, max.Y - min.Y);
		if (scale <= 0f) return null;
		var center = (min + max) / 2f;
		return [.. points.Select(p => (p - center) / scale)];
	}

	static Vector2[] Resample(List<Vector2> path, int count)
	{
		var total = path.Zip(path.Skip(1), Vector2.Distance).Sum();
		if (total <= 0f) return [.. Enumerable.Repeat(path[0], count)];

		var step = total / (count - 1);
		var points = new List<Vector2> { path[0] };
		var carried = 0f;
		for (var i = 1; i < path.Count && points.Count < count; i++)
		{
			var length = Vector2.Distance(path[i - 1], path[i]);
			while (length > 0f && carried + length >= step && points.Count < count)
			{
				var at = (step - carried) / length;
				var point = Vector2.Lerp(path[i - 1], path[i], at);
				points.Add(point);
				path[i - 1] = point;
				length = Vector2.Distance(point, path[i]);
				carried = 0f;
			}
			carried += length;
		}
		while (points.Count < count) points.Add(path[^1]);
		return [.. points];
	}

	// A média da distância entre pontos de mesma posição no caminho: a ordem do traço conta, e é isso que separa o 3 do 5 e do 8.
	static float Distance(Vector2[] a, Vector2[] b)
	{
		var sum = 0f;
		for (var i = 0; i < a.Length; i++) sum += Vector2.Distance(a[i], b[i]);
		return sum / a.Length;
	}
}
