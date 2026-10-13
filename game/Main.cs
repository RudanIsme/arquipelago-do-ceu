using System;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// Raiz de composição: liga os adaptadores do Godot às regras de core/ e conduz o fluxo das telas.
public partial class Main : Node3D
{
	const string RulesPath = "res://data/rules.json";
	const string SavePath = "user://save.json";
	const string DyslexiaFontPath = "res://assets/fontes/OpenDyslexic-Regular.otf";
	const int DyslexiaLetterSpacing = 1;

	CanvasLayer ui = null!;
	Control uiRoot = null!;
	Label title = null!;
	Label notice = null!;
	Label controlHint = null!;
	GameRules rules = null!;
	ProfileService profiles = null!;
	SettingsService settings = null!;
	VoiceService voice = null!;
	ProfileScreen? screen;
	CharacterEditor? editor;
	OverlayScreen? overlay;
	Button characterButton = null!;
	Button interactButton = null!;
	Control bottomBar = null!;
	ProfileSave? current;
	Lobby lobby = null!;
	Player player = null!;
	string? interactTarget;
	float uiScale = 1f;
	TrainingDummy dummy = null!;
	TrainingBattle? battle;
	CombatTime time = null!;

	public override void _Ready()
	{
		ui = GetNode<CanvasLayer>("Interface");
		BuildUiRoot();

		var loadedRules = LoadRules();
		if (!loadedRules.IsOk) { Stop(loadedRules.Error!); return; }
		rules = loadedRules.Value!;
		title.Text = rules.GameName;
		GD.Print($"Regras carregadas: {rules.GameName}");

		var store = new SaveStore(ProjectSettings.GlobalizePath(SavePath));
		var save = OpenSave(store);
		if (!save.IsOk) { Stop(save.Error!); return; }
		var session = new SaveSession(store, save.Value!);
		profiles = new ProfileService(session, rules, () => Guid.NewGuid().ToString("N"));
		settings = new SettingsService(session, rules);
		voice = new VoiceService(new GodotSpeechEngine(), settings);

		Controls.Register();
		lobby = GD.Load<PackedScene>("res://Lobby/Lobby.tscn").Instantiate<Lobby>();
		lobby.Rules = rules.Lobby;
		AddChild(lobby);
		player = new Player(lobby.Grid, lobby.Camera) { Visible = false };
		AddChild(player);
		SetUpTraining();

		BuildHud();
		settings.Changed += ApplySettings;
		ApplySettings(settings.Current);
		StartFlow();
		if (voice.MissingVoiceNotice is { } missing && string.IsNullOrEmpty(notice.Text))
		{
			notice.Text = missing;
			GD.PushWarning(missing);
		}
		DevDriver.AttachIfRequested(this);

		GD.Print("Cena inicial pronta.");
	}

	// Toda a interface vive num Control que muda de escala: o tamanho do texto vale na hora em todas as telas,
	// e o Control cobre a janela inteira na nova escala para as âncoras continuarem certas.
	void BuildUiRoot()
	{
		uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		ui.AddChild(uiRoot);
		foreach (var name in new[] { "Message", "Notice" })
		{
			var label = ui.GetNode<Label>(name);
			ui.RemoveChild(label);
			uiRoot.AddChild(label);
		}
		title = uiRoot.GetNode<Label>("Message");
		notice = uiRoot.GetNode<Label>("Notice");
		// Abaixo da fileira de botões, para nunca cobri-los com texto maior ou janela estreita.
		title.HorizontalAlignment = HorizontalAlignment.Left;
		title.OffsetLeft = 16;
		title.OffsetTop = 80;
		title.OffsetBottom = 120;
		UiKit.Outline(title);
		UiKit.Outline(notice);
		GetViewport().SizeChanged += FitUiRoot;
		FitUiRoot();
	}

	void FitUiRoot()
	{
		uiRoot.Scale = Vector2.One * uiScale;
		uiRoot.Size = GetViewport().GetVisibleRect().Size / uiScale;
	}

