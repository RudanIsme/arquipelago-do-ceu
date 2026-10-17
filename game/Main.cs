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
	// O piloto de desenvolvimento usa um save próprio e descartável, para nunca mexer no progresso nem nas configurações reais.
	const string PilotSavePath = "user://piloto-save.json";
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
	TrainingArea training = null!;
	Button characterButton = null!;
	Button interactButton = null!;
	Control bottomBar = null!;
	ProfileSave? current;
	Lobby lobby = null!;
	Player player = null!;
	string? interactTarget;
	float uiScale = 1f;
	TrainingDummy dummy = null!;

	public override void _Ready()
	{
		ui = GetNode<CanvasLayer>("Interface");
		BuildUiRoot();

		var loadedRules = LoadRules();
		if (!loadedRules.IsOk) { Stop(loadedRules.Error!); return; }
		rules = loadedRules.Value!;
		title.Text = rules.GameName;
		GD.Print($"Regras carregadas: {rules.GameName}");

		var store = new SaveStore(ProjectSettings.GlobalizePath(ChooseSavePath()));
		var save = OpenSave(store);
		if (!save.IsOk) { Stop(save.Error!); return; }
		var session = new SaveSession(store, save.Value!);
		progress = new ProgressService(session);
		journeys = new ArquipelagoDoCeu.Equipment.JourneyService(session);
		gear = new ArquipelagoDoCeu.Equipment.GearService(session, LoadGear());
		profiles = new ProfileService(session, rules, () => Guid.NewGuid().ToString("N"));
		settings = new SettingsService(session, rules);
		// Fechar a janela passa por QuitAsync, para a voz terminar antes de o jogo sair.
		GetTree().AutoAcceptQuit = false;
		speech = new GodotSpeechEngine();
		voice = new VoiceService(speech, settings);

		Controls.Register();
		lobby = GD.Load<PackedScene>("res://Lobby/Lobby.tscn").Instantiate<Lobby>();
		lobby.Rules = rules.Lobby;
		AddChild(lobby);
		player = new Player(lobby.Grid, lobby.Camera, lobby.GroundHeight) { Visible = false };
		AddChild(player);
		AddChild(new CameraController(lobby.Camera, player, rules.Lobby.Camera, () => player.ReduceMotion));
		SetUpTraining();

		UiKit.SetVariant("base");
		BuildHud();
		UiKit.SetVariant(journey.Variant);
		BuildJourneyHud();
		settings.Changed += ApplySettings;
		ApplySettings(settings.Current);
		StartFlow();
		if (voice.MissingVoiceNotice is { } missing && string.IsNullOrEmpty(notice.Text))
		{
			notice.Text = missing;
			GD.PushWarning(missing);
		}
		DevDriver.AttachIfRequested(this);
		CallDeferred(nameof(InstallPullTest));

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
		var stretch = Mathf.Max(.1f, GetViewport().GetFinalTransform().Scale.X);
		var effectiveScale = journey.Variant == "base" ? uiScale : uiScale / stretch;
		uiRoot.Scale = Vector2.One * effectiveScale;
		uiRoot.Size = GetViewport().GetVisibleRect().Size / effectiveScale;
	}

	void BuildHud()
	{
		var hud = new HBoxContainer();
		legacyHud = hud;
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

	// Com o piloto de desenvolvimento, o jogo trabalha numa cópia do save: nada do que o piloto clica volta para o real.
	static string ChooseSavePath()
	{
		// O teste de ponta a ponta fecha e reabre o jogo no mesmo save, então `--save=` aponta um arquivo que fica de uma execução para a outra.
		if (OS.IsDebugBuild() && OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--save="))?["--save=".Length..] is { Length: > 0 } fixedPath) return fixedPath;
		if (!OS.IsDebugBuild() || !OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--driver="))) return SavePath;
		DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PilotSavePath));
		if (FileAccess.FileExists(SavePath)) DirAccess.CopyAbsolute(ProjectSettings.GlobalizePath(SavePath), ProjectSettings.GlobalizePath(PilotSavePath));
		return PilotSavePath;
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
		lobby.SetEffects(s.ReduceEffects);
		dummy.SetFont(font);

		player.ReduceMotion = s.ReduceMotion || (OS.IsDebugBuild() && OS.GetCmdlineUserArgs().Contains("--reduzir-animacoes"));
		if (dungeon is not null) dungeon.Player.ReduceMotion = player.ReduceMotion;
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
		DropTarget();
		if (screen is not null || overlay is not null || editor is not null || workshop is not null) return;
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
		dungeon?.Leave("");
		if (current?.Id != profile.Id) training.End();
		current = profile;
		characterButton.Visible = true;
		title.Text = $"{rules.GameName}  ·  {profile.Nickname}";
		player.Visible = true;
		ApplyAppearance();
		LoadJourney();
		UpdatePlayerControl();
		GD.Print($"Perfil ativo: {profile.Nickname}");
	}

	void ShowEditor()
	{
		DropTarget();
		if (editor is not null || screen is not null || overlay is not null || workshop is not null || current is null) return;
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
		DropTarget();
		if (overlay is not null || screen is not null || editor is not null || workshop is not null)
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
			case "album": ShowOverlay(new AlbumScreen(OwnedCosmetics())); break;
			case "portal": ShowOverlay(BuildPortal()); break;
			case "mestre": ShowMaster(); break;
			case "torre-matematica": EnterTower(TrainingSetup.Mathematics); break;
			case "torre-portugues": EnterTower(TrainingSetup.Portuguese); break;
			case "guia": ShowGuide(); break;
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


	// A Área de treino: o Boneco, a Energia própria e as telas de montagem vivem em TrainingArea, e nada vai para o save.
	void SetUpTraining()
	{
		dummy = new TrainingDummy();
		lobby.AddChild(dummy);
		training = new TrainingArea(rules, lobby, player, dummy, voice, settings, uiRoot,
			UnlockedLevel, () => player.Enabled, ShowOverlay, Notify);
		AddChild(training);
		training.RoleChosen += ChooseRole;
		training.EnergyBonusFor = papel => BonusesOf(papel).ExtraEnergy;
		master = new MasterChallenge(rules, training, uiRoot, progress, () => equipmentBonuses.ExtraEnergy, Notify, saved => current = saved);
		AddChild(master);
	}


	// O Nível a que o Perfil tem acesso. Em build de depuração, `--nivel=N` o força, só para o piloto de desenvolvimento testar os Níveis 2 e 3.
	// O Nível sobe quando o Desafio mestre é vencido, então vale o do save, e não a cópia que a Main guardou ao entrar.
	int UnlockedLevel()
	{
		if (OS.IsDebugBuild() && OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--nivel="))?["--nivel=".Length..] is { } text && int.TryParse(text, out var forced))
			return forced;
		return current is null ? 1 : profiles.Find(current.Id) is { IsOk: true } fresh ? fresh.Value!.Level : current.Level;
	}
	// Avisos curtos sobre a cena somem sozinhos e também são ditos pela voz.
	void Notify(string text)
	{
		notice.Text = text;
		voice.Say(text, interrupt: true);
		GetTree().CreateTimer(3.0, true, false, true).Timeout += () =>
		{
			if (notice.Text == text) notice.Text = "";
		};
	}

	public override void _ExitTree() => Engine.TimeScale = 1.0;

	GodotSpeechEngine speech = null!;

	// Sai do jogo depois de a fala em andamento terminar (no máximo alguns segundos); o piloto de desenvolvimento usa o mesmo caminho.
	public async System.Threading.Tasks.Task QuitAsync(int code = 0)
	{
		if (quitting) return;
		quitting = true;
		speech?.Stop();
		var start = Time.GetTicksMsec();
		while (speech is { Silent: false } && Time.GetTicksMsec() - start < 6000)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		WatchExit(code);
		GetTree().Quit(code);
	}

	// O encerramento do TTS do sistema às vezes não termina e deixaria um processo preso e invisível.
	// Se o jogo não sair em poucos segundos, o vigia o encerra: nada fica pendente, o save é gravado a cada mudança.
	static void WatchExit(int code) =>
		new System.Threading.Thread(() =>
		{
			System.Threading.Thread.Sleep(8000);
			System.Environment.Exit(code);
		})
		{ IsBackground = true, Name = "Vigia da saída" }.Start();

	bool quitting;

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest) _ = QuitAsync();
	}

	// O Personagem do Lobby usa sempre a aparência gravada do Perfil ativo.
	void ApplyAppearance()
	{
		if (current is null) return;
		player.Apply(profiles.AppearanceOf(profiles.Find(current.Id).Value!));
	}

	// Com uma tela aberta, teclado e toque são da tela, não do Personagem.
	void UpdatePlayerControl()
	{
		var free = current is not null && screen is null && editor is null && overlay is null && workshop is null;
		player.Enabled = free && !inDungeon;
		dungeon?.SetEnabled(free);
		bottomBar.Visible = player.Enabled;
		RefreshJourneyVisibility();
	}

	// Clicar (ou tocar) na criatura a escolhe; o clique não vira caminhada. Cliques sobre a interface ficam com ela.
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click) return;
		var actor = inDungeon ? dungeon?.Player : player;
		var combat = dungeon?.Combat ?? training;
		if (actor is null || !actor.Enabled || GetViewport().GuiGetHoveredControl() is not null || !combat.HitsDummy(click.Position)) return;
		GetViewport().SetInputAsHandled();
		combat.ToggleTarget();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(Controls.Interact)) Interact(interactTarget);
		if (@event.IsActionPressed(Controls.Target) && inDungeon && dungeon is { Player.Enabled: true }) dungeon.Combat.ToggleTarget();
		else if (@event.IsActionPressed(Controls.Target) && player.Enabled) training.ToggleTarget();
	}

	// Chegar perto de um objeto mostra o botão com o nome da ação e a seta sobre o objeto; a tecla E aciona o mesmo.
	public override void _Process(double delta)
	{
		if (player is null || inDungeon) return;
		journeyHud?.SetNotice(notice.Text);
		journeyHud?.SetPlayState(player.Enabled && journey.Variant != "base" && !training.HasTarget);
		if (returnJourney is not null && journey.Variant == "base") returnJourney.Visible = player.Enabled && !training.HasTarget;
		dummy.SetCaptionContext(journey.Variant != "base", player.Enabled && training.InTargetRange());
		var target = player.Enabled
			? InteractionFinder.Nearest(player.GroundPosition, lobby.Interactables, rules.Lobby.InteractionReach + equipmentBonuses.InteractionReachBonus)?.Id
			: null;
		training.CheckRange();
		if (target != interactTarget)
		{
			interactTarget = target;
			lobby.Highlight(target);
			interactButton.Visible = target is not null;
			journeyHud?.SetInteraction(target is null ? null : rules.Lobby.Objects.First(o => o.Id == target).Prompt + " [E]");
			if (target is not null)
			{
				var prompt = rules.Lobby.Objects.First(o => o.Id == target).Prompt;

				interactButton.Text = settings.Current.Controls == "teclado" ? $"[E]  {prompt}" : prompt;
			}
		}

	}
}
