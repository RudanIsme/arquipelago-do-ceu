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
	ProgressServices Progress,
	FallbackFloors Fallback,
	CosmeticCatalog Catalog,
	string Nickname,
	string RoleName,
	int ExtraEnergy,
	float ExtraReach,
	Action<Player> ConfigurePlayer,
	IRandomSource Random,
	int StartFloor = 1,
	bool AtFastTravel = false,
	Action<OverlayScreen>? ShowOverlay = null,
	bool Shortcut = false,
	bool Free = false,
	ConceptNames? Concepts = null);

// Uma Expedição numa torre: liga as regras de core/ (Expedition) ao mundo 3D das Salas, à interface e ao combate.
// Nada aqui decide regra: quem manda passar, vencer ou subir é a Expedition; este nó só mostra e conta o que a criança faz.
public partial class DungeonRun : Node
{
	const float MessageSeconds = 4f;
	// A Energia fica logo abaixo do card do avatar.
	const int EnergyTop = 104;
	const float EndSeconds = 4f;

	readonly DungeonContext ctx;
	readonly IReadOnlyList<Mission> missions = MissionSource.Load().Missions;
	readonly Expedition expedition;
	// Na Expedição livre os problemas já vistos ficam de fora, e cada Andar paga as moedas dele uma vez só.
	readonly MissionHistory history = new();
	readonly HashSet<int> paidFloors = [];
	readonly PanelContainer plate = new() { Name = "Placa" };
	string? masteryNote;
	(int Floor, TrainingSetup Setup)? fightSetup;
	readonly DungeonWorld world = new();
	readonly Control hud = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
	readonly Label header = new();
	readonly Label message = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
	readonly VBoxContainer mapBox = new();
	AvatarCard card = null!;
	readonly Label coinToast = new() { Name = "AvisoDeMoedas", MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
	int toastEpoch;
	readonly Label floorLabel = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
	readonly ProgressBar floorBar = new() { ShowPercentage = false, CustomMinimumSize = new Godot.Vector2(0, 14) };
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
	(int Floor, int Visited, int Cleared) lastSaved;

	public event Action<string>? Left;

	public DungeonRun(DungeonContext ctx)
	{
		this.ctx = ctx;
		var seed = WorldSeed(ctx.ProfileId);
		var plan = ctx.Free ? LevelPlan.Free(ctx.Discipline, ctx.Level) : LevelPlan.Create(ctx.Rules.Dungeon, seed, ctx.Discipline, ctx.Level, GeneratorVersion);
		var floors = new FloorProvider(ctx.Rules.Dungeon, ctx.Fallback);
		expedition = Expedition.Start(floors, plan, seed, GeneratorVersion, new Energy(ctx.Rules.MaxEnergy + ctx.ExtraEnergy), HasTradeItem,
			ctx.Free ? 1 : ctx.StartFloor, !ctx.Free && ctx.AtFastTravel, ctx.Shortcut, Explored);
	}

	// O Atalho não carrega nem guarda exploração: ele pula o caminho, e as Salas do caminho não foram jogadas. A Livre também
	// não: ela não mexe no que a Progressão guardou.
	bool Persists => !ctx.Shortcut && !ctx.Free;

	FloorExploration? Explored(int floor) =>
		!Persists ? null : ctx.Progress.Explored.Get(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, floor), GeneratorVersion);

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

	bool HasTradeItem() => ctx.Progress.Levels.HasItem(ctx.ProfileId, new LevelKey(Other, ctx.Level));

	public TrainingArea Combat => combat;

	public Player Player => player;

	public Expedition Expedition => expedition;

	public MissionHistory History => history;

	// O último aviso de moedas (fica guardado mesmo depois de sumir da tela), para o piloto conferir.
	public string CoinToast => coinToast.Text;

