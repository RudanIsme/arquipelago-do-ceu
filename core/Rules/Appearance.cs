using System.Text.RegularExpressions;

namespace ArquipelagoDoCeu.Rules;

public sealed record ProfileRules(int MaxNicknameLength, int MaxProfiles);

// Cada opção tem nome, porque nenhuma informação pode depender só da cor.
public sealed record AppearanceOption(string Id, string Name, string Color);

// Uma categoria sem opções fica declarada nos dados e some da tela; é assim que a customização
// ainda sem arte (acessórios, por exemplo) fica em aberto sem código a mais.
public sealed record AppearanceCategory(string Id, string Name, IReadOnlyList<AppearanceOption> Options);

public sealed record AppearanceCatalog(IReadOnlyList<AppearanceCategory> Categories)
{
	public AppearanceCategory? Find(string categoryId) => Categories.FirstOrDefault(c => c.Id == categoryId);
}

sealed record AppearanceDto(List<CategoryDto?>? Categories);

sealed record CategoryDto(string? Id, string? Name, List<OptionDto?>? Options);

sealed record OptionDto(string? Id, string? Name, string? Color);

static partial class AppearanceChecks
{
	[GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
	private static partial Regex HexColor();

	// Devolve a primeira mensagem de erro, ou null se o catálogo está bom.
	public static string? Check(AppearanceDto? appearance)
	{
		if (appearance is null) return Required("appearance");
		if (appearance.Categories is null) return Required("appearance.categories");

		var categoryIds = new HashSet<string>();
		for (var i = 0; i < appearance.Categories.Count; i++)
		{
			var path = $"appearance.categories[{i}]";
			var category = appearance.Categories[i];
			if (category is null || Blank(category.Id)) return Required($"{path}.id");
			if (!categoryIds.Add(category.Id!)) return $"Regras inválidas: a categoria '{category.Id}' aparece mais de uma vez em 'appearance.categories'.";
			if (Blank(category.Name)) return Required($"{path}.name");
			if (category.Options is null) return Required($"{path}.options");

			var optionIds = new HashSet<string>();
			for (var j = 0; j < category.Options.Count; j++)
			{
				var option = category.Options[j];
				var optionPath = $"{path}.options[{j}]";
				if (option is null || Blank(option.Id)) return Required($"{optionPath}.id");
				if (!optionIds.Add(option.Id!)) return $"Regras inválidas: a opção '{option.Id}' aparece mais de uma vez na categoria '{category.Id}'.";
				if (Blank(option.Name)) return Required($"{optionPath}.name");
				if (option.Color is null || !HexColor().IsMatch(option.Color))
					return $"Regras inválidas: o campo '{optionPath}.color' deve ser uma cor no formato #RRGGBB (veio '{option.Color}').";
			}
		}
		return null;
	}

	public static AppearanceCatalog Build(AppearanceDto appearance) => new(
		appearance.Categories!.Select(c => new AppearanceCategory(
			c!.Id!,
			c.Name!,
			c.Options!.Select(o => new AppearanceOption(o!.Id!, o.Name!, o.Color!)).ToList())).ToList());

	static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);

	static string Required(string field) => $"Regras inválidas: o campo '{field}' é obrigatório.";
}
