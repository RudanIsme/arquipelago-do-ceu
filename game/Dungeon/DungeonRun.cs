using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using ArquipelagoDoCeu.World;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Tudo o que a Dungeon precisa de fora, reunido num lugar só para a Main não repassar uma lista de parâmetros.
public sealed record DungeonContext(
	GameRules Rules,
	string ProfileId,
	string Discipline,
	string TowerName,
	int Level,
	VoiceService Voice,
	SettingsService Settings,
	Control UiRoot,
	ProgressService Progress,
	FallbackFloors Fallback,
	int ExtraEnergy,
	Action<Player> ConfigurePlayer,
	int StartFloor = 1,
	bool AtFastTravel = false,
	Action<OverlayScreen>? ShowOverlay = null,
	bool Shortcut = false);

// Uma Expedição numa torre: liga as regras de core/ (Expedition) ao mundo 3D das Salas, à interface e ao combate.
// Nada aqui decide regra: quem manda passar, vencer ou subir é a Expedition; este nó só mostra e conta o que a criança faz.
public partial class DungeonRun : Node
{
	const float MessageSeconds = 4f;
	const float EndSeconds = 4f;

	readonly DungeonContext ctx;
	readonly IReadOnlyList<Mission> missions = MissionSource.Load().Missions;
	readonly Expedition expedition;
	readonly DungeonWorld world = new();
	readonly Control hud = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
	readonly Label header = new();
	readonly Label message = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
	readonly VBoxContainer mapBox = new();
	readonly Label mapLegend = new();
	readonly Label itemsLabel = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
	readonly EnergyView energyView = new("Voltar ao Lobby");
	Button interactButton = null!;
	FloorMap? map;
	Player player = null!;
	TrainingArea combat = null!;
	string? target;
	bool left;
	bool enabled = true;
	int messageEpoch;
	OverlayScreen? levelDone;
	// O Atalho de Nível: os Desafios sorteados, na ordem, e qual está em jogo.
	IReadOnlyList<TrainingSetup> shortcut = [];
	int shortcutIndex;

	public event Action<string>? Left;

	public DungeonRun(DungeonContext ctx)
	{
		this.ctx = ctx;
		var seed = WorldSeed(ctx.ProfileId);
		var plan = LevelPlan.Create(ctx.Rules.Dungeon, seed, ctx.Discipline, ctx.Level, GeneratorVersion);
		var floors = new FloorProvider(ctx.Rules.Dungeon, ctx.Fallback);
		expedition = Expedition.Start(floors, plan, seed, GeneratorVersion, new Energy(ctx.Rules.MaxEnergy + ctx.ExtraEnergy), HasTradeItem,
			ctx.StartFloor, ctx.AtFastTravel, ctx.Shortcut);
	}

	// Em build de depuração, `--seed-do-mundo=N` fixa a seed para o piloto repetir sempre os mesmos Andares.
	static ulong WorldSeed(string profileId) =>
		OS.IsDebugBuild() && OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--seed-do-mundo="))?["--seed-do-mundo=".Length..] is { } text && ulong.TryParse(text, out var forced)
			? forced
			: Seed.ForProfile(profileId);

	// Muda quando o gerador muda de jeito, para os Andares antigos não valerem para os novos (ADR 0003).
	const int GeneratorVersion = 1;


	public static string TowerName(string discipline) => discipline == TrainingSetup.Mathematics ? "Torre dos Números" : "Torre das Letras";

	// A outra torre: é dela o Item de troca que abre o Marco desta.
	// O Atalho vale até o Desafio épico ser vencido; depois a criança explora a torre como quiser.
	bool InShortcut => ctx.Shortcut && !expedition.Finished;

	string Other => ctx.Discipline == TrainingSetup.Mathematics ? TrainingSetup.Portuguese : TrainingSetup.Mathematics;

	bool HasTradeItem() => ctx.Progress.HasItem(ctx.ProfileId, Other, ctx.Level);

	public TrainingArea Combat => combat;

	public Player Player => player;

	public Expedition Expedition => expedition;

	public ProgressService Progress => ctx.Progress;

	public string ProfileId => ctx.ProfileId;

	public DungeonWorld World => world;

	// A Main liga e desliga o Personagem quando uma tela (configurações, perfis) abre por cima.
	public void SetEnabled(bool value)
	{
		enabled = value;
		if (player is not null) player.Enabled = value && !left;
		interactButton.Visible = value && target is not null;
	}

