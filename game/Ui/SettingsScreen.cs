using System;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// Configurações do aparelho em três seções. Cada mudança é gravada na hora e vale na hora em todas as telas.
public partial class SettingsScreen : OverlayScreen
{
	const string TestPhrase = "Olá! Eu sou a voz do jogo.";

	readonly SettingsService settings;
	readonly VoiceService voice;
	readonly string? nickname;
	readonly Action openProfiles;
	string section = "jogo";
	string? error;

	public SettingsScreen(SettingsService settings, VoiceService voice, string? nickname, Action openProfiles)
	{
		this.settings = settings;
		this.voice = voice;
		this.nickname = nickname;
		this.openProfiles = openProfiles;
	}

	public override void _Ready()
	{
		base._Ready();
		Build();
	}

	// A frase de teste não continua falando depois que a tela fecha.
	public override void _ExitTree()
	{
		base._ExitTree();
		voice.Stop();
	}

	void Build()
	{
		var focus = UiKit.FocusIndex(this);
		var box = UiKit.Card(this, "Configurações", out var footer);
		box.AddChild(Tabs());
		switch (section)
		{
			case "acessibilidade": Accessibility(box); break;
			case "voz": VoiceSection(box); break;
			case "controles": ControlsSection(box); break;
			default: Game(box); break;
		}
		if (error is not null) footer.AddChild(UiKit.Hint(error));
		footer.AddChild(UiKit.BigButton("Fechar", Close));
		UiKit.RestoreFocus(this, focus);
	}

	Control Tabs()
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 10);
		var group = new ButtonGroup();
		foreach (var (id, name) in new[] { ("jogo", "Jogo"), ("acessibilidade", "Acessibilidade"), ("voz", "Voz"), ("controles", "Controles") })
		{
			var target = id;
			var tab = UiKit.BigButton(name, () => { section = target; Build(); }, 190);
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
		box.AddChild(UiKit.Hint("Controles: WASD ou setas para andar, Shift para correr, clique para caminhar, E para interagir e F para escolher a criatura."));
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

		AddToggle(box, "Redução de animações", current.ReduceMotion, (s, on) => s with { ReduceMotion = on });
		AddToggle(box, "Fonte para dislexia", current.DyslexiaFont, (s, on) => s with { DyslexiaFont = on });
		AddToggle(box, "Modo calmo", current.CalmMode, (s, on) => s with { CalmMode = on });
		AddToggle(box, "Efeitos visuais reduzidos", current.ReduceEffects, (s, on) => s with { ReduceEffects = on });
	}

	void VoiceSection(VBoxContainer box)
	{
		var current = settings.Current;
		AddToggle(box, "Voz", current.Voice, (s, on) => s with { Voice = on });
		if (!current.Voice)
		{
			box.AddChild(UiKit.Hint("Com a voz desligada, o jogo só mostra os textos."));
			return;
		}

		if (voice.MissingVoiceNotice is { } missing)
		{
			box.AddChild(UiKit.Hint(missing));
			return;
		}

		box.AddChild(UiKit.Hint("Velocidade da fala"));
		var speeds = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		speeds.AddThemeConstantOverride("separation", 8);
		var speedGroup = new ButtonGroup();
		foreach (var speed in settings.Options.VoiceSpeeds)
		{
			var id = speed.Id;
			var button = UiKit.BigButton(speed.Name, () => Apply(s => s with { VoiceSpeed = id }), 190);
			button.ToggleMode = true;
			button.ButtonGroup = speedGroup;
			button.ButtonPressed = speed.Id == current.VoiceSpeed;
			speeds.AddChild(button);
		}
		box.AddChild(speeds);

		var voices = voice.PortugueseVoices();
		if (voices.Count > 1)
		{
			box.AddChild(UiKit.Hint("Voz"));
			var voiceGroup = new ButtonGroup();
			var chosen = voice.Chosen()?.Id;
			foreach (var option in voices)
			{
				var id = option.Id;
				var button = UiKit.BigButton(option.Name, () => Apply(s => s with { VoiceId = id }), 420);
				button.ToggleMode = true;
				button.ButtonGroup = voiceGroup;
				button.ButtonPressed = id == chosen;
				box.AddChild(button);
			}
		}

		box.AddChild(UiKit.BigButton("Ouvir a voz", () => voice.Say(TestPhrase, interrupt: true), 420));
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

	// Se não deu para gravar, a tela avisa e continua mostrando o valor que vale.
	void Apply(Func<GameSettings, GameSettings> edit)
	{
		var result = settings.Change(edit);
		error = result.IsOk ? null : "Não foi possível guardar essa escolha. Ela volta ao que era.";
		if (!result.IsOk) GD.PushWarning(result.Error!);
		CallDeferred(nameof(Build));
	}
}
