namespace ArquipelagoDoCeu.Equipment;

// A aparência do equipamento no Personagem (modelo 3D e cores). Os atributos moram em GearCatalog, nos dados.
public sealed record EquipmentLoadout(string Armor = "original", string Helmet = "none",
	string ClothColor = "#315BB3", string MetalColor = "#AAB8C9", string AccentColor = "#D6AA51");

// O que o equipamento soma ao Personagem. `Extras` leva os efeitos que não são os três de sempre (um efeito novo).
public sealed record EquipmentBonuses(float SpeedMultiplier = 1f, int ExtraEnergy = 0, float InteractionReachBonus = 0f,
	IReadOnlyDictionary<string, double>? Extras = null);

public static class EquipmentCatalog
{
	// As aparências que têm modelo 3D; uma peça sem modelo aparece como a aparência original.
	public static readonly IReadOnlyList<string> ArmorLooks = ["original", "scout", "guardian", "scholar"];

	public static readonly IReadOnlyList<string> HelmetLooks = ["none", "open", "crystal"];

	public static EquipmentLoadout Normalize(EquipmentLoadout? loadout)
	{
		var value = loadout ?? new EquipmentLoadout();
		return new EquipmentLoadout(
			ArmorLooks.Contains(value.Armor) ? value.Armor : "original",
			HelmetLooks.Contains(value.Helmet) ? value.Helmet : "none",
			Color(value.ClothColor, "#315BB3"), Color(value.MetalColor, "#AAB8C9"), Color(value.AccentColor, "#D6AA51"));
	}

	static string Color(string? value, string fallback) => value is { Length: 7 } && value[0] == '#'
		&& value.Skip(1).All(Uri.IsHexDigit) ? value.ToUpperInvariant() : fallback;

	// Aplicar um preset é uma ação explícita; trocar Variant não chama esta função.
	public static EquipmentLoadout Preset(string? variant) => variant switch
	{
		"minimal" => new("scout", "none", "#416EAB", "#AAB8C9", "#D6AA51"),
		"book" => new("scholar", "open", "#637E49", "#B7AD91", "#E6C66D"),
		"adventure" => new("guardian", "open", "#AD6843", "#9EAEBF", "#DBB755"),
		"crystal" => new("scholar", "crystal", "#7061B4", "#B2C7D8", "#6FE2D8"),
		_ => new()
	};
}
