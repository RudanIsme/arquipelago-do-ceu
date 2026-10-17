using System;
using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Data;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rewards;
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
	const string DyslexiaFontPath = "res://assets/fontes/OpenDyslexic-Regular.otf";
	const int DyslexiaLetterSpacing = 1;

	CanvasLayer ui = null!;
	Control uiRoot = null!;
	RootUi rootUi = null!;
	Label title = null!;
	Label notice = null!;
	GameRules rules = null!;
	ProfileService profiles = null!;
	SettingsService settings = null!;
	VoiceService voice = null!;
	ProfileScreen? screen;
	CharacterEditor? editor;
	OverlayScreen? overlay;
	TrainingArea training = null!;
	ProfileSave? current;
	Lobby lobby = null!;
	Player player = null!;
	string? interactTarget;
	float uiScale = 1f;
	TrainingDummy dummy = null!;

	readonly IRandomSource random = new GodotRandomSource();
	readonly CatalogLoader catalogs = new(new GodotDataSource(), message => GD.PushWarning(message));

	public override void _Ready()
	{
		ui = GetNode<CanvasLayer>("Interface");
		BuildUiRoot();

		var loadedRules = LoadRules();
		if (!loadedRules.IsOk) { Stop(loadedRules.Error!); return; }
		rules = loadedRules.Value!;
		var loadedTowers = catalogs.Required("torres.json", json => TowerCatalog.Parse(json, MissionLoader.KnownMechanics));
		if (!loadedTowers.IsOk) { Stop(loadedTowers.Error!); return; }
		towers = loadedTowers.Value!;
		role = towers.All[0].Id;
		title.Text = rules.GameName;
		GD.Print($"Regras carregadas: {rules.GameName}");

		var store = new SaveStore(ProjectSettings.GlobalizePath(SaveOpener.ChoosePath()));
		var save = SaveOpener.Open(store);
		if (!save.IsOk) { Stop(save.Error!); return; }
		if (save.Value!.Notice is { } saveNotice)
		{
			notice.Text = saveNotice;
			GD.PushWarning(saveNotice);
		}
		var session = new SaveSession(store, save.Value.Data);
		progress = ProgressServices.Create(session);
		rewardService = new RewardService(new ProgressStore(session));
		inventory = new ArquipelagoDoCeu.Equipment.InventoryService(session, LoadCosmetics());
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

		BuildLobbyHud();
		ApplyTheme();
		settings.Changed += ApplySettings;
		ApplySettings(settings.Current);
		StartFlow();
		if (voice.MissingVoiceNotice is { } missing && string.IsNullOrEmpty(notice.Text))
		{
			notice.Text = missing;
			GD.PushWarning(missing);
		}
		if (OS.IsDebugBuild()) DevDriver.AttachIfRequested(this, new PilotPort(progress, rewardService, gear, inventory, master, training, () => current?.Id ?? "", UnlockedLevel));
		CallDeferred(nameof(InstallPullTest));

		GD.Print("Cena inicial pronta.");
	}

	void BuildUiRoot()
	{
		rootUi = new RootUi(ui);
		uiRoot = rootUi.Root;
		title = rootUi.Title;
		notice = rootUi.Notice;
		GetViewport().SizeChanged += FitUiRoot;
		FitUiRoot();
	}

	void FitUiRoot() => rootUi.Fit(GetViewport(), uiScale, compensateStretch: true);


	void Stop(string text)
	{
		title.Text = text;
		GD.PushError(text);
	}

	Result<GameRules> LoadRules() => catalogs.Required("rules.json", GameRules.Parse);

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
		dungeon?.Leave("");
		if (current?.Id != profile.Id) training.End();
		current = profile;
		title.Text = $"{rules.GameName}  ·  {profile.Nickname}";
		player.Visible = true;
		ApplyAppearance();
		LoadProfileLook();
		UpdatePlayerControl();
		GD.Print($"Perfil ativo: {profile.Nickname}");
	}

	void ShowEditor()
	{
		DropTarget();
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
		DropTarget();
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
			case "album": ShowOverlay(new AlbumScreen(OwnedCosmetics())); break;
			case "portal": ShowOverlay(BuildPortal()); break;
			case "mestre": ShowMaster(); break;
			case var tower when tower.StartsWith("torre-", StringComparison.Ordinal) && towers.Find(tower["torre-".Length..]) is not null: EnterTower(tower["torre-".Length..]); break;
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
			UnlockedLevel, () => player.Enabled, ShowOverlay, Notify, random, towers);
		AddChild(training);
		training.RoleChosen += ChooseRole;
		training.EnergyBonusFor = papel => BonusesOf(papel).ExtraEnergy;
		master = new MasterChallenge(rules, training, uiRoot, progress, () => equipmentBonuses.ExtraEnergy, Notify, saved => current = saved, random, rewardService, LoadRewards, towers);
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

	public Task QuitAsync(int code = 0)
	{
		if (quitting) return Task.CompletedTask;
		quitting = true;
		return GameExit.QuitAsync(GetTree(), speech, code);
	}

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
		var free = current is not null && screen is null && editor is null && overlay is null;
		player.Enabled = free && !inDungeon;
		dungeon?.SetEnabled(free);
		RefreshLobbyVisibility();
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
		if (@event.IsActionPressed(Controls.Inventory) && Controls.TypingPanels == 0 && (inDungeon ? dungeon is { Player.Enabled: true } : player.Enabled)) ShowInventory();
		if (@event.IsActionPressed(Controls.Target) && inDungeon && dungeon is { Player.Enabled: true }) dungeon.Combat.ToggleTarget();
		else if (@event.IsActionPressed(Controls.Target) && player.Enabled) training.ToggleTarget();
	}

	// Chegar perto de um objeto mostra o botão com o nome da ação e a seta sobre o objeto; a tecla E aciona o mesmo.
	public override void _Process(double delta)
	{
		if (player is null || inDungeon) return;
		lobbyHud?.SetNotice(notice.Text);
		lobbyHud?.SetPlayState(player.Enabled && !training.HasTarget);
		dummy.SetCaptionContext(true, player.Enabled && training.InTargetRange());
		var target = player.Enabled
			? InteractionFinder.Nearest(player.GroundPosition, lobby.Interactables, rules.Lobby.InteractionReach + equipmentBonuses.InteractionReachBonus)?.Id
			: null;
		training.CheckRange();
		if (target != interactTarget)
		{
			interactTarget = target;
			lobby.Highlight(target);
			lobbyHud?.SetInteraction(target is null ? null : rules.Lobby.Objects.First(o => o.Id == target).Prompt + " [E]");
		}
	}
}
