using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Trade;

// Um preço em moedas do jogo. Nunca é dinheiro de verdade.
public sealed record MerchantOffer(string Id, int Price);

// O que o Mercador vende e compra, em dados (game/data/mercador.json). A ordem dos Cosméticos é a da rotação.
public sealed class MerchantCatalog
{
	const int MaxPrice = 100000;

	readonly IReadOnlyDictionary<string, int> sellPrices;

	MerchantCatalog(int stockSize, IReadOnlyList<MerchantOffer> cosmetics, IReadOnlyList<MerchantOffer> gear)
	{
		StockSize = stockSize;
		Cosmetics = cosmetics;
		sellPrices = gear.ToDictionary(o => o.Id, o => o.Price);
	}

	public int StockSize { get; }

	public IReadOnlyList<MerchantOffer> Cosmetics { get; }

	public static MerchantCatalog Empty => new(1, [], []);

	// Quanto o Mercador paga por um equipamento, e quanto custa recomprá-lo; zero quando ele não compra.
	public int SellPrice(string itemId) => sellPrices.GetValueOrDefault(itemId);

	public MerchantOffer? CosmeticOffer(string id) => Cosmetics.FirstOrDefault(o => o.Id == id);

	public static Result<MerchantCatalog> Parse(string json, CosmeticCatalog cosmetics, GearCatalog gear)
	{
		var read = CatalogJson.Deserialize<FileDto>(json, "O catálogo do Mercador");
		if (!read.IsOk) return Result<MerchantCatalog>.Fail(read.Error!);
		var dto = read.Value!;
		if (dto.StockSize < 1) return Result<MerchantCatalog>.Fail("O Mercador precisa mostrar ao menos 1 Cosmético por vez.");

		var sold = (dto.Cosmetics ?? []).Select(o => new MerchantOffer(o.Id ?? "", o.Price)).ToList();
		var bought = (dto.Gear ?? []).Select(o => new MerchantOffer(o.Id ?? "", o.Price)).ToList();
		var problem = Problem(sold, bought, cosmetics, gear);
		return problem is null
			? Result<MerchantCatalog>.Ok(new MerchantCatalog(dto.StockSize, sold, bought))
			: Result<MerchantCatalog>.Fail(problem);
	}

	static string? Problem(List<MerchantOffer> sold, List<MerchantOffer> bought, CosmeticCatalog cosmetics, GearCatalog gear)
	{
		if (sold.Count == 0) return "O Mercador não vende nenhum Cosmético.";
		if (sold.Concat(bought).Any(o => o.Price is < 1 or > MaxPrice)) return $"Um preço do Mercador está fora do limite de 1 a {MaxPrice}.";
		if (sold.FirstOrDefault(o => cosmetics.Find(o.Id) is null) is { } noCosmetic) return $"O Mercador vende o Cosmético {noCosmetic.Id}, que não existe.";
		if (bought.FirstOrDefault(o => gear.Find(o.Id) is null) is { } noGear) return $"O Mercador compra o equipamento {noGear.Id}, que não existe.";
		if (CatalogJson.Duplicate(sold, o => o.Id) is { } repeatedCosmetic) return $"O Mercador repete o Cosmético {repeatedCosmetic}.";
		return CatalogJson.Duplicate(bought, o => o.Id) is { } repeatedGear ? $"O Mercador repete o equipamento {repeatedGear}." : null;
	}

	sealed record FileDto(int StockSize, List<OfferDto>? Cosmetics, List<OfferDto>? Gear);

	sealed record OfferDto(string? Id, int Price);
}

// O Mercador de um Perfil: compra equipamento, vende Cosméticos do estoque e rotaciona o estoque, só com moedas do jogo.
// O estoque nunca troca sozinho: cada Expedição concluída dá uma carga (no máximo uma) e só o botão a gasta.
public sealed class MerchantService(ProgressStore store, MerchantCatalog catalog)
{
	public int Balance(string profileId) => store.Of(profileId)?.Coins ?? 0;

	public bool HasCharge(string profileId) => State(profileId).Charge == true;

	public IReadOnlyList<Buyback> Buybacks(string profileId) => State(profileId).Buyback ?? [];

	// O que está à venda agora: o que a rotação guardou, ou, antes da primeira rotação, o começo do catálogo.
	// Comprar não muda o estoque: o item comprado fica na vitrine, e só a rotação o tira.
	public IReadOnlyList<MerchantOffer> Stock(string profileId)
	{
		if (store.Find(profileId) is null) return [];
		var ids = State(profileId).Stock ?? catalog.Cosmetics.Take(catalog.StockSize).Select(o => o.Id).ToList();
		return ids.Select(catalog.CosmeticOffer).OfType<MerchantOffer>().ToList();
	}

	public Result<ProfileSave> AddCharge(string profileId) => Update(profileId, state => state with { Charge = true });