	public override void _Ready()
	{
		AddChild(world);
		world.Camera.MakeCurrent();
		world.Name = "Dungeon";

		player = new Player(world.Grid, world.Camera);
		ctx.ConfigurePlayer(player);
		AddChild(player);

		world.ReduceMotion = player.ReduceMotion;
		combat = new TrainingArea(ctx.Rules, world, player, world.Creature, ctx.Voice, ctx.Settings, ctx.UiRoot,
			() => ctx.Level, () => player.Enabled && !left, _ => { }, Say);
		combat.InDungeon = true;
		combat.Creature = CreatureWords.Wild;
		combat.EnergyTop = 12;
		AddChild(combat);

		BuildHud();
		ctx.UiRoot.AddChild(hud);
		ctx.UiRoot.AddChild(energyView);
		energyView.LeaveRequested += () => Leave("");
		energyView.Set(expedition.Energy.Current, expedition.Energy.Max);
		energyView.OffsetTop = 12;

		player.Enabled = enabled;
		if (ctx.Shortcut && !BeginShortcut()) return;
		// O Atalho não descobre Andares: quem erra joga o Nível normalmente, do Andar mais fundo que já conhecia.
		if (ctx.Shortcut) BuildMap();
		else DiscoverFloor();
		EnterRoom(expedition.CurrentRoom, -1);
		Say($"Bem-vinda à {ctx.TowerName}! Ande até uma porta e escolha entrar.");
	}

	void BuildHud()
	{
		hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		header.AddThemeFontSizeOverride("font_size", 20);
		header.AddThemeColorOverride("font_color", UiKit.TextColor);
		header.AutowrapMode = TextServer.AutowrapMode.WordSmart;

		// O mapa do Andar fica no canto, com a legenda das letras e formas.
		var panel = new PanelContainer { Name = "Mapa" };
		panel.AddThemeStyleboxOverride("panel", UiKit.Surface());
		panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		panel.OffsetLeft = -300;
		panel.OffsetRight = -12;
		panel.OffsetTop = 12;
		mapBox.AddThemeConstantOverride("separation", 4);
		mapLegend.Text = FloorMap.Legend + "\n★ Item de troca";
		mapLegend.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		mapLegend.AddThemeFontSizeOverride("font_size", 13);
		mapLegend.AddThemeColorOverride("font_color", UiKit.TextColor);
		mapBox.AddChild(header);
		mapBox.AddChild(mapLegend);
		itemsLabel.AddThemeFontSizeOverride("font_size", 15);
		itemsLabel.AddThemeColorOverride("font_color", UiKit.TextColor);
		mapBox.AddChild(itemsLabel);
		panel.AddChild(mapBox);
		hud.AddChild(panel);

		message.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		message.OffsetTop = -190;
		message.OffsetBottom = -110;
		message.OffsetRight = -310;
		message.AddThemeFontSizeOverride("font_size", 22);
		message.AddThemeColorOverride("font_color", Colors.White);
		UiKit.Outline(message);
		hud.AddChild(message);

		interactButton = UiKit.BigButton("", Activate, 380);
		interactButton.Visible = false;
		interactButton.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		interactButton.OffsetTop = -96;
		interactButton.OffsetBottom = -36;
		interactButton.OffsetLeft = -190;
		interactButton.OffsetRight = 190;
		hud.AddChild(interactButton);
	}

	// --- Salas

	void EnterRoom(int room, int from)
	{
		combat.EndFight();
		RefreshRoom();
		var back = from >= 0 ? world.DoorTo(from) : null;
		player.PlaceAt(back is null ? DungeonWorld.Center : DungeonWorld.InsideOf(back));
		StartFight();
		RefreshHud();
		var kind = expedition.Current.Kind;
		if (kind == RoomKind.Marco)
			Say(HasTradeItem() ? "O Marco brilhou! O Item de troca abriu o caminho." : $"Este é o Marco. Ele só abre com o Item de troca da {TowerName(Other)}.");
		else if (kind == RoomKind.FastTravel) FindFastTravel();
		else if (kind is RoomKind.Rest or RoomKind.Exit)
			Say($"Sala de {FloorMap.NameOf(kind).ToLowerInvariant()}. {(kind == RoomKind.Exit ? "A escada leva ao próximo Andar." : "Descanse um pouco, se quiser.")}");
	}