	void BuildHud()
	{
		var hud = new HBoxContainer();
		hud.AddThemeConstantOverride("separation", 8);
		hud.AddChild(UiKit.BigButton("Perfis", ShowProfiles, 160));
		characterButton = UiKit.BigButton("Personagem", ShowEditor, 220);
		characterButton.Visible = false;
		hud.AddChild(characterButton);
		hud.AddChild(UiKit.BigButton("Configurações", ShowSettings, 260));
		uiRoot.AddChild(hud);

		// O botão de interação e a dica de controle ficam embaixo, centralizados, longe das telas do alto.
		var bottom = bottomBar = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
		bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		bottom.OffsetTop = -200;
		bottom.OffsetBottom = -90;
		bottom.AddThemeConstantOverride("separation", 8);
		interactButton = UiKit.BigButton("", () => Interact(interactTarget), 380);
		interactButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		interactButton.Visible = false;
		bottom.AddChild(interactButton);
		controlHint = UiKit.Hint("");
		controlHint.CustomMinimumSize = new Vector2(760, 0);
		var hintBar = UiKit.HintBar(controlHint);
		hintBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		bottom.AddChild(hintBar);
		uiRoot.AddChild(bottom);
	}

	void Stop(string text)
	{
		title.Text = text;
		GD.PushError(text);
	}

	static Result<GameRules> LoadRules()
	{
		using var file = FileAccess.Open(RulesPath, FileAccess.ModeFlags.Read);
		if (file is null)
			return Result<GameRules>.Fail($"Não foi possível abrir {RulesPath}: {FileAccess.GetOpenError()}");

		return GameRules.Parse(file.GetAsText());
	}

	// Abre o save; se precisou recuperá-lo ou migrá-lo, grava o estado novo para o problema não se repetir
	// e avisa o Responsável na tela.
	Result<SaveData> OpenSave(SaveStore store)
	{
		var loaded = store.Load();
		if (!loaded.IsOk) return Result<SaveData>.Fail(loaded.Error!);

		var outcome = loaded.Value!;
		if (outcome.Notice == SaveNotice.None) return Result<SaveData>.Ok(outcome.Data);

		var written = store.Write(outcome.Data);
		if (!written.IsOk) return written;

		if (outcome.Notice == SaveNotice.Migrated)
			GD.Print($"Save migrado; o original está em {outcome.CopyPath}");
		else
		{
			notice.Text = outcome.Notice == SaveNotice.Recovered
				? "Não foi possível ler o progresso anterior. Ele foi guardado numa cópia e o jogo começou do zero."
				: "Este progresso é de uma versão mais nova do jogo. Ele foi guardado numa cópia e o jogo começou do zero.";
			GD.PushWarning(notice.Text);
		}
		return written;
	}

	// Aplica as configurações em tudo que já está na tela; é chamado ao abrir o jogo e a cada mudança.
	void ApplySettings(GameSettings s)
	{
		uiScale = s.TextScale;
		FitUiRoot();

		Font? font = null;
		if (s.DyslexiaFont && GD.Load<FontFile>(DyslexiaFontPath) is { } file)
			font = new FontVariation { BaseFont = file, SpacingGlyph = DyslexiaLetterSpacing };
		uiRoot.Theme = font is null ? null : new Theme { DefaultFont = font };
		lobby.SetFont(font);
		dummy.SetFont(font);

		player.ReduceMotion = s.ReduceMotion || (OS.IsDebugBuild() && OS.GetCmdlineUserArgs().Contains("--reduzir-animacoes"));
		lobby.ReduceMotion = player.ReduceMotion;
		dummy.ReduceMotion = player.ReduceMotion;
		controlHint.Text = ControlHints.For(s.Controls);
		interactTarget = null; // força o botão de interação a refazer o texto com a dica nova
	}

	// Um só Perfil entra direto; zero ou vários passam pela tela de Perfis.
	void StartFlow()
	{
		if (profiles.Profiles.Count == 1) Enter(profiles.Profiles[0]);
		else ShowProfiles();
	}

