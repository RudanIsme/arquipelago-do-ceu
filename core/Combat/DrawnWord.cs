using System.Globalization;
using System.Text;

namespace ArquipelagoDoCeu.Combat;

// `Incomplete` é uma sílaba pela metade: espera o resto, sem custo. Só `Wrong` é resposta errada.
public enum WriteKind { Accepted, Incomplete, Wrong }

// `Parts` é quantas sílabas (ou letras) esse passo escreveu: a sílaba, ou várias se a palavra veio inteira.
public sealed record WriteStep(WriteKind Kind, int Parts);

// A palavra que a criança escreve desenhando, uma sílaba por vez (BO, depois LA) ou de uma só vez. O jogo só confere as letras
// reconhecidas contra as sílabas da Missão; acento e cedilha não têm traço e não contam, e maiúscula ou minúscula tanto faz.
public sealed class DrawnWord
{
	readonly IReadOnlyList<string> parts;
	readonly IReadOnlyList<string> plain;
	int done;

	public DrawnWord(IReadOnlyList<string> parts)
	{
		this.parts = parts;
		plain = [.. parts.Select(Plain)];
	}

	// Uma palavra sem sílabas marcadas vale letra a letra, e qualquer pedaço dela vale.
	public static DrawnWord ByLetters(string word) => new([.. word.Select(c => c.ToString())]);

	public bool IsDone => done >= parts.Count;

	// As letras já aceitas, em maiúsculas: é o que aparece escrito sobre a criatura.
	public string Written => string.Concat(parts.Take(done)).ToUpperInvariant();

	// A sílaba que ficou pela metade, esperando o resto.
	public string Pending { get; private set; } = "";

	// A sílaba que o pontilhado-guia mostra: a próxima a escrever.
	public string? NextPart => IsDone ? null : parts[done];

	public WriteStep Submit(string letters)
	{
		var candidate = Pending + Plain(letters);
		if (IsDone || candidate.Length == 0) return Reject();

		for (var count = 1; done + count <= plain.Count; count++)
		{
			var taken = string.Concat(plain.Skip(done).Take(count));
			if (taken == candidate) return Accept(count);
			if (!taken.StartsWith(candidate, StringComparison.Ordinal) && !candidate.StartsWith(taken, StringComparison.Ordinal)) break;
		}
		var rest = string.Concat(plain.Skip(done));
		return rest.StartsWith(candidate, StringComparison.Ordinal) && candidate.Length < rest.Length ? Wait(candidate) : Reject();
	}

	WriteStep Accept(int count)
	{
		done += count;
		Pending = "";
		return new WriteStep(WriteKind.Accepted, count);
	}

	WriteStep Wait(string candidate)
	{
		Pending = candidate;
		return new WriteStep(WriteKind.Incomplete, 0);
	}

	WriteStep Reject()
	{
		Pending = "";
		return new WriteStep(WriteKind.Wrong, 0);
	}

	// Sem acento nem cedilha, em maiúsculas, só as letras.
	static string Plain(string text)
	{
		var builder = new StringBuilder();
		foreach (var c in text.Normalize(NormalizationForm.FormD))
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetter(c)) builder.Append(char.ToUpperInvariant(c));
		return builder.ToString();
	}
}