	// A tentativa do Atalho é gravada ao começar: sair no meio também a gasta, e cada Nível tem uma só.
	bool BeginShortcut()
	{
		var setups = DungeonCombat.ShortcutSetups(missions, ctx.Discipline, ctx.Level, ctx.Rules.Dungeon.ShortcutChallenges, new Pcg32((ulong)Time.GetTicksUsec()));
		if (!setups.IsOk)
		{
			// Sem Desafios para sortear, a criança nem chega a tentar: a tentativa não é gasta.
			GD.PushWarning(setups.Error!);
			Leave("Não foi possível abrir o Atalho agora.");
			return false;
		}
		var marked = ctx.Progress.MarkShortcutTried(ctx.ProfileId, ctx.Discipline, ctx.Level);
		if (!marked.IsOk) GD.PushWarning(marked.Error!);
		shortcut = setups.Value!;
		return true;
	}

	void RefreshRoom() =>
		world.Show(expedition.Floor, expedition.CurrentRoom, expedition.Cleared, Describe, RoomNote());

	// O que a criança precisa saber desta Sala sobre o Item de troca, em texto e com a estrela.
	string? RoomNote()
	{
		var current = expedition.Current;
		if (current.Kind == RoomKind.Marco)
			return HasTradeItem() ? $"★ O Item da {TowerName(Other)} abriu o Marco" : $"★ Falta o Item de troca da {TowerName(Other)}";
		return current.HoldsItem && !expedition.IsCleared(expedition.CurrentRoom) ? "★ Vença esta Sala para ganhar o Item de troca" : null;
	}

	(string Label, bool Locked) Describe(int room)
	{
		var name = FloorMap.NameOf(expedition.Floor.Rooms[room].Kind);
		return expedition.CanMoveTo(room) switch
		{
			Passage.MustClearRoom => ($"{name}\n(vença esta Sala)", true),
			Passage.NeedsItem => ($"{name}\n(precisa do Item)", true),
			_ => (name, false),
		};
	}

	// Combate, Puzzle e épico têm uma criatura a vencer. A criança a escolhe (clique ou F) para abrir a montagem.
	void StartFight()
	{
		var current = expedition.Current;
		var needsFight = expedition.NeedsClearing(current) && current.Kind != RoomKind.Marco && !expedition.IsCleared(expedition.CurrentRoom);
		var puzzle = current.Kind == RoomKind.Puzzle;
		world.ShowTarget(needsFight, puzzle);
		if (!needsFight) return;

		// A porta do Puzzle não ataca: sem bolhas, só o Desafio e a Energia que os erros custam.
		combat.Bubbles = !puzzle && !InShortcut;
		combat.StopOnMistake = InShortcut ? OnShortcutFailed : null;
		combat.Creature = puzzle ? CreatureWords.Gate : CreatureWords.Wild;
		world.Creature.Title = combat.Creature.Title;
		world.Creature.RestingHint = puzzle ? "Escolha-a para abrir" : "Escolha-a para lutar";
		world.Creature.RestingCompact = puzzle ? "Abrir [F]" : "Lutar [F]";

		var setup = InShortcut ? Result<TrainingSetup>.Ok(shortcut[shortcutIndex]) : DungeonCombat.Setup(missions, ctx.Discipline, ctx.Level, expedition.Floor.Number);
		if (!setup.IsOk) { Say(setup.Error!); return; }
		var fight = CombatSession.Start(ctx.Rules.Combat, expedition.Energy, ctx.Level, setup.Value!, missions, new Pcg32((ulong)Time.GetTicksUsec()));
		if (!fight.IsOk) { Say(fight.Error!); return; }

		combat.BeginFight(fight.Value!, energyView, OnWon, OnLost);
		if (InShortcut)
		{
			Say($"Atalho de Nível! Desafio {shortcutIndex + 1} de {shortcut.Count}. Errar encerra a tentativa. Escolha a criatura (clique ou F).");
			return;
		}
		Say(current.Kind switch
		{
			RoomKind.Epic => "Desafio épico! Chegue perto da criatura e escolha-a (clique ou F) para atacar.",
			RoomKind.Puzzle => "Uma porta trancada! Chegue perto dela e escolha-a (clique ou F) para abrir.",
			_ => "Uma criatura da névoa! Chegue perto dela e escolha-a (clique ou F) para atacar.",
		});
	}

