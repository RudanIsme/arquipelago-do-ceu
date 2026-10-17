using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Equipment;

// Um atributo que muda ao trocar de equipamento. `Spoken` é a frase que a voz diz ("Energia sobe 1").
public sealed record AttributeChange(string Effect, string Label, double From, double To, string FromText, string ToText, string Spoken)
{
	public bool Rises => To > From;

	public string Arrow => Rises ? "▲" : "▼";
}

// Uma linha da biblioteca: o equipamento, se está vestido e se falta Maestria para vesti-lo.
public sealed record LibraryEntry(GearItem Item, bool Equipped, bool Locked);

// A biblioteca de equipamentos da criança e a comparação que o Inventário mostra. Enquanto nada concede equipamento
// (#130), tudo o que está no catálogo é dela; o inventário acumula tudo, sem limite.
public sealed class GearLibrary(GearCatalog catalog)
{
	const double Tolerance = 1e-9;

	// Os vestidos primeiro; depois o mais novo (o que está mais para o fim do catálogo).
	public IReadOnlyList<LibraryEntry> Entries(Gear worn, int mastery) =>
		catalog.Items
			.Select(i => new LibraryEntry(i, worn.In(i.Slot) == i.Id, mastery < i.MinMastery))
			.Reverse()
			.OrderByDescending(e => e.Equipped)
			.ToList();

	// Só o que muda de `from` para `to`; atributo igual não aparece.
	public IReadOnlyList<AttributeChange> Compare(Gear from, Gear to)
	{
		var after = catalog.Totals(to).ToDictionary(t => t.Effect);
		return catalog.Totals(from)
			.Where(before => Math.Abs(after[before.Effect].Value - before.Value) > Tolerance)
			.Select(before => Change(before, after[before.Effect]))
			.ToList();
	}

	static AttributeChange Change(GearTotal before, GearTotal after)
	{
		var effect = before.Definition;
		var rises = after.Value > before.Value;
		var amount = effect.Format(Math.Abs(after.Value - before.Value));
		var spoken = $"{effect.Label} {(rises ? "sobe" : "desce")} {amount}";
		return new AttributeChange(before.Effect, effect.Label, before.Value, after.Value, effect.Format(before.Value), effect.Format(after.Value), spoken);
	}
}

// O que o Inventário guarda no Perfil: os Cosméticos vestidos, o que a criança já viu (o selo "novo") e se o guia abriu.
// Os Cosméticos só mudam a aparência: vestir nunca toca no equipamento nem nos atributos.
public sealed class InventoryService(SaveSession session, CosmeticCatalog cosmetics)
{
	public IReadOnlyList<string> Worn(string profileId) =>
		(Profile(profileId)?.Worn ?? []).Where(id => cosmetics.Find(id) is not null).ToList();

	public IReadOnlyList<CosmeticLook> WornLooks(string profileId) =>
		Worn(profileId).Select(id => cosmetics.Find(id)?.Look).OfType<CosmeticLook>().ToList();

	public Result<IReadOnlyList<string>> Wear(string profileId, string cosmeticId, IReadOnlyList<string> owned)
	{
		if (cosmetics.Find(cosmeticId) is null) return Result<IReadOnlyList<string>>.Fail("Esse Cosmético não existe.");
		if (!owned.Contains(cosmeticId)) return Result<IReadOnlyList<string>>.Fail("Você ainda não ganhou esse Cosmético.");
		var worn = Worn(profileId);
		return worn.Contains(cosmeticId) ? Result<IReadOnlyList<string>>.Ok(worn) : Save(profileId, p => p with { Worn = [.. worn, cosmeticId] }, p => p.Worn);
	}

	public Result<IReadOnlyList<string>> Remove(string profileId, string cosmeticId) =>
		Save(profileId, p => p with { Worn = Worn(profileId).Where(id => id != cosmeticId).ToList() }, p => p.Worn);

	public bool IsNew(string profileId, string key) => !(Profile(profileId)?.Seen ?? []).Contains(key);

	public int NewCount(string profileId, IEnumerable<string> keys) => keys.Count(key => IsNew(profileId, key));

	public Result<IReadOnlyList<string>> MarkSeen(string profileId, string key)
	{
		var seen = Profile(profileId)?.Seen ?? [];
		return seen.Contains(key) ? Result<IReadOnlyList<string>>.Ok(seen) : Save(profileId, p => p with { Seen = [.. seen, key] }, p => p.Seen);
	}

	public bool GuideSeen(string profileId) => Profile(profileId)?.GuideSeen ?? false;

	public Result<IReadOnlyList<string>> MarkGuideSeen(string profileId) => Save(profileId, p => p with { GuideSeen = true }, _ => []);

	ProfileSave? Profile(string profileId) => session.Data.Profiles.FirstOrDefault(p => p.Id == profileId);

	Result<IReadOnlyList<string>> Save(string profileId, Func<ProfileSave, ProfileSave> change, Func<ProfileSave, IReadOnlyList<string>?> read)
	{
		if (Profile(profileId) is not { } current) return Result<IReadOnlyList<string>>.Fail("Perfil não encontrado.");
		var updated = change(current);
		var saved = session.Commit(data => data with { Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList() });
		return saved.IsOk ? Result<IReadOnlyList<string>>.Ok(read(updated) ?? []) : Result<IReadOnlyList<string>>.Fail(saved.Error!);
	}
}
