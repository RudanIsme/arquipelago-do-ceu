using System.Globalization;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// O que a criatura pede: um resultado, que a criança alcança com uma conta ("faça 8"), ou uma conta pronta ("3 + 5 = ?"),
// que ela responde só com o número.
public enum MathAsk { Result, Answer }

public enum DrawnKind { Right, Wrong, NeedsOperation, NumberOnly, ZeroTerm, SignNotAllowed, TooManyTerms, Malformed }

// `Right` e `Wrong` são respostas (a segunda custa 1 de Energia); o resto é a conta que o jogo recusa, sem custo, com o motivo em `Message`.
public sealed record DrawnVerdict(DrawnKind Kind, string Message, int? Total = null, string Equation = "")
{
	public bool IsAnswer => Kind is DrawnKind.Right or DrawnKind.Wrong;
}

// Lê o que a criança desenhou ("3+5") e calcula em código; a conta nunca vem de fora. O desenho é só texto de dígitos e sinais.
public static class DrawnMath
{
	const int MaxDigits = 4;
	const string Digits = "0123456789";

	public static string Allowed(MathAsk ask, MathLevel level) => ask == MathAsk.Answer ? Digits : Digits + string.Concat(level.Operators);

	public static DrawnVerdict Judge(string drawn, int target, MathAsk ask, MathLevel level) =>
		ask == MathAsk.Answer ? JudgeAnswer(drawn, target) : JudgeResult(drawn, target, level);

	static DrawnVerdict JudgeAnswer(string drawn, int target)
	{
		if (drawn.Length == 0 || drawn.Any(c => !char.IsAsciiDigit(c)) || drawn.Length > MaxDigits)
			return drawn.Any(c => c is '+' or '-') ? Refuse(DrawnKind.NumberOnly, "Aqui é só o número da resposta, sem sinais.") : Refuse(DrawnKind.Malformed, "Escreva o número da resposta.");

		var value = int.Parse(drawn, CultureInfo.InvariantCulture);
		return new DrawnVerdict(value == target ? DrawnKind.Right : DrawnKind.Wrong, "", value, drawn);
	}

	static DrawnVerdict JudgeResult(string drawn, int target, MathLevel level)
	{
		var read = Tokenize(drawn);
		if (read is null) return Refuse(DrawnKind.Malformed, "Escreva números e sinais, como 3 + 5.");
		if (read.Count == 1 && !read[0].IsOperator) return Refuse(DrawnKind.NeedsOperation, "Faça uma conta que dê o resultado. O número sozinho não vale.");
		if (read.Count % 2 == 0 || read.Where((t, i) => t.IsOperator != (i % 2 == 1)).Any())
			return Refuse(DrawnKind.Malformed, "Escreva um número de cada lado do sinal, como 3 + 5.");

		var sign = read.FirstOrDefault(t => t.IsOperator && !level.Operators.Contains(t.ToString()));
		if (sign.IsOperator) return Refuse(DrawnKind.SignNotAllowed, sign.Kind == TokenKind.Minus ? "Neste Nível ainda não usamos o sinal de menos." : "Neste Nível ainda não usamos o sinal de mais.");

		var terms = read.Where(t => !t.IsOperator).ToList();
		if (terms.Count > level.MaxTerms) return Refuse(DrawnKind.TooManyTerms, $"Neste Nível a conta tem até {level.MaxTerms} números.");
		if (terms.Any(t => t.Value == 0)) return Refuse(DrawnKind.ZeroTerm, "O zero não entra na conta, porque somar ou tirar zero não muda nada. Use outro número.");

		var total = Total(read);
		var equation = $"{string.Join(" ", read)} = {total}";
		return new DrawnVerdict(total == target ? DrawnKind.Right : DrawnKind.Wrong, "", (int)total, equation);
	}

	// Uma conta de exemplo para a Dica, que dá `target` e respeita o Nível; nula quando não há (o 1 só com "+").
	public static string? Example(int target, MathLevel level, Pcg32 rng)
	{
		var candidates = new List<string>();
		for (var a = 1; a < target; a++) candidates.Add($"{a}+{target - a}");
		if (level.Operators.Contains("-")) candidates.AddRange(Enumerable.Range(1, 9).Select(b => $"{target + b}-{b}"));
		if (level.MaxTerms >= 3)
			for (var a = 1; a < target - 1; a++) candidates.Add($"{a}+{(target - a) / 2}+{target - a - (target - a) / 2}");
		candidates = [.. candidates.Where(c => DrawnMath.Judge(c, target, MathAsk.Result, level).Kind == DrawnKind.Right)];
		return candidates.Count == 0 ? null : candidates[rng.NextInt(candidates.Count)];
	}

	static DrawnVerdict Refuse(DrawnKind kind, string message) => new(kind, message);

	static long Total(List<Token> tokens)
	{
		long total = 0;
		var subtract = false;
		foreach (var token in tokens)
		{
			if (token.IsOperator) subtract = token.Kind == TokenKind.Minus;
			else total += subtract ? -token.Value : token.Value;
		}
		return total;
	}

	// Dígitos seguidos são um número só. Devolve null se há algo que não é dígito nem sinal, ou número grande demais.
	static List<Token>? Tokenize(string drawn)
	{
		var tokens = new List<Token>();
		var number = new System.Text.StringBuilder();
		foreach (var c in drawn)
		{
			if (char.IsAsciiDigit(c)) number.Append(c);
			else if (c is '+' or '-')
			{
				Flush(tokens, number);
				tokens.Add(c == '+' ? Token.Plus : Token.Minus);
			}
			else return null;
			if (number.Length > MaxDigits) return null;
		}
		Flush(tokens, number);
		return tokens.Count == 0 ? null : tokens;
	}

	static void Flush(List<Token> tokens, System.Text.StringBuilder number)
	{
		if (number.Length > 0) tokens.Add(Token.Number(int.Parse(number.ToString(), CultureInfo.InvariantCulture)));
		number.Clear();
	}
}
