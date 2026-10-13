namespace ArquipelagoDoCeu.Rules;

// Como a criança desenha um número ou uma letra: pontos-guia a cada `DotSpacing` px, `Tolerance` px de folga,
// `MinCoverage` dos pontos tocados e no máximo `MaxStray` do traço fora do guia. Tudo em pixels da área de desenho.
public sealed record TracingRules(int DotSpacing, float Tolerance, float MinCoverage, float MaxStray);

// Bolsa do Matemático, sinais da calculadora, tempo da montagem e traçado; todo valor vem de dados.
public sealed record CombatRules(int HandSize, IReadOnlyList<int> BagNumbers, int BagCopies, float SlowTimeScale, float TargetRange, IReadOnlyList<string> Operators, TracingRules Tracing);

sealed record TracingDto(int? DotSpacing, float? Tolerance, float? MinCoverage, float? MaxStray);

sealed record CombatDto(int? HandSize, int[]? BagNumbers, int? BagCopies, float? SlowTimeScale, float? TargetRange, string[]? Operators, TracingDto? Tracing);

static class CombatChecks
{
	const int HandLimit = 9;
	const int PieceLimit = 9; // o teclado digita uma tecla por peça; peças maiores esperam a digitação de vários dígitos
	const int CopiesLimit = 9;
	const float MinSlowScale = 0.05f;
	const float MaxTargetRange = 30f;
	static readonly string[] SupportedOperators = ["+"];

	public static string? Check(CombatDto? combat)
	{
		if (combat is null) return Required("combat");
		if (combat.HandSize is null) return Required("combat.handSize");
		if (combat.HandSize < 1 || combat.HandSize > HandLimit) return OutOfRange("combat.handSize", $"1 e {HandLimit}", combat.HandSize);
		if (combat.BagNumbers is null) return Required("combat.bagNumbers");
		if (combat.BagNumbers.Length == 0) return "Regras inválidas: 'combat.bagNumbers' precisa de ao menos uma peça.";
		if (combat.BagNumbers.Any(n => n < 1 || n > PieceLimit)) return OutOfRange("combat.bagNumbers", $"1 e {PieceLimit}", string.Join(", ", combat.BagNumbers));
		if (combat.BagNumbers.Distinct().Count() != combat.BagNumbers.Length) return "Regras inválidas: 'combat.bagNumbers' tem peças repetidas; use 'combat.bagCopies' para ter mais de uma.";
		if (combat.BagCopies is null) return Required("combat.bagCopies");
		if (combat.BagCopies < 1 || combat.BagCopies > CopiesLimit) return OutOfRange("combat.bagCopies", $"1 e {CopiesLimit}", combat.BagCopies);
		if (combat.HandSize > combat.BagNumbers.Length * combat.BagCopies)
			return $"Regras inválidas: 'combat.handSize' ({combat.HandSize}) não pode ser maior que o total de peças da bolsa ({combat.BagNumbers.Length * combat.BagCopies}).";
		if (combat.SlowTimeScale is null) return Required("combat.slowTimeScale");
		if (combat.SlowTimeScale < MinSlowScale || combat.SlowTimeScale > 1f) return OutOfRange("combat.slowTimeScale", $"{MinSlowScale} e 1", combat.SlowTimeScale);
		if (combat.TargetRange is null) return Required("combat.targetRange");
		if (combat.TargetRange < 1f || combat.TargetRange > MaxTargetRange) return OutOfRange("combat.targetRange", $"1 e {MaxTargetRange}", combat.TargetRange);
		if (combat.Operators is null) return Required("combat.operators");
		if (combat.Operators.Length == 0) return "Regras inválidas: 'combat.operators' precisa de ao menos um sinal.";
		if (combat.Operators.Any(o => !SupportedOperators.Contains(o)))
			return $"Regras inválidas: 'combat.operators' só aceita {string.Join(", ", SupportedOperators)} por enquanto (veio {string.Join(", ", combat.Operators)}).";
		return CheckTracing(combat.Tracing);
	}

	static string? CheckTracing(TracingDto? tracing)
	{
		if (tracing is null) return Required("combat.tracing");
		if (tracing.DotSpacing is null) return Required("combat.tracing.dotSpacing");
		if (tracing.DotSpacing < 4 || tracing.DotSpacing > 60) return OutOfRange("combat.tracing.dotSpacing", "4 e 60", tracing.DotSpacing);
		if (tracing.Tolerance is null) return Required("combat.tracing.tolerance");
		if (tracing.Tolerance < 4 || tracing.Tolerance > 120) return OutOfRange("combat.tracing.tolerance", "4 e 120", tracing.Tolerance);
		if (tracing.MinCoverage is null) return Required("combat.tracing.minCoverage");
		if (tracing.MinCoverage < 0.3f || tracing.MinCoverage > 1f) return OutOfRange("combat.tracing.minCoverage", "0,3 e 1", tracing.MinCoverage);
		if (tracing.MaxStray is null) return Required("combat.tracing.maxStray");
		if (tracing.MaxStray < 0f || tracing.MaxStray > 1f) return OutOfRange("combat.tracing.maxStray", "0 e 1", tracing.MaxStray);
		return null;
	}

	public static CombatRules Build(CombatDto combat) =>
		new(combat.HandSize!.Value, [.. combat.BagNumbers!], combat.BagCopies!.Value, combat.SlowTimeScale!.Value, combat.TargetRange!.Value, [.. combat.Operators!],
			new TracingRules(combat.Tracing!.DotSpacing!.Value, combat.Tracing.Tolerance!.Value, combat.Tracing.MinCoverage!.Value, combat.Tracing.MaxStray!.Value));

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";

	static string OutOfRange(string field, string range, object? value) => $"Regras inválidas: '{field}' deve estar entre {range} (veio {value}).";
}
