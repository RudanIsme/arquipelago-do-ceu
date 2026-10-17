using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.World;

namespace ArquipelagoDoCeu.Generation;

// A forma de uma Sala: os hexágonos pisáveis ao redor da origem, o ponto de interesse (onde ficam a criatura, a escada e a fonte)
// e as alturas do chão (só o desnível tem). A Sala de sempre é a "media" (ADR 0003).
public sealed record RoomForm(string Id, string Name, IReadOnlySet<HexCell> Cells, IReadOnlyDictionary<HexCell, float> Heights, HexCell Center)
{
	public float HeightAt(HexCell cell) => Heights.TryGetValue(cell, out var height) ? height : 0f;

	// Um hexágono ao lado do centro para o que não pode ficar em cima dele (a escada lateral).
	public HexCell Side => Center.Neighbors().Where(Cells.Contains).OrderBy(c => c.Q).ThenBy(c => c.R).First();
}

// O catálogo de formas, lido de dados e validado na carga: o gerador sorteia pela seed e o tema fica separado da estrutura.
public sealed class RoomForms
{
	public const string LegacyId = "media";

	const int MaxRadius = 8;
	const int MaxCells = 150;

	readonly IReadOnlyList<RoomForm> forms;

	RoomForms(IReadOnlyList<RoomForm> forms) => this.forms = forms;

	public IReadOnlyList<RoomForm> All => forms;

	public RoomForm? Find(string id) => forms.FirstOrDefault(f => f.Id == id);

	// A forma dos Andares da versão antiga do gerador: todos tinham a mesma Sala.
	public RoomForm Legacy => Find(LegacyId)!;

	public RoomForm Pick(Pcg32 rng) => forms[rng.NextInt(forms.Count)];

	public static Result<RoomForms> Parse(string json)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O arquivo das formas de Sala");
		if (!read.IsOk) return Result<RoomForms>.Fail(read.Error!);
		if (read.Value!.Forms is not { Count: > 0 } dtos) return Result<RoomForms>.Fail("O arquivo das formas de Sala não traz nenhuma forma.");
		if (CatalogJson.Duplicate(dtos, f => f.Id ?? "") is { } duplicate)
			return Result<RoomForms>.Fail($"A forma de Sala '{duplicate}' aparece mais de uma vez.");

		var forms = new List<RoomForm>();
		foreach (var dto in dtos)
		{
			var built = Build(dto);
			if (!built.IsOk) return Result<RoomForms>.Fail(built.Error!);
			forms.Add(built.Value!);
		}
		return forms.All(f => f.Id != LegacyId)
			? Result<RoomForms>.Fail($"Falta a forma '{LegacyId}', a Sala dos Andares da versão antiga.")
			: Result<RoomForms>.Ok(new RoomForms(forms));
	}

	static Result<RoomForm> Build(FormDto dto)
	{
		var name = dto.Id ?? "?";
		Result<RoomForm> Fail(string why) => Result<RoomForm>.Fail($"A forma de Sala '{name}' é inválida: {why}.");

		if (string.IsNullOrWhiteSpace(dto.Id)) return Fail("sem id");
		if (string.IsNullOrWhiteSpace(dto.Name)) return Fail("sem nome");
		if (dto.Parts is not { Count: > 0 }) return Fail("precisa de ao menos uma peça em 'parts'");

		var regions = dto.Parts.Concat(dto.Holes ?? []).Concat(dto.Heights ?? []).ToList();
		if (regions.Any(r => !InRange(r.A) || !InRange(r.B) || !InRange(r.C))) return Fail($"a, b e c de cada peça devem estar entre 1 e {MaxRadius}");

		var cells = dto.Parts.SelectMany(Cells).ToHashSet();
		cells.ExceptWith(dto.Holes?.SelectMany(Cells) ?? []);
		var heights = new Dictionary<HexCell, float>();
		foreach (var step in dto.Heights ?? [])
		{
			if (step.Height is not (>= 0f and <= 1f)) return Fail("a altura deve estar entre 0 e 1");
			foreach (var cell in Cells(step).Where(cells.Contains)) heights[cell] = step.Height.Value;
		}

		var center = dto.Center is { Length: 2 } c ? new HexCell(c[0], c[1]) : new HexCell(0, 0);
		if (cells.Count == 0 || cells.Count > MaxCells) return Fail($"precisa de 1 a {MaxCells} hexágonos");
		if (!cells.Contains(center)) return Fail("o centro precisa ser um hexágono da forma");
		if (Reach(center, cells) != cells.Count) return Fail("tem hexágono solto (a forma precisa ser uma peça só)");
		return Result<RoomForm>.Ok(new RoomForm(dto.Id!, dto.Name!, cells, heights, center));
	}

	static bool InRange(int? value) => value is >= 1 and <= MaxRadius;

	// Um hexágono é da região se está a até a, b e c do centro dela nos três eixos do cubo.
	static IEnumerable<HexCell> Cells(RegionDto r)
	{
		var (a, b, c) = (r.A ?? 1, r.B ?? 1, r.C ?? 1);
		for (var q = -a; q <= a; q++)
			for (var rr = -b; rr <= b; rr++)
				if (Math.Abs(q + rr) <= c) yield return new HexCell(q + (r.Q ?? 0), rr + (r.R ?? 0));
	}

	static int Reach(HexCell start, HashSet<HexCell> cells)
	{
		var seen = new HashSet<HexCell> { start };
		var queue = new Queue<HexCell>([start]);
		while (queue.Count > 0)
			foreach (var next in queue.Dequeue().Neighbors().Where(cells.Contains))
				if (seen.Add(next)) queue.Enqueue(next);
		return seen.Count;
	}

	sealed record FileDto(List<FormDto>? Forms);

	sealed record FormDto(string? Id, string? Name, List<RegionDto>? Parts, List<RegionDto>? Holes, List<RegionDto>? Heights, int[]? Center);

	sealed record RegionDto(int? A, int? B, int? C, int? Q, int? R, float? Height);
}
