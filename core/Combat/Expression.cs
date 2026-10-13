namespace ArquipelagoDoCeu.Combat;

public enum Comparison { Below, Equal, Above }

public sealed record ExpressionResult(int Total, Comparison Comparison);

// A conta é calculada aqui, em código, sobre as peças da bolsa: nenhum texto de fora vira conta.
// O sinal + entre as peças é fixo, então a conta é a soma das peças.
public static class ExpressionEvaluator
{
	public static Result<ExpressionResult> Evaluate(IReadOnlyList<int> pieces, int shield)
	{
		if (pieces.Count == 0) return Result<ExpressionResult>.Fail("Monte uma conta antes de confirmar.");
		if (shield < 1) return Result<ExpressionResult>.Fail("O escudo precisa valer ao menos 1.");

		long total = 0;
		foreach (var piece in pieces) total += piece;
		if (total > int.MaxValue) return Result<ExpressionResult>.Fail("A conta ficou grande demais.");

		var comparison = total < shield ? Comparison.Below : total == shield ? Comparison.Equal : Comparison.Above;
		return Result<ExpressionResult>.Ok(new ExpressionResult((int)total, comparison));
	}
}

public enum HitOutcome { Reduced, Dissolved, Overshoot }

// O número da criatura. Uma conta abaixo tira do escudo, a exata o dissolve, e passar do valor não vale.
public sealed class Shield
{
	public Shield(int value) => Remaining = value;

	public int Remaining { get; private set; }

	public bool IsDissolved => Remaining == 0;

	public HitOutcome Hit(ExpressionResult result)
	{
		switch (result.Comparison)
		{
			case Comparison.Above:
				return HitOutcome.Overshoot;
			case Comparison.Equal:
				Remaining = 0;
				return HitOutcome.Dissolved;
			default:
				Remaining -= result.Total;
				return HitOutcome.Reduced;
		}
	}
}