	void ShowProfiles()
	{
		if (screen is not null || overlay is not null || editor is not null) return;
		screen = new ProfileScreen(profiles, canCancel: current is not null);
		screen.Chosen += Enter;
		screen.Cancelled += () =>
		{
			screen?.QueueFree();
			screen = null;
			// Um apelido mudado na tela de Perfis tem que aparecer no título e nas configurações.
			if (current is not null && profiles.Find(current.Id) is { IsOk: true } fresh) Enter(fresh.Value!);
			else UpdatePlayerControl();
		};
		uiRoot.AddChild(screen);
		UpdatePlayerControl();
	}

	void Enter(ProfileSave profile)
	{
		screen?.QueueFree();
		screen = null;
		editor?.QueueFree();
		editor = null;
		current = profile;
		characterButton.Visible = true;
		title.Text = $"{rules.GameName}  ·  {profile.Nickname}";
		player.Visible = true;
		ApplyAppearance();
		UpdatePlayerControl();
		GD.Print($"Perfil ativo: {profile.Nickname}");
	}

	void ShowEditor()
	{
		if (editor is not null || screen is not null || overlay is not null || current is null) return;
		editor = new CharacterEditor(profiles, current.Id);
		editor.Changed += ApplyAppearance;
		editor.Done += () =>
		{
			editor?.QueueFree();
			editor = null;
			UpdatePlayerControl();
		};
		uiRoot.AddChild(editor);
		UpdatePlayerControl();
	}

	void ShowSettings() => ShowOverlay(new SettingsScreen(settings, voice, current?.Nickname, ShowProfiles));

	void ShowOverlay(OverlayScreen next)
	{
		if (overlay is not null || screen is not null || editor is not null)
		{
			next.QueueFree();
			return;
		}
		overlay = next;
		next.Closed += () =>
		{
			next.QueueFree();
			overlay = null;
			UpdatePlayerControl();
		};
		uiRoot.AddChild(next);
		UpdatePlayerControl();
	}

	void Interact(string? target)
	{
		if (target is null || !player.Enabled) return;
		switch (target)
		{
			case "album": ShowOverlay(new AlbumScreen()); break;
			case "portal": ShowOverlay(new PortalScreen()); break;
			case "guia": ShowGuide(); break;
			case "boneco": Attack(); break;
			default: GD.PushWarning($"Objeto do Lobby sem tela: {target}"); break;
		}
	}

	void ShowGuide()
	{
		var guide = rules.Lobby.Objects.First(o => o.Id == "guia");
		if (guide.Lines.Count == 0)
		{
			GD.PushWarning("O Guia não tem falas em rules.json.");
			return;
		}
		ShowOverlay(new GuideScreen(guide.Name, guide.Lines, voice));
	}


	// O Boneco de treino: o escudo vem de uma Missão carregada do build. O treino não grava nada no save.
	void SetUpTraining()
	{
		time = new CombatTime(rules.Combat);
		dummy = new TrainingDummy();
		lobby.AddChild(dummy);
		var (ground, top) = lobby.FootprintOf("boneco");
		dummy.Place(ground, top);

		var started = TrainingBattle.Start(rules.Combat, MissionSource.Load().Missions, new Pcg32((ulong)Time.GetTicksUsec()));
		if (started.IsOk)
		{
			battle = started.Value;
			dummy.Reset(battle!.Shield.Remaining);
		}
		else GD.PushWarning(started.Error!);
	}

