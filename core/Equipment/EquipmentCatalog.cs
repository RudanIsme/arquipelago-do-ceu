namespace ArquipelagoDoCeu.Equipment;

public sealed record EquipmentLoadout(string Armor = "original", string Helmet = "none",
	string ClothColor = "#315BB3", string MetalColor = "#AAB8C9", string AccentColor = "#D6AA51");
public sealed record EquipmentBonuses(float SpeedMultiplier = 1f, int ExtraEnergy = 0, float InteractionReachBonus = 0f);
public sealed record EquipmentItem(string Id, string Name, string Description);

public static class EquipmentCatalog
{
	public static IReadOnlyList<EquipmentItem> Armors { get; } = Array.AsReadOnly(new[] {
		new EquipmentItem("original", "Traje original", "Aparência original, sem bônus."),
		new EquipmentItem("scout", "Explorador", "Passos leves: +12% de velocidade."),
		new EquipmentItem("guardian", "Guardião", "Proteção: +2 de Energia máxima. Equipar não recupera Energia."),
		new EquipmentItem("scholar", "Estudioso", "Alcance: +0,35 unidade para interagir.")
	});
	public static IReadOnlyList<EquipmentItem> Helmets { get; } = Array.AsReadOnly(new[] {
		new EquipmentItem("none", "Sem capacete", "Rosto livre, sem bônus adicional."),
		new EquipmentItem("open", "Elmo aberto", "Proteção: +1 de Energia máxima, até +2 no conjunto."),
		new EquipmentItem("crystal", "Diadema de cristal", "Alcance: +0,15 unidade para interagir.")
	});

	public static EquipmentLoadout Normalize(EquipmentLoadout? loadout)
	{
		var value = loadout ?? new EquipmentLoadout();
		return new EquipmentLoadout(
			Armors.Any(x => x.Id == value.Armor) ? value.Armor : "original",
			Helmets.Any(x => x.Id == value.Helmet) ? value.Helmet : "none",
			Color(value.ClothColor, "#315BB3"), Color(value.MetalColor, "#AAB8C9"), Color(value.AccentColor, "#D6AA51"));
	}

	static string Color(string? value, string fallback) => value is { Length: 7 } && value[0] == '#'
		&& value.Skip(1).All(Uri.IsHexDigit) ? value.ToUpperInvariant() : fallback;

	// Derivado apenas do conjunto atual: reaplicar ou mudar a interface nunca acumula atributos.
	public static EquipmentBonuses Bonuses(EquipmentLoadout? loadout)
	{
		var value = Normalize(loadout);
		return new EquipmentBonuses(value.Armor == "scout" ? 1.12f : 1f,
			Math.Min(2, (value.Armor == "guardian" ? 2 : 0) + (value.Helmet == "open" ? 1 : 0)),
			Math.Min(.5f, (value.Armor == "scholar" ? .35f : 0f) + (value.Helmet == "crystal" ? .15f : 0f)));
	}

	// Aplicar um preset é uma ação explícita; trocar Variant não chama esta função.
	public static EquipmentLoadout Preset(string? variant) => variant switch
	{
		"minimal" => new("scout", "none", "#416EAB", "#AAB8C9", "#D6AA51"),
		"book" => new("scholar", "open", "#637E49", "#B7AD91", "#E6C66D"),
		"adventure" => new("guardian", "open", "#AD6843", "#9EAEBF", "#DBB755"),
		"crystal" => new("scholar", "crystal", "#7061B4", "#B2C7D8", "#6FE2D8"),
		_ => new()
	};

	public static string Summary(EquipmentLoadout? loadout)
	{
		var value = Normalize(loadout);
		var bonus = Bonuses(value);
		return $"{Armors.First(x => x.Id == value.Armor).Name} · {Helmets.First(x => x.Id == value.Helmet).Name}\n"
			+ $"Velocidade +{Math.Round((bonus.SpeedMultiplier - 1) * 100)}% · Energia máxima +{bonus.ExtraEnergy} · Alcance +{bonus.InteractionReachBonus.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}";
	}
}
