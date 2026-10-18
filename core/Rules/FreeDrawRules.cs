namespace ArquipelagoDoCeu.Rules;

// O desenho livre: a espera que fecha o desenho, o tanto de pontos e a distância máxima da comparação, a razão que marca dúvida,
// o quanto de cobertura junta traços num símbolo, o tamanho de acento e até que Nível o pontilhado fica sempre à vista.
public sealed record FreeDrawRules(float IdleSeconds, int Points, float MaxDistance, float AmbiguityRatio, float MergeOverlap, float MarkRatio, int GuideAlwaysUpToYear)
{
	public static FreeDrawRules Default { get; } = new(0.8f, 32, 0.25f, 0.82f, 0.5f, 0.15f, 1);

	public bool GuideAlwaysShown(int year) => year <= GuideAlwaysUpToYear;
}

sealed record FreeDrawDto(float? IdleSeconds, int? Points, float? MaxDistance, float? AmbiguityRatio, float? MergeOverlap, float? MarkRatio, int? GuideAlwaysUpToYear);

static class FreeDrawChecks
{
	// O bloco é opcional: sem ele valem os padrões.
	public static string? Check(FreeDrawDto? d)
	{
		if (d is null) return null;
		var fields = new (string Name, float? Value, float Min, float Max)[]
		{
			("idleSeconds", d.IdleSeconds, 0.3f, 3f), ("points", d.Points, 16f, 128f), ("maxDistance", d.MaxDistance, 0.05f, 0.5f),
			("ambiguityRatio", d.AmbiguityRatio, 0.5f, 1f), ("mergeOverlap", d.MergeOverlap, 0.1f, 1f), ("markRatio", d.MarkRatio, 0f, 0.4f),
			("guideAlwaysUpToYear", d.GuideAlwaysUpToYear, 0f, 3f),
		};
		foreach (var (name, value, min, max) in fields)
		{
			if (value is null) return $"Regras inválidas: o campo 'combat.freeDraw.{name}' é obrigatório.";
			if (value < min || value > max) return $"Regras inválidas: 'combat.freeDraw.{name}' deve estar entre {min} e {max} (veio {value}).";
		}
		return null;
	}

	public static FreeDrawRules Build(FreeDrawDto? d) =>
		d is null ? FreeDrawRules.Default : new FreeDrawRules(d.IdleSeconds!.Value, d.Points!.Value, d.MaxDistance!.Value, d.AmbiguityRatio!.Value, d.MergeOverlap!.Value, d.MarkRatio!.Value, d.GuideAlwaysUpToYear!.Value);
}