	// Gasta a carga e mostra os próximos Cosméticos que a criança ainda não tem, seguindo o catálogo em ordem e em círculo.
	public Result<IReadOnlyList<MerchantOffer>> Rotate(string profileId)
	{
		if (store.Find(profileId) is null) return Result<IReadOnlyList<MerchantOffer>>.Fail("Perfil não encontrado.");
		if (!HasCharge(profileId)) return Result<IReadOnlyList<MerchantOffer>>.Fail("O Mercador só rotaciona depois de uma Expedição concluída.");

		var window = Window(profileId, NextCursor(profileId));
		var saved = Update(profileId, state => state with { Stock = window.Ids, Cursor = window.Next, Charge = false });
		return saved.IsOk ? Result<IReadOnlyList<MerchantOffer>>.Ok(Stock(profileId)) : Result<IReadOnlyList<MerchantOffer>>.Fail(saved.Error!);
	}

	public Result<ProfileSave> Buy(string profileId, string cosmeticId)
	{
		if (store.Find(profileId) is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");
		if (Stock(profileId).FirstOrDefault(o => o.Id == cosmeticId) is not { } offer) return Result<ProfileSave>.Fail("Esse Cosmético não está à venda agora.");
		if ((store.Of(profileId)?.Cosmetics ?? []).Contains(cosmeticId)) return Result<ProfileSave>.Fail("Você já tem esse Cosmético.");
		if (Balance(profileId) < offer.Price) return Result<ProfileSave>.Fail($"Faltam {offer.Price - Balance(profileId)} moedas.");

		return store.Update(profileId, p => p with { Coins = Balance(profileId) - offer.Price, Cosmetics = [.. p.Cosmetics ?? [], cosmeticId] });
	}

	// Vende vários equipamentos de uma vez; se algum não puder (equipado, de outro, sem preço), nada é vendido.
	public Result<int> Sell(string profileId, IReadOnlyList<string> itemIds)
	{
		if (store.Find(profileId) is not { } profile) return Result<int>.Fail("Perfil não encontrado.");
		var ids = itemIds.Distinct().ToList();
		if (ids.Count == 0) return Result<int>.Fail("Escolha ao menos um equipamento para vender.");

		var owned = profile.Progress?.Equipment ?? [];
		var worn = (profile.Gear ?? new Dictionary<string, Gear>()).Values.SelectMany(g => g.Items.Values).ToHashSet();
		var blocked = ids.FirstOrDefault(id => !owned.Contains(id) || worn.Contains(id) || catalog.SellPrice(id) == 0);
		if (blocked is not null) return Result<int>.Fail(worn.Contains(blocked) ? "O equipamento vestido não se vende." : "Esse equipamento não pode ser vendido.");

		var total = ids.Sum(catalog.SellPrice);
		var saved = store.Update(profileId, p => WithState(p with
		{
			Coins = WalletService.Plus(p.Coins, total),
			Equipment = (p.Equipment ?? []).Where(id => !ids.Contains(id)).ToList(),
		}, state => state with { Buyback = [.. state.Buyback ?? [], .. ids.Select(id => new Buyback(id, catalog.SellPrice(id)))] }));
		return saved.IsOk ? Result<int>.Ok(total) : Result<int>.Fail(saved.Error!);
	}

	// Recomprar custa o mesmo preço da venda, sem prazo.
	public Result<ProfileSave> Rebuy(string profileId, string itemId)
	{
		if (store.Find(profileId) is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");
		if (Buybacks(profileId).FirstOrDefault(b => b.Item == itemId) is not { } entry) return Result<ProfileSave>.Fail("Esse equipamento não está na linha de recompra.");
		if (Balance(profileId) < entry.Price) return Result<ProfileSave>.Fail($"Faltam {entry.Price - Balance(profileId)} moedas.");

		return store.Update(profileId, p => WithState(p with
		{
			Coins = Balance(profileId) - entry.Price,
			Equipment = [.. p.Equipment ?? [], itemId],
		}, state => state with { Buyback = (state.Buyback ?? []).Where(b => b != entry).ToList() }));
	}

	MerchantSave State(string profileId) => store.Of(profileId)?.Merchant ?? new MerchantSave();

	Result<ProfileSave> Update(string profileId, Func<MerchantSave, MerchantSave> change) =>
		store.Update(profileId, p => WithState(p, change));

	static ProgressSave WithState(ProgressSave progress, Func<MerchantSave, MerchantSave> change) =>
		progress with { Merchant = change(progress.Merchant ?? new MerchantSave()) };

	int NextCursor(string profileId) => State(profileId).Cursor ?? catalog.StockSize % catalog.Cosmetics.Count;

	// Os próximos Cosméticos do catálogo, em círculo a partir de `start`, sem os que a criança já tem.
	(IReadOnlyList<string> Ids, int Next) Window(string profileId, int start)
	{
		var owned = store.Of(profileId)?.Cosmetics ?? [];
		var all = catalog.Cosmetics;
		var picked = new List<string>();
		var next = start;
		for (var step = 0; step < all.Count && picked.Count < catalog.StockSize; step++)
		{
			var index = (start + step) % all.Count;
			if (owned.Contains(all[index].Id)) continue;
			picked.Add(all[index].Id);
			next = (index + 1) % all.Count;
		}
		return (picked, next);
	}
}
