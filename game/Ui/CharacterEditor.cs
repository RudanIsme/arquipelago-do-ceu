using System;
using System.Linq;
using ArquipelagoDoCeu.Profiles;
using Godot;

namespace ArquipelagoDoCeu;

// Editor do Personagem: as categorias e opções vêm do catálogo de dados, e a prévia em 3D muda na hora.
// Categorias sem opções (como acessórios, ainda sem arte) simplesmente não aparecem.
public partial class CharacterEditor : Control
{
	readonly ProfileService profiles;
	readonly string profileId;
	CharacterView preview = null!;

	public event Action? Changed;
	public event Action? Done;

	public CharacterEditor(ProfileService profiles, string profileId)
	{
		this.profiles = profiles;
		this.profileId = profileId;
	}

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var card = UiKit.Card(this, "Meu personagem");
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 24);
		card.AddChild(row);

		row.AddChild(BuildPreview());
		var options = new VBoxContainer();
		options.AddThemeConstantOverride("separation", 10);
		row.AddChild(options);

		var current = profiles.AppearanceOf(profiles.Find(profileId).Value!);
		foreach (var category in profiles.Catalog.Categories)
		{
			if (category.Options.Count == 0) continue;
			var chosen = current.FirstOrDefault(c => c.Category.Id == category.Id)?.Option.Id;
			options.AddChild(UiKit.Hint(category.Name));

			var grid = new GridContainer { Columns = 3 };
			grid.AddThemeConstantOverride("h_separation", 8);
			grid.AddThemeConstantOverride("v_separation", 8);
			var group = new ButtonGroup();
			foreach (var option in category.Options)
			{
				var (categoryId, optionId) = (category.Id, option.Id);
				var button = UiKit.BigButton(option.Name, () => Pick(categoryId, optionId), 190);
				button.AddThemeFontSizeOverride("font_size", 24);
				button.Icon = UiKit.Swatch(option.Color);
				button.ToggleMode = true;
				button.ButtonGroup = group;
				button.ButtonPressed = option.Id == chosen;
				grid.AddChild(button);
			}
			options.AddChild(grid);
		}
		options.AddChild(UiKit.BigButton("Pronto", () => Done?.Invoke()));
		Refresh();
	}

	Control BuildPreview()
	{
		var container = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(300, 430) };
		var viewport = new SubViewport { OwnWorld3D = true, TransparentBg = true, Size = new Vector2I(300, 430), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		container.AddChild(viewport);

		var camera = new Camera3D { Fov = 28f, Current = true };
		viewport.AddChild(camera);
		camera.LookAtFromPosition(new Vector3(0f, 0.65f, 3.4f), new Vector3(0f, 0.6f, 0f), Vector3.Up);
		viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35f, -25f, 0f), LightEnergy = 1.2f });
		viewport.AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = new Color(0f, 0f, 0f, 0f),
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = Color.FromHtml("#CFE0FF"),
				AmbientLightEnergy = 0.8f,
			},
		});
		preview = new CharacterView { RotationDegrees = new Vector3(0f, -20f, 0f) };
		viewport.AddChild(preview);
		return container;
	}

	void Pick(string categoryId, string optionId)
	{
		if (!profiles.Choose(profileId, categoryId, optionId).IsOk) return;
		Refresh();
		Changed?.Invoke();
	}

	void Refresh() => preview.Apply(profiles.AppearanceOf(profiles.Find(profileId).Value!));
}
