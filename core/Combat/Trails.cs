using System.Numerics;
using System.Text.Json;
using ArquipelagoDoCeu.Rules;
using Json.Schema;

namespace ArquipelagoDoCeu.Combat;

// Um traço de um glifo: a sequência de pontos, de 0 a 1 dentro do quadrado do glifo, na ordem de escrita.
// O primeiro ponto é o início numerado e a direção do primeiro trecho é a seta.
public sealed record TrailStroke(IReadOnlyList<Vector2> Points);

public sealed record GlyphTrail(string Glyph, IReadOnlyList<TrailStroke> Strokes);

// Um traço já na área de desenho, em pixels, e quais dos pontos-guia (a partir de `FirstDot`) ele guarda.
public sealed record TrailPath(IReadOnlyList<Vector2> Points, int FirstDot, int DotCount);

// As trilhas de traço do jogo (game/data/trilhas.json), validadas na carga contra o schema.
// Trilha inválida vira resultado de falha, nunca exceção: quem chama segue sem ela.
public sealed class TraceTrails
{
	// O glifo ocupa este tanto do lado menor da área de desenho.
	const float Fill = 0.86f;
	const float MinStrokeLength = 0.05f;

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	readonly IReadOnlyDictionary<string, GlyphTrail> byGlyph;

	TraceTrails(IReadOnlyDictionary<string, GlyphTrail> byGlyph) => this.byGlyph = byGlyph;

	public static TraceTrails Empty => new(new Dictionary<string, GlyphTrail>());

	public IEnumerable<string> Glyphs => byGlyph.Keys;

	public GlyphTrail? Find(string glyph) => byGlyph.GetValueOrDefault(glyph.ToUpperInvariant());

	public static Result<TraceTrails> Parse(string json, string schemaJson)
	{
		var schema = Schema(schemaJson);
		if (!schema.IsOk) return Result<TraceTrails>.Fail(schema.Error!);
		try
		{
			using var document = JsonDocument.Parse(json);
			var evaluation = schema.Value!.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
			if (!evaluation.IsValid) return Result<TraceTrails>.Fail($"As trilhas de traço não seguem o schema: {FirstError(evaluation)}");
			return Build(document.RootElement.Deserialize<FileDto>(Options)!);
		}
		catch (JsonException e)
		{
			return Result<TraceTrails>.Fail($"As trilhas de traço não são um JSON válido ({e.Message}).");
		}
	}

	// Os pontos-guia de um glifo na área de desenho: um a cada `DotSpacing`, traço por traço, na ordem de escrita.
	public Result<TraceGuide> Guide(string glyph, Vector2 canvas, TracingRules rules)
	{
		if (Find(glyph) is not { } trail) return Result<TraceGuide>.Fail($"Não há trilha para '{glyph}'.");

		var side = Math.Min(canvas.X, canvas.Y) * Fill;
		var origin = (canvas - new Vector2(side, side)) / 2f;
		var dots = new List<Vector2>();
		var paths = new List<TrailPath>();
		foreach (var stroke in trail.Strokes)
		{
			var points = stroke.Points.Select(p => origin + p * side).ToList();
			var sampled = Sample(points, rules.DotSpacing);
			paths.Add(new TrailPath(points, dots.Count, sampled.Count));
			dots.AddRange(sampled);
		}
		return Result<TraceGuide>.Ok(new TraceGuide(dots, paths));
	}

	// Os glifos de `text` lado a lado, cada um na sua célula, para mostrar uma conta ou uma sílaba. Sem trilha (espaço, letra rara) é pulado.
	public Result<TraceGuide> GuideRow(string text, Vector2 canvas, TracingRules rules)
	{
		var glyphs = Letters(text).Where(g => Find(g) is not null).ToList();
		if (glyphs.Count == 0) return Result<TraceGuide>.Fail($"Não há trilha para {text}.");

		var cell = Math.Min(canvas.X / glyphs.Count, canvas.Y);
		var left = (canvas.X - cell * glyphs.Count) / 2f;
		var dots = new List<Vector2>();
		var paths = new List<TrailPath>();
		for (var i = 0; i < glyphs.Count; i++)
		{
			var guide = Guide(glyphs[i], new Vector2(cell, canvas.Y), rules).Value!;
			var shift = new Vector2(left + i * cell, 0f);
			paths.AddRange(guide.Paths!.Select(p => new TrailPath([.. p.Points.Select(q => q + shift)], p.FirstDot + dots.Count, p.DotCount)));
			dots.AddRange(guide.Dots.Select(d => d + shift));
		}
		return Result<TraceGuide>.Ok(new TraceGuide(dots, paths));
	}

	// Sem acento nem cedilha: o traço é do corpo da letra.
	static IEnumerable<string> Letters(string text) =>
		text.Normalize(System.Text.NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).Select(c => c.ToString());

	// Um ponto no início, um a cada `spacing` pixels ao longo do traço e o último na ponta.
	static List<Vector2> Sample(IReadOnlyList<Vector2> points, float spacing)
	{
		var sampled = new List<Vector2> { points[0] };
		var carried = 0f;
		for (var i = 1; i < points.Count; i++)
		{
			var length = Vector2.Distance(points[i - 1], points[i]);
			for (var at = spacing - carried; at <= length; at += spacing)
				sampled.Add(Vector2.Lerp(points[i - 1], points[i], at / length));
			carried = (carried + length) % spacing;
		}
		if (Vector2.Distance(sampled[^1], points[^1]) > spacing / 3f) sampled.Add(points[^1]);
		return sampled;
	}

	static Result<TraceTrails> Build(FileDto file)
	{
		var trails = new Dictionary<string, GlyphTrail>();
		foreach (var glyph in file.Glyphs)
		{
			var strokes = glyph.Strokes.Select(s => new TrailStroke([.. s.Select(p => new Vector2((float)p[0], (float)p[1]))])).ToList();
			if (strokes.Any(s => Length(s) < MinStrokeLength)) return Result<TraceTrails>.Fail($"A trilha de '{glyph.Glyph}' tem um traço curto demais.");
			if (!trails.TryAdd(glyph.Glyph.ToUpperInvariant(), new GlyphTrail(glyph.Glyph.ToUpperInvariant(), strokes))) return Result<TraceTrails>.Fail($"A trilha de '{glyph.Glyph}' está repetida.");
		}
		return Result<TraceTrails>.Ok(new TraceTrails(trails));
	}

	static float Length(TrailStroke stroke) => stroke.Points.Zip(stroke.Points.Skip(1), Vector2.Distance).Sum();

	static Result<JsonSchema> Schema(string schemaJson)
	{
		try
		{
			return Result<JsonSchema>.Ok(JsonSchema.FromText(schemaJson, new BuildOptions { SchemaRegistry = new SchemaRegistry() }));
		}
		catch (Exception e) when (e is JsonException or ArgumentException)
		{
			return Result<JsonSchema>.Fail($"O schema das trilhas de traço não é válido: {e.Message}");
		}
	}

	static string FirstError(EvaluationResults results) =>
		(results.Details ?? []).SelectMany(d => (d.Errors ?? new Dictionary<string, string>()).Select(e => $"{d.InstanceLocation}: {e.Value}")).FirstOrDefault() ?? "formato inesperado";

	sealed record GlyphDto(string Glyph, double[][][] Strokes);

	sealed record FileDto(GlyphDto[] Glyphs);
}
