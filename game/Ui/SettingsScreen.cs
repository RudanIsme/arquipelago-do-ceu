using System;
using ArquipelagoDoCeu.Settings;
using Godot;

namespace ArquipelagoDoCeu;

// Configurações do aparelho em três seções. Cada mudança é gravada na hora e vale na hora em todas as telas.
public partial class SettingsScreen : OverlayScreen
{
	readonly SettingsService settings;
	readonly string? nickname;
	readonly Action openProfiles;
	string section = "jogo";

	public SettingsScreen(SettingsService settings, string? nickname, Action openProfiles)
	{
		this.settings = settings;
		this.nickname = nickname;
		this.openProfiles = openProfiles;
	}

	public override void _Ready()
	{
		base._Ready();
		Build();
	}

	void Build()
	{
		var box = UiKit.Card(this, "Configurações", out var footer);
		box.AddChild(Tabs());
		switch (section)
		{
			case "acessibilidade": Accessibility(box); break;
			case "controles": ControlsSection(box); break;
			default: Game(box); break;
		}
		footer.AddChild(UiKit.BigButton("Fechar", Close));
	}

	Control Tabs()
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 10);
		var group = new ButtonGroup();
		foreach (var (id, name) in new[] { ("jogo", "Jogo"), ("acessibilidade", "Acessibilidade"), ("controles", "Controles") })
		{
			var target = id;
			var tab = UiKit.BigButton(name, () => { section = target; Build(); }, 200);
			tab.ToggleMode = true;
			tab.ButtonGroup = group;
			tab.ButtonPressed = id == section;
			row.AddChild(tab);
		}
		return row;
	}

	void Game(VBoxContainer box)
	{
		box.AddChild(UiKit.Hint(nickname is null ? "Nenhum perfil escolhido." : $"Quem está jogando: {nickname}"));
		box.AddChild(UiKit.BigButton("Trocar de perfil", () =>
		{
			Close();
			openProfiles();
		}));
	}

	void Accessibility(VBoxContainer box)
	{
		var current = settings.Current;
		box.AddChild(UiKit.Hint("Tamanho do texto"));
		var sizes = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		sizes.AddThemeConstantOverride("separation", 8);
		var group = new ButtonGroup();
		foreach (var size in settings.Options.TextSizes)
		{
			var id = size.Id;
			var button = UiKit.BigButton(size.Name, () => Apply(s => s with { TextSize = id }), 190);
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = size.Id == current.TextSize;
			sizes.AddChild(button);
		}
		box.AddChild(sizes);

		AddToggle(box, "Voz", current.Voice, (s, on) => s with { Voice = on });
		AddToggle(box, "Redução de animações", current.ReduceMotion, (s, on) => s with { ReduceMotion = on });
		AddToggle(box, "Fonte para dislexia", current.DyslexiaFont, (s, on) => s with { DyslexiaFont = on });
		AddToggle(box, "Modo calmo", current.CalmMode, (s, on) => s with { CalmMode = on });
	}

	void ControlsSection(VBoxContainer box)
	{
		box.AddChild(UiKit.Hint("Como você prefere jogar? Todas as formas continuam funcionando; aqui você escolhe as dicas que aparecem na tela."));
		var group = new ButtonGroup();
		foreach (var option in settings.Options.Controls)
		{
			var id = option.Id;
			var button = UiKit.BigButton(option.Name, () => Apply(s => s with { Controls = id }), 420);
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = option.Id == settings.Current.Controls;
			box.AddChild(button);
		}
		box.AddChild(UiKit.Hint(ControlHints.For(settings.Current.Controls)));
	}

	void AddToggle(VBoxContainer box, string label, bool on, Func<GameSettings, bool, GameSettings> change) =>
		box.AddChild(UiKit.Toggle(label, on, value => Apply(s => change(s, value))));

	// Se não deu para gravar, a tela avisa e mostra o valor que continua valendo.
	void Apply(Func<GameSettings, GameSettings> edit)
	{
		var result = settings.Change(edit);
		if (!result.IsOk) GD.PushWarning(result.Error!);
		CallDeferred(nameof(Build));
	}
}
