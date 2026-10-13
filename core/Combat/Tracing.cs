using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// Os pontos pontilhados que a criança liga ao desenhar um número ou uma letra.
public sealed record TraceGuide(IReadOnlyList<Vector2> Dots);

public sealed record TraceCheck(float Coverage, float Stray, bool Passed);

// O traço é conferido em código, contra o próprio guia: quanto dos pontos a criança tocou e quanto do traço
// ficou longe de qualquer ponto. Não reconhece o desenho; só confere se ela seguiu o pontilhado.
public static class Tracing
{
	// O traço pode sair um pouco do miolo sem contar como rabisco.
	const float StrayFactor = 1.6f;

	// `mask[x, y]` é verdadeiro onde o desenho da letra tem tinta. Os pontos ficam em grade e só onde há tinta
	// ao redor, para seguirem o miolo do traço e não a borda.
	public static Result<TraceGuide> GuideFromMask(bool[,] mask, TracingRules rules)
	{
		var width = mask.GetLength(0);
		var height = mask.GetLength(1);
		var margin = Math.Max(1, rules.DotSpacing / 3);
		var dots = new List<Vector2>();
		for (var y = margin; y < height - margin; y += rules.DotSpacing)
			for (var x = margin; x < width - margin; x += rules.DotSpacing)
				if (mask[x, y] && mask[x - margin, y] && mask[x + margin, y] && mask[x, y - margin] && mask[x, y + margin])
					dots.Add(new Vector2(x, y));

		return dots.Count < 3
			? Result<TraceGuide>.Fail("Não foi possível desenhar o pontilhado dessa letra.")
			: Result<TraceGuide>.Ok(new TraceGuide(dots));
	}

	// `strokes` são os traços da criança, cada um como a sequência de pontos do ponteiro.
	public static TraceCheck Check(TraceGuide guide, IReadOnlyList<IReadOnlyList<Vector2>> strokes, TracingRules rules)
	{
		var points = strokes.SelectMany(s => Densify(s, rules.Tolerance / 2f)).ToList();
		if (points.Count == 0) return new TraceCheck(0f, 0f, false);

		var touched = guide.Dots.Count(dot => points.Any(p => Vector2.Distance(p, dot) <= rules.Tolerance));
		var stray = points.Count(p => guide.Dots.All(dot => Vector2.Distance(p, dot) > rules.Tolerance * StrayFactor));
		var coverage = (float)touched / guide.Dots.Count;
		var strayShare = (float)stray / points.Count;
		return new TraceCheck(coverage, strayShare, coverage >= rules.MinCoverage && strayShare <= rules.MaxStray);
	}

	// O ponteiro anda aos saltos; sem completar os saltos, um traço rápido pareceria não tocar nos pontos do meio.
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
}
