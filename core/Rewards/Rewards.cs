using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Rewards;

// O que aconteceu no jogo e pode render recompensa: o gatilho e onde (torre, Nível, Andar) ou qual Conceito.
public sealed record RewardContext(string Trigger, string Discipline, int Level, int? Floor = null, string? Concept = null);

// Um gatilho de recompensa (ADR 0006): sabe se aquele acontecimento já foi pago e como marcá-lo. Um gatilho novo é uma
// linha em RewardTriggers; o que ele paga vem dos dados.
public interface IRewardTrigger
{
	string Id { get; }

	bool AlreadyGiven(ProgressSave progress, RewardContext context);

	ProgressSave MarkGiven(ProgressSave progress, RewardContext context);
}

// Paga toda vez que acontece (criatura acalmada, Andar da Expedição livre).
public sealed class RepeatableTrigger(string id) : IRewardTrigger
{
	public string Id => id;

	public bool AlreadyGiven(ProgressSave progress, RewardContext context) => false;

	public ProgressSave MarkGiven(ProgressSave progress, RewardContext context) => progress;
}

// Paga uma vez só por ocorrência: `occurrence` diz o que identifica "a mesma" (por exemplo a torre e o Nível).
public sealed class OnceTrigger(string id, Func<RewardContext, string> occurrence) : IRewardTrigger
{
	public string Id => id;

	string Key(RewardContext context) => $"{id}:{occurrence(context)}";

	public bool AlreadyGiven(ProgressSave progress, RewardContext context) => (progress.Occurrences ?? []).Contains(Key(context));

	public ProgressSave MarkGiven(ProgressSave progress, RewardContext context) => progress with { Occurrences = [.. progress.Occurrences ?? [], Key(context)] };
}

// Andar completo: uma vez por Andar, e o próprio registro de Andares completos guarda isso (o mapa e saves antigos o leem).
sealed class FloorCompleteTrigger : IRewardTrigger
{
	public string Id => "floor-complete";

	static string Key(RewardContext context) => new FloorKey(context.Discipline, context.Level, context.Floor ?? 0).ToString();

	public bool AlreadyGiven(ProgressSave progress, RewardContext context) => (progress.FloorsDone ?? []).Contains(Key(context));

	public ProgressSave MarkGiven(ProgressSave progress, RewardContext context) => progress with { FloorsDone = [.. progress.FloorsDone ?? [], Key(context)] };
}

// Os gatilhos que o jogo conhece, por id.
public sealed class RewardTriggers
{
	readonly IReadOnlyDictionary<string, IRewardTrigger> byId;

	RewardTriggers(IReadOnlyDictionary<string, IRewardTrigger> byId) => this.byId = byId;

	public static RewardTriggers Default { get; } = new(new IRewardTrigger[]
	{
		new RepeatableTrigger("creature-calmed"),
		new RepeatableTrigger("free-floor-complete"),
		new FloorCompleteTrigger(),
		new OnceTrigger("item-room", c => $"{c.Discipline}:{c.Level}"),
		new OnceTrigger("concept-mastered", c => $"{c.Discipline}:{c.Concept}"),
		new OnceTrigger("master-challenge", c => c.Level.ToString(System.Globalization.CultureInfo.InvariantCulture)),
	}.ToDictionary(t => t.Id));

	public IRewardTrigger? Find(string id) => byId.GetValueOrDefault(id);

	public RewardTriggers With(IRewardTrigger trigger) => new(new Dictionary<string, IRewardTrigger>(byId) { [trigger.Id] = trigger });
}

// Onde a recompensa vale. Campo ausente vale para qualquer valor.
public sealed record RewardFilter(string? Discipline = null, int? Level = null, int? Floor = null, string? Concept = null)
{
	public bool Matches(RewardContext context) =>
		(Discipline is null || Discipline == context.Discipline) && (Level is null || Level == context.Level)
		&& (Floor is null || Floor == context.Floor) && (Concept is null || Concept == context.Concept);
}

// Uma recompensa, em dados: moedas, um Cosmético ou um equipamento, ligada a um gatilho (e, se quiser, a um Andar ou Conceito).
public sealed record Reward(string Id, string Trigger, int Coins, Cosmetic? Cosmetic, RewardFilter When, GearItem? Gear = null);

// O que o gatilho rendeu: `Fired` é falso quando aquele acontecimento já tinha sido pago.
public sealed record RewardOutcome(bool Fired, int Coins, IReadOnlyList<Cosmetic> Cosmetics, IReadOnlyList<GearItem>? Gear = null);

// As recompensas do jogo (game/data/recompensas.json): o que cada gatilho paga.
public sealed class RewardCatalog
{
	// Quanto uma recompensa pode pagar de uma vez; mais que isso é erro de digitação nos dados.
	const int MaxCoins = 1000;

	RewardCatalog(IReadOnlyList<Reward> rewards, RewardTriggers triggers)
	{
		Rewards = rewards;
		Triggers = triggers;
	}

	public IReadOnlyList<Reward> Rewards { get; }

	public RewardTriggers Triggers { get; }

	public static RewardCatalog Empty => new([], RewardTriggers.Default);

	public IReadOnlyList<Reward> For(RewardContext context) => Rewards.Where(r => r.Trigger == context.Trigger && r.When.Matches(context)).ToList();