	void OnWon()
	{
		// No Atalho cada Desafio vencido leva ao seguinte; só o último vence o épico.
		if (InShortcut && shortcutIndex + 1 < shortcut.Count)
		{
			shortcutIndex++;
			StartFight();
			return;
		}

		var room = expedition.Current;
		// O Item é guardado antes de a Sala valer como vencida: se o save falhar, a criança tenta de novo e não perde o prêmio.
		if (room.HoldsItem && !GiveItem())
		{
			StartFight();
			return;
		}
		expedition.ClearCurrent();
		world.ShowTarget(false, false);
		RefreshRoom();
		RefreshHud();
		if (room.Kind == RoomKind.Epic)
		{
			ConcludeLevel();
			return;
		}
		if (room.HoldsItem) return;
		Say(expedition.Current.Kind == RoomKind.Puzzle ? "A porta abriu! O caminho está livre." : "Sala vencida! As portas estão livres.");
	}

	// Só para o piloto de desenvolvimento: vence a Sala sem resolver o Desafio, pela mesma saída da vitória de verdade.
	public void DevWinRoom()
	{
		if (!OS.IsDebugBuild()) return;
		combat.EndFight();
		OnWon();
	}

	// O Desafio épico vencido conclui o Nível nesta torre (também pelo Atalho, que ainda entrega o Item); a criança escolhe seguir.
	void ConcludeLevel()
	{
		var marked = ctx.Progress.MarkEpicDone(ctx.ProfileId, ctx.Discipline, ctx.Level);
		if (!marked.IsOk) GD.PushWarning(marked.Error!);
		var item = ctx.Shortcut && !ctx.Progress.HasItem(ctx.ProfileId, ctx.Discipline, ctx.Level) && GiveItem();
		RefreshHud();
		var text = $"Nível {ctx.Level} concluído! Você venceu o Desafio épico da {TowerName(ctx.Discipline)}.";
		Say(text);
		var screen = new LevelDoneScreen(TowerName(ctx.Discipline), ctx.Level, item, () => Leave(""));
		levelDone = screen;
		if (ctx.ShowOverlay is { } show) show(screen);
		else GetTree().CreateTimer(EndSeconds, true, false, true).Timeout += () => Leave("");
	}

	void OnShortcutFailed() =>
		Leave("O Atalho não deu certo desta vez, e cada Nível só deixa tentar uma vez. Entre pela porta para jogar o Nível normalmente.");

	bool GiveItem()
	{
		var saved = ctx.Progress.GiveItem(ctx.ProfileId, ctx.Discipline, ctx.Level);
		if (!saved.IsOk)
		{
			GD.PushWarning(saved.Error!);
			Say("Não foi possível guardar o Item agora. Vamos tentar de novo.");
			return false;
		}
		Say($"Você ganhou o Item de troca da {TowerName(ctx.Discipline)}! Leve-o à {TowerName(Other)} para abrir o Marco de lá.");
		return true;
	}

	void OnLost() => Leave("A névoa ficou forte demais desta vez. Vamos recarregar a energia?");

	// --- Portas e escada

	public override void _Process(double delta)
	{
		if (left) return;
		world.Creature.SetCaptionContext(UiKit.Compact, enabled && combat.InTargetRange());
		combat.CheckRange();
		energyView.Set(expedition.Energy.Current, expedition.Energy.Max);
		var near = enabled ? InteractionFinder.Nearest(player.GroundPosition, Interactables(), ctx.Rules.Lobby.InteractionReach) : null;
		if (near?.Id == target) return;
		target = near?.Id;
		interactButton.Visible = target is not null;
		if (target is not null) interactButton.Text = PromptFor(target);
	}

	List<Interactable> Interactables()
	{
		var list = world.Doors.Select(d => new Interactable($"door:{d.Target}", new Vector2(d.Ground.X, d.Ground.Z))).ToList();
		if (expedition.CanAscend) list.Add(new Interactable("stairs", DungeonWorld.StairsSpot));
		if (expedition.Current.Kind == RoomKind.Rest) list.Add(new Interactable("rest", DungeonWorld.FountainSpot));
		return list;
	}

	string PromptFor(string id)
	{
		if (id == "rest") return "Descansar [E]";
		if (id == "stairs") return $"Subir para o Andar {expedition.Floor.Number + 1} [E]";
		var room = int.Parse(id["door:".Length..]);
		return $"Ir para {FloorMap.NameOf(expedition.Floor.Rooms[room].Kind).ToLowerInvariant()} [E]";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (left || !@event.IsActionPressed(Controls.Interact)) return;
		Activate();
	}

