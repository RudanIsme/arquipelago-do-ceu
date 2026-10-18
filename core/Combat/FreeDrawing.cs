using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// Os traços de um símbolo (um número, um sinal, uma letra), em pixels da tela.
public sealed record DrawnSymbol(IReadOnlyList<IReadOnlyList<Vector2>> Strokes);

// O desenho fechado, com os símbolos da esquerda para a direita.
public sealed record DrawnReading(IReadOnlyList<DrawnSymbol> Symbols);

// O desenho livre: a criança escreve onde quiser e o fim do símbolo é levantar o dedo e esperar `IdleSeconds`.
// `IsActive` é o que o combate usa para desacelerar o mundo; ele fica ligado do primeiro toque até a leitura.
public sealed class FreeDrawing
{
	readonly FreeDrawRules rules;
	readonly List<List<Vector2>> strokes = [];
	bool down;
	float idle;

	public FreeDrawing(FreeDrawRules rules) => this.rules = rules;

	public bool IsActive => down || strokes.Count > 0;

	public bool IsDown => down;

	public IReadOnlyList<IReadOnlyList<Vector2>> Strokes => strokes;

	public void Down(Vector2 point)
	{
		down = true;
		idle = 0f;
		strokes.Add([point]);
	}

	public void Move(Vector2 point)
	{
		if (down) strokes[^1].Add(point);
	}

	public void Up()
	{
		down = false;
		idle = 0f;
	}

	// Um toque curto não é traço: some sem deixar o desenho ativo.
	public void Discard()
	{
		if (strokes.Count > 0) strokes.RemoveAt(strokes.Count - 1);
		down = false;
		idle = 0f;
	}

	public void Clear()
	{
		down = false;
		idle = 0f;
		strokes.Clear();
	}

	// Devolve o desenho quando a espera acabou; com o dedo apoiado o relógio não corre.
	public DrawnReading? Tick(float seconds)
	{
		if (down || strokes.Count == 0) return null;
		idle += seconds;
		if (idle < rules.IdleSeconds) return null;

		var reading = new DrawnReading(StrokeGrouping.Split(strokes, rules));
		Clear();
		return reading;
	}
}

// Separa os traços em símbolos pela posição: traços que se cobrem na horizontal são o mesmo símbolo (o "+", o "4", o "B"),
// e o que está ao lado é outro. Risco bem menor que o maior traço é acento e não entra.
static class StrokeGrouping
{
	sealed record Box(IReadOnlyList<Vector2> Stroke, float MinX, float MaxX, float Size);

	public static IReadOnlyList<DrawnSymbol> Split(IReadOnlyList<IReadOnlyList<Vector2>> strokes, FreeDrawRules rules)
	{
		var boxes = strokes.Where(s => s.Count > 1).Select(Measure).ToList();
		if (boxes.Count == 0) return [];

		var biggest = boxes.Max(b => b.Size);
		var kept = boxes.Where(b => b.Size >= biggest * rules.MarkRatio).OrderBy(b => b.MinX).ToList();
		var clusters = new List<List<Box>>();
		foreach (var box in kept)
		{
			var last = clusters.Count == 0 ? null : clusters[^1];
			if (last is not null && Overlaps(last, box, rules.MergeOverlap, biggest)) last.Add(box);
			else clusters.Add([box]);
		}
		return [.. clusters.Select(c => new DrawnSymbol([.. c.Select(b => b.Stroke)]))];
	}

	static Box Measure(IReadOnlyList<Vector2> stroke)
	{
		var min = new Vector2(stroke.Min(p => p.X), stroke.Min(p => p.Y));
		var max = new Vector2(stroke.Max(p => p.X), stroke.Max(p => p.Y));
		return new Box(stroke, min.X, max.X, Vector2.Distance(min, max));
	}

	// Um traço fino e reto (o "1", o lado de um "H") cabe se está dentro do símbolo, com uma folga pequena.
	static bool Overlaps(List<Box> cluster, Box box, float fraction, float biggest)
	{
		var minX = cluster.Min(b => b.MinX);
		var maxX = cluster.Max(b => b.MaxX);
		var slack = biggest * 0.05f;
		var overlap = Math.Min(maxX, box.MaxX) - Math.Max(minX, box.MinX);
		var clusterWidth = maxX - minX;
		var boxWidth = box.MaxX - box.MinX;
		if (boxWidth <= slack) return box.MinX >= minX - slack && box.MaxX <= maxX + slack;
		if (clusterWidth <= slack) return minX >= box.MinX - slack && maxX <= box.MaxX + slack;
		return overlap >= Math.Min(clusterWidth, boxWidth) * fraction;
	}
}
