using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Equipment;

public sealed record JourneyPreferences(string Variant = "book", EquipmentLoadout? Equipment = null)
{
    public static JourneyPreferences Normalize(JourneyPreferences? preferences)
    {
        var value = preferences ?? new JourneyPreferences();
        var variant = value.Variant is "minimal" or "book" or "adventure" or "crystal" or "base" ? value.Variant : "book";
        return new(variant, EquipmentCatalog.Normalize(value.Equipment));
    }
}

public sealed class JourneyService(SaveSession session)
{
    public JourneyPreferences Get(string profileId) => JourneyPreferences.Normalize(
        session.Data.Profiles.FirstOrDefault(p => p.Id == profileId)?.Journey);

    public Result<ProfileSave> Set(string profileId, JourneyPreferences preferences)
    {
        var current = session.Data.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (current is null) return Result<ProfileSave>.Fail("Perfil não encontrado.");
        var updated = current with { Journey = JourneyPreferences.Normalize(preferences) };
        var saved = session.Commit(data => data with {
            Profiles = data.Profiles.Select(p => p.Id == profileId ? updated : p).ToList()
        });
        return saved.IsOk ? Result<ProfileSave>.Ok(updated) : Result<ProfileSave>.Fail(saved.Error!);
    }
}
