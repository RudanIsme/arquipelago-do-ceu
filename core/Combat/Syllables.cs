using ArquipelagoDoCeu.Generation;

namespace ArquipelagoDoCeu.Combat;

// A bolsa de sílabas do Escriba: as sílabas do nome mais alguns distratores, embaralhados pela seed.
public static class SyllableBag
{
	public static Result<IReadOnlyList<string>> Deal(IReadOnlyList<string> name, IReadOnlyList<string> pool, int distractors, Pcg32 rng)
	{
		if (name.Count == 0) return Result<IReadOnlyList<string>>.Fail("O nome precisa de ao menos uma sílaba.");

		var candidates = pool
			.Where(s => !name.Contains(s, StringComparer.OrdinalIgnoreCase))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (candidates.Count < distractors)
			return Result<IReadOnlyList<string>>.Fail($"Faltam sílabas para distrair: pedi {distractors}, há {candidates.Count}.");

		rng.Shuffle(candidates);
		var pieces = name.Concat(candidates.Take(distractors)).ToList();
		rng.Shuffle(pieces);
		return Result<IReadOnlyList<string>>.Ok(pieces);
	}
}

public sealed record NameCheck(bool IsComplete, IReadOnlyList<bool> Correct);

public static class NameChecker
{
	// Compara posição a posição, sem diferenciar maiúscula de minúscula. `Correct` tem uma marca por sílaba do nome.
	public static NameCheck Check(IReadOnlyList<string> expected, IReadOnlyList<string> assembled)
	{
		var correct = expected
			.Select((syllable, i) => i < assembled.Count && string.Equals(syllable, assembled[i], StringComparison.OrdinalIgnoreCase))
			.ToList();
		return new NameCheck(assembled.Count == expected.Count && correct.All(c => c), correct);
	}
}