	public static Result<RewardCatalog> Parse(string json, RewardTriggers triggers, CosmeticCatalog cosmetics, GearCatalog? gear = null)
	{
		gear ??= GearCatalog.Empty;
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo de recompensas");
		if (!read.IsOk) return Result<RewardCatalog>.Fail(read.Error!);
		if (read.Value!.Rewards is not { Count: > 0 } list) return Result<RewardCatalog>.Fail("O catálogo de recompensas não traz nenhuma recompensa.");
		if (CatalogJson.Duplicate(list, r => r.Id ?? "") is { } repeated) return Result<RewardCatalog>.Fail($"O catálogo de recompensas repete o id {repeated}.");

		var rewards = new List<Reward>();
		foreach (var dto in list)
		{
			var problem = Problem(dto, triggers, cosmetics, gear);
			if (problem is not null) return Result<RewardCatalog>.Fail(problem);
			rewards.Add(new Reward(dto.Id!, dto.Trigger!, dto.Coins, dto.Cosmetic is null ? null : cosmetics.Find(dto.Cosmetic),
				new RewardFilter(dto.When?.Discipline, dto.When?.Level, dto.When?.Floor, dto.When?.Concept), gear.Find(dto.Gear)));
		}
		return Result<RewardCatalog>.Ok(new RewardCatalog(rewards, triggers));
	}

	static string? Problem(RewardDto dto, RewardTriggers triggers, CosmeticCatalog cosmetics, GearCatalog gear)
	{
		if (string.IsNullOrEmpty(dto.Id)) return "Uma recompensa do catálogo está sem id.";
		if (dto.Trigger is null || triggers.Find(dto.Trigger) is null) return $"A recompensa {dto.Id} usa o gatilho {dto.Trigger}, que não existe.";
		if (dto.Coins is < 0 or > MaxCoins) return $"A recompensa {dto.Id} paga moedas fora do limite de 0 a {MaxCoins}.";
		if (((dto.Coins > 0 ? 1 : 0) + (dto.Cosmetic is not null ? 1 : 0) + (dto.Gear is not null ? 1 : 0)) != 1) return $"A recompensa {dto.Id} precisa de moedas, de um Cosmético ou de um equipamento, só um dos três.";
		if (dto.Gear is not null && gear.Find(dto.Gear) is null) return $"A recompensa {dto.Id} usa o equipamento {dto.Gear}, que não existe.";
		return dto.Cosmetic is not null && cosmetics.Find(dto.Cosmetic) is null ? $"A recompensa {dto.Id} usa o Cosmético {dto.Cosmetic}, que não existe." : null;
	}

	sealed record FileDto(List<RewardDto>? Rewards);

	sealed record RewardDto(string? Id, string? Trigger, int Coins, string? Cosmetic, string? Gear, FilterDto? When);

	sealed record FilterDto(string? Discipline, int? Level, int? Floor, string? Concept);
}

// Paga as recompensas de um gatilho, uma vez por acontecimento quando o gatilho é de uma vez só, num só salvamento.
// Se o save falha, nada vale e a criança ganha de novo da próxima vez.
public sealed class RewardService(ProgressStore store)
{
	public IReadOnlyList<string> Cosmetics(string profileId) => store.Of(profileId)?.Cosmetics ?? [];

	public IReadOnlyList<string> Equipment(string profileId) => store.Of(profileId)?.Equipment ?? [];

	public Result<RewardOutcome> Grant(string profileId, RewardContext context, RewardCatalog catalog)
	{
		if (store.Find(profileId) is null) return Result<RewardOutcome>.Fail("Perfil não encontrado.");
		// Sem recompensas (o arquivo não abriu) o acontecimento fica por registrar, para o prêmio não se perder para sempre.
		if (catalog.Rewards.Count == 0) return Result<RewardOutcome>.Ok(new RewardOutcome(false, 0, []));
		if (catalog.Triggers.Find(context.Trigger) is not { } trigger) return Result<RewardOutcome>.Fail($"O gatilho {context.Trigger} não existe.");

		var progress = store.Of(profileId) ?? new ProgressSave();
		if (trigger.AlreadyGiven(progress, context)) return Result<RewardOutcome>.Ok(new RewardOutcome(false, 0, []));

		var matched = catalog.For(context);
		var owned = progress.Cosmetics ?? [];
		var ownedGear = progress.Equipment ?? [];
		var coins = matched.Sum(r => r.Coins);
		var gear = matched.Select(r => r.Gear).OfType<GearItem>().Where(g => !ownedGear.Contains(g.Id)).DistinctBy(g => g.Id).ToList();
		var cosmetics = matched.Select(r => r.Cosmetic).OfType<Cosmetic>().Where(c => !owned.Contains(c.Id)).DistinctBy(c => c.Id).ToList();
		var outcome = new RewardOutcome(true, coins, cosmetics, gear);
		if (ReferenceEquals(trigger.MarkGiven(progress, context), progress) && coins == 0 && cosmetics.Count == 0 && gear.Count == 0) return Result<RewardOutcome>.Ok(outcome);

		var saved = store.Update(profileId, p => trigger.MarkGiven(p, context) with
		{
			Coins = coins == 0 ? p.Coins : WalletService.Plus(p.Coins, coins),
			Cosmetics = cosmetics.Count == 0 ? p.Cosmetics : [.. p.Cosmetics ?? [], .. cosmetics.Select(c => c.Id)],
			Equipment = gear.Count == 0 ? p.Equipment : [.. p.Equipment ?? [], .. gear.Select(g => g.Id)],
		});
		return saved.IsOk ? Result<RewardOutcome>.Ok(outcome) : Result<RewardOutcome>.Fail(saved.Error!);
	}
}
