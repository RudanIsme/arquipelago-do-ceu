namespace ArquipelagoDoCeu.Combat;

public enum Comparison { Below, Equal, Above }

public sealed record ExpressionResult(int Total, Comparison Comparison);

public enum TokenKind { Number, Plus, Minus }

// Uma peça da conta: um número da bolsa ou um sinal da calculadora.
public readonly record struct Token(TokenKind Kind, int Value = 0)
{
	public static Token Number(int value) => new(TokenKind.Number, value);

	public static readonly Token Plus = new(TokenKind.Plus);

	public static readonly Token Minus = new(TokenKind.Minus);

	public bool IsOperator => Kind != TokenKind.Number;

	public override string ToString() => Kind switch { TokenKind.Plus => "+", TokenKind.Minus => "-", _ => Value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
}

// A conta é calculada aqui, em código, sobre as peças montadas: nenhum texto de fora vira conta.
// Uma conta válida é número, sinal, número... e tem ao menos um sinal; um número sozinho não é conta.
public static class ExpressionEvaluator
{
	public static Result<ExpressionResult> Evaluate(IReadOnlyList<Token> tokens, int shield)
	{
		if (shield < 1) return Result<ExpressionResult>.Fail("O escudo precisa valer ao menos 1.");
		if (tokens.Count < 3) return Result<ExpressionResult>.Fail("Monte uma conta com um sinal e pelo menos dois números.");

		long total = 0;
		var subtract = false;
		for (var i = 0; i < tokens.Count; i++)
		{
			var isNumberSlot = i % 2 == 0;
			if (tokens[i].IsOperator == isNumberSlot) return Result<ExpressionResult>.Fail("A conta precisa alternar número e sinal, e terminar em um número.");
			if (!isNumberSlot) subtract = tokens[i].Kind == TokenKind.Minus;
			else total += subtract ? -tokens[i].Value : tokens[i].Value;
		}
		if (tokens.Count % 2 == 0) return Result<ExpressionResult>.Fail("A conta precisa alternar número e sinal, e terminar em um número.");
		if (total < 1) return Result<ExpressionResult>.Fail("A conta não pode dar zero nem menos que zero. Tente outra.");
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
