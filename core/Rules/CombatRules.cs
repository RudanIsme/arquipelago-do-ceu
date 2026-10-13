namespace ArquipelagoDoCeu.Rules;

// Bolsa do Matemático e tempo da montagem; todo valor vem de dados.
public sealed record CombatRules(int HandSize, IReadOnlyList<int> BagNumbers, int BagCopies, float SlowTimeScale);

sealed record CombatDto(int? HandSize, int[]? BagNumbers, int? BagCopies, float? SlowTimeScale);

static class CombatChecks
{
	const int HandLimit = 9;
	const int PieceLimit = 99;
	const int CopiesLimit = 9;
	const float MinSlowScale = 0.05f;

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
		return null;
	}

	public static CombatRules Build(CombatDto combat) =>
		new(combat.HandSize!.Value, [.. combat.BagNumbers!], combat.BagCopies!.Value, combat.SlowTimeScale!.Value);

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";

	static string OutOfRange(string field, string range, object? value) => $"Regras inválidas: '{field}' deve estar entre {range} (veio {value}).";
}
