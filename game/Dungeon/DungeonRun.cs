using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.World;
using Godot;
using static ArquipelagoDoCeu.DungeonRewards;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Uma Expedição numa torre: liga as regras de core/ (Expedition) ao mundo 3D das Salas, ao HUD e ao combate.
// Nada aqui decide regra: quem manda passar, vencer ou subir é a Expedition; este nó só mostra e conta o que a criança faz.
public partial class DungeonRun : Node
{
	const float EndSeconds = 4f;

	readonly DungeonContext ctx;
	readonly IReadOnlyList<Mission> missions = MissionSource.Load().Missions;
	readonly Expedition expedition;
	// Na Expedição livre os problemas já vistos ficam de fora.
	readonly MissionHistory history = new();
	readonly DungeonWorld world = new();
	readonly DungeonHud hud;
	readonly DungeonNarration narration;
	readonly DungeonRewards rewards;
	readonly DungeonExploration exploration;
	(int Floor, TrainingSetup Setup)? fightSetup;
	Player player = null!;
	TrainingArea combat = null!;
	string? target;
	bool left;
	bool enabled = true;
	OverlayScreen? levelDone;
	// O Atalho de Nível: os Desafios sorteados, na ordem, e qual está em jogo.
	IReadOnlyList<TrainingSetup> shortcut = [];
	int shortcutIndex;

	public event Action<string>? Left;

	public DungeonRun(DungeonContext ctx)
	{
		this.ctx = ctx;
		var seed = ctx.WorldSeed;
		var plan = ctx.Free ? LevelPlan.Free(ctx.Discipline, ctx.Level) : LevelPlan.Create(ctx.Rules.Dungeon, seed, ctx.Discipline, ctx.Level, DungeonContext.GeneratorVersion);
		var floors = new FloorProvider(ctx.Rules.Dungeon, ctx.Fallback);
		exploration = new DungeonExploration(ctx);
		expedition = Expedition.Start(floors, plan, seed, DungeonContext.GeneratorVersion, new Energy(ctx.Rules.MaxEnergy + ctx.ExtraEnergy), ctx.HasTradeItem,
			ctx.Free ? 1 : ctx.StartFloor, !ctx.Free && ctx.AtFastTravel, ctx.Shortcut, exploration.Get);
		hud = new DungeonHud(ctx, expedition);
		narration = new DungeonNarration(ctx, expedition);
		rewards = new DungeonRewards(ctx, () => InShortcut, hud.ShowCoins);
	}

	// O Atalho vale até o Desafio épico ser vencido; depois a criança explora a torre como quiser.
	bool InShortcut => ctx.Shortcut && !expedition.Finished;

	string OtherName => ctx.TowerNameOf(ctx.OtherTower);

	public TrainingArea Combat => combat;

	public Player Player => player;

	public Expedition Expedition => expedition;

	public MissionHistory History => history;

	public string CoinToast => hud.CoinToast;

	public ProgressServices Progress => ctx.Progress;

	public string ProfileId => ctx.ProfileId;

	public DungeonWorld World => world;

	public string RoomSpeech(int room) => narration.RoomSpeech(room);

	// A Main liga e desliga o Personagem quando uma tela (configurações, perfis) abre por cima.
	public void SetEnabled(bool value)
	{
		enabled = value;
		if (player is not null) player.Enabled = value && !left;
		hud.ShowPrompt(value && target is not null ? narration.PromptFor(target) : null);
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
			() => ctx.Level, () => player.Enabled && !left, _ => { }, Say, ctx.Random, ctx.Towers);
		combat.InDungeon = true;
		combat.Creature = CreatureWords.Wild;
		combat.EnergyTop = DungeonHud.EnergyTop;
		hud.InCombat = () => combat.HasTarget;
		combat.ChallengeSolved += OnSolved;
		AddChild(combat);

		AddChild(hud);
		hud.InteractPressed += Activate;
		hud.LeaveRequested += () => Leave("");
		hud.MapSpeechRequested += () => Say(narration.MapSpeech(IsFloorDone()));
		hud.RoomPointed += room => Say(narration.RoomSpeech(room));

