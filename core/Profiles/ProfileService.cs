using System.Text.RegularExpressions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Profiles;

public sealed record ChosenOption(AppearanceCategory Category, AppearanceOption Option);

// Perfis da criança neste aparelho: criar, renomear e escolher a aparência. Cada mudança é gravada
// antes de valer; se a gravação falhar, a lista em memória continua como estava.
public sealed partial class ProfileService
{
	readonly SaveStore store;
	readonly GameRules rules;
	readonly Func<string> newId;
	SaveData data;

	public ProfileService(SaveStore store, GameRules rules, SaveData data, Func<string> newId)
	{
		this.store = store;
		this.rules = rules;
		this.data = data;
		this.newId = newId;
	}

	public IReadOnlyList<ProfileSave> Profiles => data.Profiles;

	public bool CanCreate => data.Profiles.Count < rules.Profile.MaxProfiles;

	public int MaxNicknameLength => rules.Profile.MaxNicknameLength;

	public Result<ProfileSave> Find(string id)
	{
		var found = data.Profiles.FirstOrDefault(p => p.Id == id);
		return found is null ? Result<ProfileSave>.Fail("Perfil não encontrado.") : Result<ProfileSave>.Ok(found);
	}

	public Result<ProfileSave> Create(string nickname)
	{
		var name = Normalize(nickname, exceptId: null);
		if (!name.IsOk) return Result<ProfileSave>.Fail(name.Error!);
		if (data.Profiles.Count >= rules.Profile.MaxProfiles)
			return Result<ProfileSave>.Fail($"Este aparelho já tem {rules.Profile.MaxProfiles} perfis.");

		var profile = new ProfileSave(newId(), name.Value!, 1);
		return Commit(data with { Profiles = [.. data.Profiles, profile] }, profile);
	}

	public Result<ProfileSave> Rename(string id, string nickname)
	{
		var current = Find(id);
		if (!current.IsOk) return current;
		var name = Normalize(nickname, exceptId: id);
		if (!name.IsOk) return Result<ProfileSave>.Fail(name.Error!);

		return Replace(current.Value! with { Nickname = name.Value! });
	}

	public Result<ProfileSave> Choose(string id, string categoryId, string optionId)
	{
		var current = Find(id);
		if (!current.IsOk) return current;
		var category = rules.Appearance.Find(categoryId);
		if (category is null || category.Options.All(o => o.Id != optionId))
			return Result<ProfileSave>.Fail("Essa opção não existe.");

		var chosen = new Dictionary<string, string>(current.Value!.Appearance ?? new Dictionary<string, string>()) { [categoryId] = optionId };
		return Replace(current.Value with { Appearance = chosen });
	}

	// A escolha de cada categoria com opções; sem escolha, ou com uma opção que saiu dos dados, vale a primeira.
	public IReadOnlyList<ChosenOption> AppearanceOf(ProfileSave profile) =>
		rules.Appearance.Categories
			.Where(c => c.Options.Count > 0)
			.Select(c =>
			{
				var id = profile.Appearance is not null && profile.Appearance.TryGetValue(c.Id, out var saved) ? saved : null;
				return new ChosenOption(c, c.Options.FirstOrDefault(o => o.Id == id) ?? c.Options[0]);
			})
			.ToList();

	Result<ProfileSave> Replace(ProfileSave updated) =>
		Commit(data with { Profiles = data.Profiles.Select(p => p.Id == updated.Id ? updated : p).ToList() }, updated);

	Result<ProfileSave> Commit(SaveData next, ProfileSave result)
	{
		var written = store.Write(next);
		if (!written.IsOk) return Result<ProfileSave>.Fail(written.Error!);
		data = next;
		return Result<ProfileSave>.Ok(result);
	}

	Result<string> Normalize(string raw, string? exceptId)
	{
		var name = Spaces().Replace(raw ?? "", " ").Trim();
		if (name.Length == 0) return Result<string>.Fail("Escolha um apelido.");
		if (name.Length > rules.Profile.MaxNicknameLength)
			return Result<string>.Fail($"O apelido pode ter até {rules.Profile.MaxNicknameLength} letras.");
		if (!name.All(c => char.IsLetterOrDigit(c) || c == ' '))
			return Result<string>.Fail("Use só letras, números e espaços.");
		if (data.Profiles.Any(p => p.Id != exceptId && string.Equals(p.Nickname, name, StringComparison.OrdinalIgnoreCase)))
			return Result<string>.Fail("Já existe um perfil com esse apelido.");
		return Result<string>.Ok(name);
	}

	[GeneratedRegex(@"\s+")]
	private static partial Regex Spaces();
}