	void Activate()
	{
		if (left || !enabled || target is null) return;
		if (target == "stairs") { Ascend(); return; }
		if (target == "rest") { Rest(); return; }

		if (InShortcut)
		{
			Say("Termine o Atalho ou volte ao Lobby.");
			return;
		}

		var room = int.Parse(target["door:".Length..]);
		var from = expedition.CurrentRoom;
		switch (expedition.MoveTo(room))
		{
			case Passage.Open:
				EnterRoom(room, from);
				break;
			case Passage.MustClearRoom:
				Say("Vença a criatura desta Sala para seguir. Escolha-a com clique ou F.");
				break;
			case Passage.NeedsItem:
				Say($"O Marco só abre com o Item de troca da {TowerName(Other)}.");
				break;
		}
	}

	// Chegar ao Fast travel salva o ponto: é por ele que a criança retoma pelo portal do Lobby.
	void FindFastTravel()
	{
		var saved = ctx.Progress.FindFastTravel(ctx.ProfileId, ctx.Discipline, ctx.Level, expedition.Floor.Number);
		if (!saved.IsOk) GD.PushWarning(saved.Error!);
		Say("Fast travel encontrado! O progresso foi salvo, e você pode voltar por aqui pelo portal do Lobby.");
	}

	void Rest()
	{
		var rested = expedition.UseRest(ctx.Rules.Dungeon.RestEnergy);
		energyView.Set(expedition.Energy.Current, expedition.Energy.Max);
		if (rested.IsOk) RefreshRoom();
		Say(rested.IsOk ? $"Você descansou e recuperou {rested.Value} de Energia." : rested.Error!);
	}

	void Ascend()
	{
		if (!expedition.Ascend()) return;
		DiscoverFloor();
		BuildMap();
		EnterRoom(expedition.CurrentRoom, -1);
		Say($"Andar {expedition.Floor.Number}!");
	}

	// O Andar descoberto fica no save; falha de gravação não trava a criança.
	void DiscoverFloor()
	{
		var saved = ctx.Progress.DiscoverFloor(ctx.ProfileId, ctx.Discipline, ctx.Level, expedition.Floor.Number);
		if (!saved.IsOk) GD.PushWarning(saved.Error!);
		BuildMap();
	}

	void BuildMap()
	{
		map?.QueueFree();
		map = new FloorMap(expedition.Floor, new Godot.Vector2(272, 300));
		mapBox.AddChild(map);
		mapBox.MoveChild(map, 1);
	}

	void RefreshHud()
	{
		header.Text = $"{ctx.TowerName} · Nível {ctx.Level} · Andar {expedition.Floor.Number} de {expedition.Plan.Floors.Count}";
		itemsLabel.Text = "Itens de troca\n" + string.Join("\n", new[] { TrainingSetup.Mathematics, TrainingSetup.Portuguese }
			.Select(d => $"{TowerName(d)}: {(ctx.Progress.HasItem(ctx.ProfileId, d, ctx.Level) ? "★ tenho" : "ainda não")}"));
		if (map is null) return;
		map.Here = expedition.CurrentRoom;
		map.Cleared = new HashSet<int>(expedition.Cleared);
	}

	// Texto na tela e na voz; some sozinho, e um recado novo nunca é apagado pelo temporizador de um antigo.
	void Say(string text)
	{
		message.Text = text;
		ctx.Voice.Say(text, interrupt: true);
		var mine = ++messageEpoch;
		GetTree().CreateTimer(MessageSeconds, true, false, true).Timeout += () =>
		{
			if (mine == messageEpoch && IsInstanceValid(message)) message.Text = "";
		};
	}

	// A criança volta ao Lobby por vontade própria, pelo fim da torre ou por falta de Energia; os Itens ficam com ela.
	public void Leave(string reason)
	{
		if (left) return;
		left = true;
		// Sair por outro botão com a escolha do Nível concluído aberta: a tela não pode ficar para trás.
		if (levelDone is not null && IsInstanceValid(levelDone)) levelDone.Close();
		combat.EndFight();
		Engine.TimeScale = 1.0;
		hud.QueueFree();
		energyView.QueueFree();
		Left?.Invoke(reason);
		QueueFree();
	}
}