		player.Enabled = enabled;
		if (ctx.Shortcut && !BeginShortcut()) return;
		// O Atalho não descobre Andares: quem erra joga o Nível normalmente, do Andar mais fundo que já conhecia.
		if (ctx.Shortcut) hud.BuildMap();
		else DiscoverFloor();
		EnterRoom(-1);
		Say(ctx.Free
			? $"Expedição livre na {ctx.TowerName}! Os Andares não têm fim: volte ao Lobby quando quiser."
			: $"Bem-vinda à {ctx.TowerName}! Ande até uma porta e escolha entrar.");
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
		exploration.Save(expedition);
		var kind = expedition.Current.Kind;
		var text = kind.SavesPoint ? FindFastTravel() : narration.RoomLine(kind);
		var spoken = string.Join(" ", new[] { lead, text, rewards.ForFloor(expedition) }.Where(t => t is not null));
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
		var marked = ctx.Progress.Levels.MarkShortcutTried(ctx.ProfileId, ctx.LevelKey);
		if (!marked.IsOk) GD.PushWarning(marked.Error!);
		shortcut = setups.Value!;
		return true;
	}

	void RefreshRoom() =>
		world.Show(expedition.Floor, expedition.CurrentRoom, expedition.Cleared, narration.Describe, narration.RoomNote(), expedition.CanDescend);

	// Combate, Puzzle e épico têm uma criatura a vencer. A criança a escolhe (clique ou F) para abrir a montagem.
	void StartFight()
	{
		var current = expedition.Current;
		var needsFight = current.Kind.HasChallenge && !expedition.IsCleared(expedition.CurrentRoom);
		var puzzle = current.Kind.IsDoor;
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

		combat.BeginFight(fight.Value!, hud.Energy, OnWon, OnLost);
		if (InShortcut)
		{
			Say($"Atalho de Nível! Desafio {shortcutIndex + 1} de {shortcut.Count}. Errar encerra a tentativa. Escolha a criatura (clique ou F).");
			return;
		}
		Say(current.Kind.Intro ?? "Uma criatura da névoa! Chegue perto dela e escolha-a (clique ou F) para atacar.");
	}

	internal void OnWon()
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
		exploration.Save(expedition);
		var coins = paying ? rewards.ForRoom(room, expedition.Floor.Number) : null;
		var mastery = rewards.TakeMasteryNote();
		var completed = rewards.ForFloor(expedition);
		RefreshHud();
		if (room.Kind.EndsExpedition)
		{
			ConcludeLevel(Join(coins, mastery, completed));
			return;
		}
		if (room.HoldsItem)
		{
			// O recado do Item já está na tela; o resto vem junto, sem apagá-lo.
			var extra = Join(coins, mastery, completed);
			if (extra is not null) Say($"{hud.Message} {extra}");
			return;
		}
		var won = room.Kind.WonLine ?? "Sala vencida! As portas estão livres.";
		Say(Join(won, coins, mastery, narration.ProgressSpeech(), completed)!);
	}

	// Chamado pela luta quando a criança resolve a Missão, antes de a Sala valer como vencida.
	internal void OnSolved(Mission mission) => rewards.RecordSolved(mission);

	// O Desafio épico vencido conclui o Nível nesta torre (também pelo Atalho, que ainda entrega o Item); a criança escolhe seguir.
	void ConcludeLevel(string? extra = null)
	{
		var marked = ctx.Progress.Levels.MarkEpicDone(ctx.ProfileId, ctx.LevelKey);
		if (!marked.IsOk) GD.PushWarning(marked.Error!);
		var item = ctx.Shortcut && !ctx.Progress.Levels.HasItem(ctx.ProfileId, ctx.LevelKey) && GiveItem();
		RefreshHud();
		var text = $"Nível {ctx.Level} concluído! Você venceu o Desafio épico da {ctx.TowerName}." + (extra is null ? "" : " " + extra);
		Say(text);
		var screen = new LevelDoneScreen(ctx.TowerName, ctx.Level, item, () => Leave(""));
		levelDone = screen;
		if (ctx.ShowOverlay is { } show) show(screen);
		else GetTree().CreateTimer(EndSeconds, true, false, true).Timeout += () => Leave("");
	}

	void OnShortcutFailed() =>
		Leave("O Atalho não deu certo desta vez, e cada Nível só deixa tentar uma vez. Entre pela porta para jogar o Nível normalmente.");

	bool GiveItem()
	{
		var saved = ctx.Progress.Levels.GiveItem(ctx.ProfileId, ctx.LevelKey);
		if (!saved.IsOk)
		{
			GD.PushWarning(saved.Error!);
			Say("Não foi possível guardar o Item agora. Vamos tentar de novo.");
			return false;
		}
		Say($"Você ganhou o Item de troca da {ctx.TowerName}! Leve-o à {ctx.TowerNameOf(ctx.Towers.Beneficiary(ctx.Tower)?.Id ?? ctx.OtherTower)} para abrir o Marco de lá.");
		return true;
	}

	internal void OnLost() => Leave("A névoa ficou forte demais desta vez. Vamos recarregar a energia?");

	// --- Portas e escada

	public override void _Process(double delta)
	{
		if (left) return;
		world.Creature.SetCaptionContext(true, enabled && combat.InTargetRange());
		combat.CheckRange();
		hud.RefreshEnergy();
		var near = enabled ? InteractionFinder.Nearest(player.GroundPosition, world.Interactables(expedition), ctx.Rules.Lobby.InteractionReach + ctx.ExtraReach) : null;
		if (near?.Id == target) return;
		target = near?.Id;
		hud.ShowPrompt(target is null ? null : narration.PromptFor(target));
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
				Say($"O Marco só abre com o Item de troca da {OtherName}.");
				break;
		}
	}

	// Chegar ao Fast travel salva o ponto: é por ele que a criança retoma pelo portal do Lobby.
	string FindFastTravel()
	{
		// Na Livre o ponto não é guardado: ele não pertence ao Plano do Nível.
		if (ctx.Free) return "Fast travel! Aqui você pode voltar ao Lobby quando quiser.";
		var saved = ctx.Progress.Levels.FindFastTravel(ctx.ProfileId, ctx.FloorKeyOf(expedition.Floor.Number));
		if (!saved.IsOk) GD.PushWarning(saved.Error!);
		return "Fast travel encontrado! O progresso foi salvo, e você pode voltar por aqui pelo portal do Lobby.";
	}

	void Rest()
	{
		var rested = expedition.UseRest(ctx.Rules.Dungeon.RestEnergy);
		hud.RefreshEnergy();
		if (rested.IsOk) RefreshRoom();
		Say(rested.IsOk ? $"Você descansou e recuperou {rested.Value} de Energia." : rested.Error!);
	}

	// Sobe ou desce a escada; o Andar de chegada volta como a criança o deixou.
	void ChangeFloor(Func<bool> move)
	{
		if (!move()) return;
		DiscoverFloor();
		EnterRoom(-1, $"Andar {expedition.Floor.Number}! {narration.ProgressSpeech()}");
	}

	bool IsFloorDone() => !ctx.Shortcut && (expedition.IsFloorComplete(expedition.Floor.Number) || exploration.IsDone(expedition.Floor.Number));

	void DiscoverFloor()
	{
		exploration.Discover(expedition.Floor.Number);
		hud.BuildMap();
	}

	// O objetivo da placa é o Conceito do Andar, lido das Missões e nomeado em dados; o jogo nunca o inventa.
	string Objective()
	{
		// Na Livre a luta pode ter procurado outra dificuldade; o objetivo mostra o da luta de verdade.
		var setup = fightSetup is { } fight && fight.Floor == expedition.Floor.Number ? Result<TrainingSetup>.Ok(fight.Setup)
			: DungeonCombat.Setup(missions, ctx.Discipline, ctx.Level, expedition.Floor.Number);
		var concept = setup.IsOk ? DungeonCombat.ConceptOf(missions, setup.Value!) : null;
		var subject = ctx.Tower.Subject;
		return concept is null ? subject : $"{subject}: {(ctx.Concepts ?? ConceptNames.Empty).NameOf(concept)}";
	}

	public void RefreshDock() => hud.RefreshDock();

	void RefreshHud() =>
		hud.Refresh($"{(ctx.Free ? "Livre · " : "")}{ctx.TowerName} · Nível {ctx.Level} · Andar {expedition.Floor.Number}{narration.OfTotal()}\n{Objective()}", IsFloorDone());

	void Say(string text) => hud.Say(text);

	// A criança volta ao Lobby por vontade própria, pelo fim da torre ou por falta de Energia; os Itens ficam com ela.
	public void Leave(string reason)
	{
		if (left) return;
		left = true;
		// Sair por outro botão com a escolha do Nível concluído aberta: a tela não pode ficar para trás.
		if (levelDone is not null && IsInstanceValid(levelDone)) levelDone.Close();
		combat.EndFight();
		Engine.TimeScale = 1.0;
		hud.Close();
		Left?.Invoke(reason);
		QueueFree();
	}
}