	public ProgressServices Progress => ctx.Progress;

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
			() => ctx.Level, () => player.Enabled && !left, _ => { }, Say, ctx.Random);
		combat.InDungeon = true;
		combat.Creature = CreatureWords.Wild;
		combat.EnergyTop = EnergyTop;
		combat.ChallengeSolved += OnSolved;
		AddChild(combat);

		BuildHud();
		ctx.UiRoot.AddChild(hud);
		// O card (avatar, apelido e Maestria) fica no canto; a Energia, em raios, logo abaixo.
		card = new AvatarCard(text => ctx.Voice.Say(text, interrupt: true), canEdit: false);
		hud.AddChild(card);
		BuildCoinToast();
		ShowCoinToast($"Moedas: {ctx.Progress.Wallet.Balance(ctx.ProfileId)}");
		ctx.UiRoot.AddChild(energyView);
		energyView.LeaveRequested += () => Leave("");
		energyView.Set(expedition.Energy.Current, expedition.Energy.Max);
		energyView.OffsetTop = EnergyTop;

		player.Enabled = enabled;
		if (ctx.Shortcut && !BeginShortcut()) return;
		// O Atalho não descobre Andares: quem erra joga o Nível normalmente, do Andar mais fundo que já conhecia.
		if (ctx.Shortcut) BuildMap();
		else DiscoverFloor();
		EnterRoom(-1);
		Say(ctx.Free
			? $"Expedição livre na {ctx.TowerName}! Os Andares não têm fim: volte ao Lobby quando quiser."
			: $"Bem-vinda à {ctx.TowerName}! Ande até uma porta e escolha entrar.");
	}

	void BuildHud()
	{
		hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		header.AddThemeFontSizeOverride("font_size", 18);
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
		floorLabel.AddThemeFontSizeOverride("font_size", 15);
		floorLabel.AddThemeColorOverride("font_color", UiKit.TextColor);
		mapBox.AddChild(floorBar);
		mapBox.AddChild(floorLabel);
		mapBox.AddChild(UiKit.BigButton("Ouvir o mapa", () => Say(MapSpeech()), 200));
		panel.AddChild(mapBox);
		hud.AddChild(panel);

		// A placa do centro: Nível, Andar e o objetivo (o Conceito do Andar, que vem das Missões).
		plate.AddThemeStyleboxOverride("panel", UiKit.Surface());
		plate.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		plate.OffsetLeft = -200;
		plate.OffsetRight = 200;
		plate.OffsetTop = 12;
		plate.GrowHorizontal = Control.GrowDirection.Both;
		header.HorizontalAlignment = HorizontalAlignment.Center;
		plate.AddChild(header);
		hud.AddChild(plate);

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

	// `lead` abre a fala de quem acabou de mudar de Andar.
	void EnterRoom(int from, string? lead = null)
	{
		combat.EndFight();
		RefreshRoom();
		var back = from >= 0 ? world.DoorTo(from) : null;
		player.PlaceAt(back is null ? DungeonWorld.Center : DungeonWorld.InsideOf(back));
		StartFight();
		RefreshHud();
		SaveExploration();
		var kind = expedition.Current.Kind;
		var text = kind switch
		{
			RoomKind.Marco => HasTradeItem() ? "O Marco brilhou! O Item de troca abriu o caminho." : $"Este é o Marco. Ele só abre com o Item de troca da {TowerName(Other)}.",
			RoomKind.FastTravel => FindFastTravel(),
			RoomKind.Rest => "Sala de descanso. Descanse um pouco, se quiser.",
			RoomKind.Exit => "Sala de saída. A escada leva ao próximo Andar.",
			_ => null,
		};
		var spoken = string.Join(" ", new[] { lead, text, CheckFloorComplete() }.Where(t => t is not null));
		if (spoken.Length != 0) Say(spoken);
	}

	// A tentativa do Atalho é gravada ao começar: sair no meio também a gasta, e cada Nível tem uma só.
	bool BeginShortcut()
	{
		var setups = DungeonCombat.ShortcutSetups(missions, ctx.Discipline, ctx.Level, ctx.Rules.Dungeon.ShortcutChallenges, ctx.Random.NewStream());
		if (!setups.IsOk)
		{
			// Sem Desafios para sortear, a criança nem chega a tentar: a tentativa não é gasta.
			GD.PushWarning(setups.Error!);
			Leave("Não foi possível abrir o Atalho agora.");
			return false;
		}
		var marked = ctx.Progress.Levels.MarkShortcutTried(ctx.ProfileId, new LevelKey(ctx.Discipline, ctx.Level));
		if (!marked.IsOk) GD.PushWarning(marked.Error!);
		shortcut = setups.Value!;
		return true;
	}

	void RefreshRoom() =>
		world.Show(expedition.Floor, expedition.CurrentRoom, expedition.Cleared, Describe, RoomNote(), expedition.CanDescend);

	// O que a criança precisa saber desta Sala sobre o Item de troca, em texto e com a estrela.
	string? RoomNote()
	{
		var current = expedition.Current;
		if (current.Kind == RoomKind.Marco)
			return HasTradeItem() ? $"★ O Item da {TowerName(Other)} abriu o Marco" : $"★ Falta o Item de troca da {TowerName(Other)}";
		return current.HoldsItem && !expedition.IsCleared(expedition.CurrentRoom) ? "★ Vença esta Sala para ganhar o Item de troca" : null;
	}

	// A porta de uma Sala que a criança ainda não visitou não diz o que há lá dentro.
	(string Label, bool Locked, bool Known) Describe(int room)
	{
		var known = expedition.Visited.Contains(room);
		var name = known ? FloorMap.NameOf(expedition.Floor.Rooms[room].Kind) : "Sala nova";
		return expedition.CanMoveTo(room) switch
		{
			Passage.MustClearRoom => ($"{name}\n(vença esta Sala)", true, known),
			Passage.NeedsItem => ($"{name}\n(precisa do Item)", true, known),
			_ => (name, false, known),
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

		var setup = InShortcut ? Result<TrainingSetup>.Ok(shortcut[shortcutIndex])
			: ctx.Free ? DungeonCombat.FreeSetup(missions, history, ctx.Discipline, ctx.Level, expedition.Floor.Number)
			: DungeonCombat.Setup(missions, ctx.Discipline, ctx.Level, expedition.Floor.Number);
		if (!setup.IsOk) { Say(setup.Error!); return; }
		if (!InShortcut) fightSetup = (expedition.Floor.Number, setup.Value!);
		var fight = CombatSession.Start(ctx.Rules.Combat, expedition.Energy, ctx.Level, setup.Value!, missions, ctx.Random.NewStream(), ctx.Free ? history : null);
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
		var paying = !InShortcut;
		expedition.ClearCurrent();
		world.ShowTarget(false, false);
		RefreshRoom();
		SaveExploration();
		var coins = paying ? PayCoins((room.Kind is RoomKind.Combat or RoomKind.Epic ? ctx.Rules.Dungeon.CoinsCreature : 0) + (room.HoldsItem ? ctx.Rules.Dungeon.CoinsItem : 0)) : null;
		var mastery = masteryNote;
		masteryNote = null;
		var completed = CheckFloorComplete();
		RefreshHud();
		if (room.Kind == RoomKind.Epic)
		{
			ConcludeLevel(Join(coins, mastery, completed));
			return;
		}
		if (room.HoldsItem)
		{
			// O recado do Item já está na tela; o resto vem junto, sem apagá-lo.
			var extra = Join(coins, mastery, completed);
			if (extra is not null) Say($"{message.Text} {extra}");
			return;
		}
		var won = expedition.Current.Kind == RoomKind.Puzzle ? "A porta abriu! O caminho está livre." : "Sala vencida! As portas estão livres.";
		Say(Join(won, coins, mastery, ProgressSpeech(), completed)!);
	}

	static string? Join(params string?[] parts) => parts.Any(p => p is not null) ? string.Join(" ", parts.Where(p => p is not null)) : null;

	// A vitória num Conceito conta para a Maestria do Papel (Dungeon, nunca treino); o aviso sai junto com o resto da Sala.
	void OnSolved(Mission mission)
	{
		// O Atalho pula o caminho: não rende moedas nem Maestria, para as duas recompensas seguirem a mesma regra.
		if (InShortcut) return;
		var update = ctx.Progress.Mastery.Record(ctx.ProfileId, new ConceptKey(ctx.Discipline, mission.Concept), ctx.Rules.Dungeon.ConceptSolves);
		if (!update.IsOk)
		{
			GD.PushWarning(update.Error!);
			return;
		}
		if (!update.Value!.NewlyMastered) return;
		var name = (ctx.Concepts ?? ConceptNames.Empty).NameOf(mission.Concept);
		masteryNote = $"Você dominou \"{name}\"! Maestria {update.Value.Mastery}.";
	}

	// Moeda é só do jogo, ganha por aprendizado e exploração; falha de gravação não trava a criança.
	string? PayCoins(int amount)
	{
		if (amount <= 0) return null;
		var paid = ctx.Progress.Wallet.Add(ctx.ProfileId, amount);
		if (!paid.IsOk)
		{
			GD.PushWarning(paid.Error!);
			return null;
		}
		ShowCoinToast($"+{(amount == 1 ? "1 moeda" : $"{amount} moedas")}");
		return $"Você ganhou {(amount == 1 ? "1 moeda" : $"{amount} moedas")}.";
	}

	// Só para o piloto de desenvolvimento: vence a Sala sem resolver o Desafio, pela mesma saída da vitória de verdade.
	public void DevWinRoom()
	{
		if (!OS.IsDebugBuild()) return;
		var mission = combat.CurrentMission;
		combat.EndFight();
		if (mission is not null) OnSolved(mission);
		OnWon();
	}

	// O Desafio épico vencido conclui o Nível nesta torre (também pelo Atalho, que ainda entrega o Item); a criança escolhe seguir.
	void ConcludeLevel(string? extra = null)
	{
		var marked = ctx.Progress.Levels.MarkEpicDone(ctx.ProfileId, new LevelKey(ctx.Discipline, ctx.Level));
		if (!marked.IsOk) GD.PushWarning(marked.Error!);
		var item = ctx.Shortcut && !ctx.Progress.Levels.HasItem(ctx.ProfileId, new LevelKey(ctx.Discipline, ctx.Level)) && GiveItem();
		RefreshHud();
		var text = $"Nível {ctx.Level} concluído! Você venceu o Desafio épico da {TowerName(ctx.Discipline)}." + (extra is null ? "" : " " + extra);
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
		var saved = ctx.Progress.Levels.GiveItem(ctx.ProfileId, new LevelKey(ctx.Discipline, ctx.Level));
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
		var near = enabled ? InteractionFinder.Nearest(player.GroundPosition, Interactables(), ctx.Rules.Lobby.InteractionReach + ctx.ExtraReach) : null;
		if (near?.Id == target) return;
		target = near?.Id;
		interactButton.Visible = target is not null;
		if (target is not null) interactButton.Text = PromptFor(target);
	}

	List<Interactable> Interactables()
	{
		var list = world.Doors.Select(d => new Interactable($"door:{d.Target}", new Vector2(d.Ground.X, d.Ground.Z))).ToList();
		if (expedition.CanAscend) list.Add(new Interactable("stairs", DungeonWorld.StairsSpot));
		if (expedition.CanDescend) list.Add(new Interactable("stairs-down", DungeonWorld.StairsSpot));
		if (expedition.Current.Kind == RoomKind.Rest) list.Add(new Interactable("rest", DungeonWorld.FountainSpot));
		return list;
	}

	string PromptFor(string id)
	{
		if (id == "rest") return "Descansar [E]";
		if (id == "stairs") return $"Subir para o Andar {expedition.Floor.Number + 1} [E]";
		if (id == "stairs-down") return $"Descer para o Andar {expedition.Floor.Number - 1} [E]";
		var room = int.Parse(id["door:".Length..], System.Globalization.CultureInfo.InvariantCulture);
		return expedition.Visited.Contains(room) ? $"Ir para {FloorMap.NameOf(expedition.Floor.Rooms[room].Kind).ToLowerInvariant()} [E]" : "Ir para a sala nova [E]";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (left || !@event.IsActionPressed(Controls.Interact)) return;
		Activate();
	}

	void Activate()
	{
		if (left || !enabled || target is null) return;
		if (target == "rest") { Rest(); return; }

		if (InShortcut)
		{
			Say("Termine o Atalho ou volte ao Lobby.");
			return;
		}

		if (target == "stairs") { ChangeFloor(expedition.Ascend); return; }
		if (target == "stairs-down") { ChangeFloor(expedition.Descend); return; }

		var room = int.Parse(target["door:".Length..], System.Globalization.CultureInfo.InvariantCulture);
		var from = expedition.CurrentRoom;
		switch (expedition.MoveTo(room))
		{
			case Passage.Open:
				EnterRoom(from);
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
	string FindFastTravel()
	{
		// Na Livre o ponto não é guardado: ele não pertence ao Plano do Nível.
		if (ctx.Free) return "Fast travel! Aqui você pode voltar ao Lobby quando quiser.";
		var saved = ctx.Progress.Levels.FindFastTravel(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, expedition.Floor.Number));
		if (!saved.IsOk) GD.PushWarning(saved.Error!);
		return "Fast travel encontrado! O progresso foi salvo, e você pode voltar por aqui pelo portal do Lobby.";
	}

	void Rest()
	{
		var rested = expedition.UseRest(ctx.Rules.Dungeon.RestEnergy);
		energyView.Set(expedition.Energy.Current, expedition.Energy.Max);
		if (rested.IsOk) RefreshRoom();
		Say(rested.IsOk ? $"Você descansou e recuperou {rested.Value} de Energia." : rested.Error!);
	}

	// Sobe ou desce a escada; o Andar de chegada volta como a criança o deixou.
	void ChangeFloor(Func<bool> move)
	{
		if (!move()) return;
		DiscoverFloor();
		EnterRoom(-1, $"Andar {expedition.Floor.Number}! {ProgressSpeech()}");
	}

	// O que a criança explorou fica no save a cada passo: ver o mapa sumir ao zerar a Energia seria punição.
	void SaveExploration()
	{
		if (!Persists) return;
		// Só grava quando algo mudou: voltar a uma Sala já visitada não precisa reescrever o save.
		var stamp = (expedition.Floor.Number, expedition.Visited.Count, expedition.Cleared.Count);
		if (stamp == lastSaved) return;
		var saved = ctx.Progress.Explored.Save(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, expedition.Floor.Number), expedition.Exploration(), GeneratorVersion);
		if (saved.IsOk) lastSaved = stamp;
		else GD.PushWarning(saved.Error!);
	}

	// Registra o Andar completo uma única vez e diz o que a criança ganhou; falha de gravação não trava ninguém.
	string? CheckFloorComplete()
	{
		var number = expedition.Floor.Number;
		if (ctx.Shortcut || !expedition.IsFloorComplete(number)) return null;
		// A Livre paga as moedas do Andar uma vez só, mas não registra Andar completo nem Cosmético: isso é do Plano do Nível.
		if (ctx.Free) return paidFloors.Add(number) ? Join($"Andar {number} completo!", PayCoins(ctx.Rules.Dungeon.CoinsFloor)) : null;
		if (ctx.Progress.Explored.IsFloorDone(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, number))) return null;
		var done = ctx.Progress.Rewards.CompleteFloor(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, number), ctx.Catalog);
		if (!done.IsOk)
		{
			GD.PushWarning(done.Error!);
			return null;
		}
		if (!done.Value!.NewlyDone) return null;
		var coins = PayCoins(ctx.Rules.Dungeon.CoinsFloor);
		return done.Value.Cosmetic is { } prize ? Join($"Andar {number} completo! Você ganhou um Cosmético: {prize.Name}.", coins) : Join($"Andar {number} completo!", coins);
	}

	string ProgressSpeech()
	{
		var progress = expedition.ProgressOf(expedition.Floor.Number);
		return $"{progress.Resolved} de {progress.Total} Salas resolvidas.";
	}

	// O que a voz diz de uma Sala do mapa: o que é (se já visitada), se está vencida, se tem cadeado. Sala sob névoa não revela o tipo.
	public string RoomSpeech(int room)
	{
		var kind = expedition.Floor.Rooms[room];
		var where = expedition.CurrentRoom == room ? " Você está aqui." : "";
		return expedition.SightOf(room) switch
		{
			Sight.Seen => $"Sala de {FloorMap.NameOf(kind.Kind).ToLowerInvariant()}{(expedition.IsCleared(room) ? ", vencida" : "")}.{where}",
			Sight.Fog => expedition.IsLocked(room) ? "Sala nova sob névoa, com cadeado: falta vencer uma Sala." : "Sala nova sob névoa.",
			_ => "Ainda não se vê nada aqui.",
		};
	}

	// O mapa dito em voz alta, para quem não lê a névoa e o cadeado.
	string MapSpeech()
	{
		var rooms = expedition.Floor.Rooms.Select(r => r.Id).ToList();
		var fog = rooms.Count(r => expedition.SightOf(r) == Sight.Fog);
		var locked = rooms.Count(expedition.IsLocked);
		var done = IsFloorDone() ? " Este Andar está completo." : "";
		var item = ctx.Free ? "" : $" Item de troca da {TowerName(Other)}: {(HasTradeItem() ? "você tem" : "ainda não")}.";
		return $"Andar {expedition.Floor.Number}{OfTotal()}. {ProgressSpeech()} Salas novas sob névoa: {fog}. Salas com cadeado: {locked}.{item}{done}";
	}

	bool IsFloorDone() => !ctx.Shortcut && (expedition.IsFloorComplete(expedition.Floor.Number) || (!ctx.Free && ctx.Progress.Explored.IsFloorDone(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, expedition.Floor.Number))));

	// O Andar descoberto fica no save; falha de gravação não trava a criança.
	void DiscoverFloor()
	{
		if (!ctx.Free)
		{
			var saved = ctx.Progress.Levels.DiscoverFloor(ctx.ProfileId, new FloorKey(ctx.Discipline, ctx.Level, expedition.Floor.Number));
			if (!saved.IsOk) GD.PushWarning(saved.Error!);
		}
		BuildMap();
	}

	void BuildMap()
	{
		map?.QueueFree();
		map = new FloorMap(expedition.Floor, new Godot.Vector2(272, 250)) { Source = expedition };
		map.RoomPointed += room => Say(RoomSpeech(room));
		mapBox.AddChild(map);
		mapBox.MoveChild(map, 0);
	}

	string OfTotal() => ctx.Free ? "" : $" de {expedition.Plan.Floors.Count}";

	// O objetivo da placa é o Conceito do Andar, lido das Missões e nomeado em dados; o jogo nunca o inventa.
	string Objective()
	{
		// Na Livre a luta pode ter procurado outra dificuldade; o objetivo mostra o da luta de verdade.
		var setup = fightSetup is { } fight && fight.Floor == expedition.Floor.Number ? Result<TrainingSetup>.Ok(fight.Setup)
			: DungeonCombat.Setup(missions, ctx.Discipline, ctx.Level, expedition.Floor.Number);
		var concept = setup.IsOk ? DungeonCombat.ConceptOf(missions, setup.Value!) : null;
		var subject = ctx.Discipline == TrainingSetup.Mathematics ? "Matemática" : "Português";
		return concept is null ? subject : $"{subject}: {(ctx.Concepts ?? ConceptNames.Empty).NameOf(concept)}";
	}

	void RefreshHud()
	{
		header.Text = $"{(ctx.Free ? "Livre · " : "")}{ctx.TowerName} · Nível {ctx.Level} · Andar {expedition.Floor.Number}{OfTotal()}\n{Objective()}";
		card.Set(ctx.Nickname, ctx.RoleName, ctx.Progress.Mastery.Of(ctx.ProfileId, ctx.Discipline));
		var progress = expedition.ProgressOf(expedition.Floor.Number);
		var complete = IsFloorDone();
		floorBar.MaxValue = progress.Total;
		floorBar.Value = progress.Resolved;
		floorLabel.Text = $"{progress.Resolved} de {progress.Total} Salas resolvidas" + (complete ? "  ★ Andar completo" : "");
		if (map is null) return;
		map.Here = expedition.CurrentRoom;
		map.Cleared = new HashSet<int>(expedition.Cleared);
		map.Complete = complete;
		map.Refresh();
	}

	// Aviso curto das moedas ("+3 moedas"): some sozinho e nunca bloqueia o jogo nem o combate.
	void BuildCoinToast()
	{
		coinToast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		coinToast.OffsetLeft = -150;
		coinToast.OffsetRight = 150;
		coinToast.OffsetTop = 96;
		coinToast.Visible = false;
		coinToast.AddThemeFontSizeOverride("font_size", 26);
		coinToast.AddThemeColorOverride("font_color", Color.FromHtml("#FFD54F"));
		UiKit.Outline(coinToast);
		hud.AddChild(coinToast);
	}

	void ShowCoinToast(string text)
	{
		coinToast.Text = text;
		coinToast.Visible = true;
		var mine = ++toastEpoch;
		GetTree().CreateTimer(4f, true, false, true).Timeout += () =>
		{
			if (mine == toastEpoch && IsInstanceValid(coinToast)) coinToast.Visible = false;
		};
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