	// Chegar perto e atacar abre a montagem do cajado no próprio cenário. Enquanto ela está aberta o tempo
	// desacelera (ou para, no Modo calmo) e volta ao normal ao confirmar ou cancelar.
	void Attack()
	{
		if (battle is null)
		{
			notice.Text = "O Boneco ainda não tem uma Missão de treino.";
			return;
		}
		if (battle.Shield.IsDissolved) return; // a névoa ainda está sumindo; o novo escudo chega em instantes
		var panel = new StaffPanel(battle, rules.Combat, voice);
		ShowOverlay(panel);
		if (overlay != panel) return;

		time.Begin(settings.Current.CalmMode);
		Engine.TimeScale = time.Scale;
		// O tempo volta ao normal assim que o painel fecha, e também se ele sair da cena por outro caminho.
		var ended = false;
		void EndAssembly()
		{
			if (ended) return;
			ended = true;
			time.End();
			Engine.TimeScale = time.Scale;
		}
		panel.Closed += () =>
		{
			EndAssembly();
			if (panel.Result is { } result) ShowAttack(result);
		};
		panel.TreeExited += EndAssembly;
	}

	void ShowAttack(AttackResult result)
	{
		var from = player.GlobalPosition + Vector3.Up * 1f;
		string text;
		switch (result.Outcome)
		{
			case HitOutcome.Dissolved:
				dummy.Ray(from, perfect: true);
				dummy.Dissolved();
				text = $"Golpe perfeito! {result.Expression.Total} dissolveu a névoa.";
				dummy.Say(text, good: true);
				GetTree().CreateTimer(3.0, true, false, true).Timeout += NextShield;
				break;
			case HitOutcome.Reduced:
				dummy.Ray(from, perfect: false);
				dummy.Reduced(result.Remaining);
				text = $"Acertou! Tirou {result.Expression.Total} do escudo. Faltam {result.Remaining}.";
				dummy.Say(text, good: true);
				break;
			default:
				dummy.Overshoot();
				text = $"Passou do valor: {result.Expression.Total} é mais que {result.Remaining}. Tente outra conta.";
				dummy.Say(text, good: false);
				break;
		}
		voice.Say(text, interrupt: true);
	}

	void NextShield()
	{
		if (battle is null) return;
		battle.Next();
		dummy.Reset(battle.Shield.Remaining);
	}

	// Até existir o treino de Português (Papel e Nível do treino), o F2 abre a montagem do Escriba só em build de depuração.
	void ShowNameDemo()
	{
		var pieces = SyllableBag.Deal(["ga", "to"], ["pa", "li", "ma", "ru", "ne"], 3, new Pcg32((ulong)Time.GetTicksUsec()));
		if (pieces.IsOk) ShowOverlay(new NamePanel(["ga", "to"], pieces.Value!, rules.Combat.Tracing, voice));
	}

	public override void _ExitTree() => Engine.TimeScale = 1.0;

	// O Personagem do Lobby usa sempre a aparência gravada do Perfil ativo.
	void ApplyAppearance()
	{
		if (current is null) return;
		player.Apply(profiles.AppearanceOf(profiles.Find(current.Id).Value!));
	}

	// Com uma tela aberta, teclado e toque são da tela, não do Personagem.
	void UpdatePlayerControl()
	{
		player.Enabled = current is not null && screen is null && editor is null && overlay is null;
		bottomBar.Visible = player.Enabled;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(Controls.Interact)) Interact(interactTarget);
		if (@event.IsActionPressed(Controls.Attack) && interactTarget == "boneco") Attack();
		if (OS.IsDebugBuild() && @event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F2 } && player.Enabled) ShowNameDemo();
	}

	// Chegar perto de um objeto mostra o botão com o nome da ação e a seta sobre o objeto; a tecla E aciona o mesmo.
	public override void _Process(double delta)
	{
		if (player is null) return;
		var target = player.Enabled
			? InteractionFinder.Nearest(player.GroundPosition, lobby.Interactables, rules.Lobby.InteractionReach)?.Id
			: null;
		if (target != interactTarget)
		{
			interactTarget = target;
			lobby.Highlight(target);
			interactButton.Visible = target is not null;
			if (target is not null)
			{
				var prompt = rules.Lobby.Objects.First(o => o.Id == target).Prompt;
				var key = target == "boneco" ? "F" : "E";
				interactButton.Text = settings.Current.Controls == "teclado" ? $"[{key}]  {prompt}" : prompt;
			}
		}

	}
}
