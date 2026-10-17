using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// A Área de treino e as lutas das Salas: liga criatura, Personagem, telas e voz à sessão de combate do core.
// Nada aqui grava no save: o treino não conta para o progresso.
public partial class TrainingArea : Node
{
	const float ReactionSeconds = 3.0f;

	readonly GameRules rules;
	readonly ICombatStage stage;
	readonly Player player;
	readonly TrainingDummy dummy;
	readonly VoiceService voice;
	readonly SettingsService settings;
	readonly Control uiRoot;
	readonly Func<int> unlockedLevel;
	readonly Func<bool> canAct;
	readonly Action<OverlayScreen> showOverlay;
	readonly Action<string> notify;
	readonly CombatTime time;
	readonly BubbleDirector bubbles;
	readonly AnswerReactions reactions;
	readonly IReadOnlyList<Mission> missions;
	readonly IRandomSource random;
	readonly TowerCatalog towers;
	CombatSession? session;
	OverlayScreen? panel;
	EnergyView? energyView;
	bool reacting;
	// Na Expedição a luta é de uma Sala: vencer o Desafio chama fightWon e zerar a Energia chama fightLost.
	// A Energia mostrada é a da Expedição, que a Dungeon desenha, e não a da Área de treino.
	Action? fightWon;
	Action? fightLost;
	bool ownsEnergyView = true;
	// Cada erro e cada fim de treino mudam a época; um temporizador de uma época antiga não faz mais nada.
	int epoch;
	int extraEnergy;
	public void SetEquipmentEnergy(int bonus)
	{
		extraEnergy = Math.Clamp(bonus, 0, 2);
		session?.Energy.SetMaximum(rules.MaxEnergy + extraEnergy);
		UpdateEnergy();
	}

	public TrainingArea(GameRules rules, ICombatStage stage, Player player, TrainingDummy dummy, VoiceService voice, SettingsService settings, Control uiRoot,
		Func<int> unlockedLevel, Func<bool> canAct, Action<OverlayScreen> showOverlay, Action<string> notify, IRandomSource random, TowerCatalog towers)
	{
		this.towers = towers;
		this.random = random;
		this.rules = rules;
		this.stage = stage;
		this.player = player;
		this.dummy = dummy;
		this.voice = voice;
		this.settings = settings;
		this.uiRoot = uiRoot;
		this.unlockedLevel = unlockedLevel;
		this.canAct = canAct;
		this.showOverlay = showOverlay;
		this.notify = notify;
		time = new CombatTime(rules.Combat);
		bubbles = new BubbleDirector(rules.Combat.Bubble, stage, dummy, player, voice);
		reactions = new AnswerReactions(this, dummy, player);
		missions = MissionSource.Load().Missions;

		var (ground, top) = stage.CreatureSpot;
		dummy.Place(ground, top);
		dummy.Idle();
	}

	// Dentro de uma Dungeon só se luta com a criatura da Sala; não há escolha de treino.
	public bool InDungeon { get; set; }

	// Sem bolhas, como no Puzzle: a porta não ataca, só espera a resposta.
	public bool Bubbles { get; set; } = true;

	// No Atalho de Nível o primeiro erro encerra a tentativa, sem tirar Energia.
	public Action? StopOnMistake { get; set; }

	// Como o jogo chama a criatura nas frases e nos painéis.
	public CreatureWords Creature { get; set; } = CreatureWords.Dummy;

	// Onde a barra de Energia fica; sem valor, o Lobby a posiciona abaixo da interface dele.
	public int? EnergyTop { get; set; }

	public bool HasTarget => panel is not null;
	public void RefreshPresentation() => UpdateEnergyDock();
	void UpdateEnergyDock() { if (energyView is not null) energyView.OffsetTop = EnergyTop ?? (panel is null ? 154 : 12); }

	// Clicar na criatura a escolhe: o raio do clique passa perto do centro do Boneco.
	public bool HitsDummy(Godot.Vector2 screenPoint) => dummy.IsPickedBy(stage.Camera, screenPoint);

	public bool InTargetRange()
	{
		var (ground, _) = stage.CreatureSpot;
		return Vector2.Distance(player.GroundPosition, new Vector2(ground.X, ground.Z)) <= rules.Combat.TargetRange;
	}

	public void ToggleTarget()
	{
		if (panel is not null) DropTarget();
		else Select();
	}

	// Solta o alvo sem responder; o treino continua (com bolhas e Energia) até a criança sair ou a Energia zerar.
	public void DropTarget() => AnswerPanels.Abandon(panel);

	// Se a criança se afasta demais, o alvo se perde.
	public void CheckRange()
	{
		if (panel is null || InTargetRange()) return;
		notify($"{Creature.Start} ficou longe.");
		DropTarget();
	}

	// Trocar de Perfil ou fechar o jogo acaba o treino sem nenhuma mensagem.
	public void End()
	{
		DropTarget();
		if (session is null) return;
		if (fightWon is not null)
		{
			// Uma luta de fora (o Desafio mestre) precisa saber que acabou, para não ficar presa em andamento.
			EndFight();
			FightAborted?.Invoke();
		}
		else Finish("");
	}

	public event Action? FightAborted;

	// A Missão que acabou de ser vencida numa luta de Sala, antes de a Sala valer como vencida. O treino não dispara isto.
	public event Action<Mission>? ChallengeSolved;

	internal CombatSession? Session => session;

	// A Missão em jogo.
	public Mission? CurrentMission => session?.Mission;

	// Começa a luta de uma Sala com a Energia da Expedição. A criança escolhe a criatura (clique ou F) para atacar.
	public void BeginFight(CombatSession fight, EnergyView energy, Action won, Action lost)
	{
		epoch++;
		session = fight;
		energyView = energy;
		ownsEnergyView = false;
		fightWon = won;
		fightLost = lost;
		reacting = false;
		bubbles.Reset();
		UpdateEnergy();
		dummy.ShowChallenge(session);
	}

	// Termina a luta sem mexer na Energia nem na tela dela; vale tanto para vitória quanto para sair da Sala.
	public void EndFight()
	{
		epoch++;
		DropTarget();
		session = null;
		reacting = false;
		fightWon = null;
		fightLost = null;
		energyView = null;
		ownsEnergyView = true;
		bubbles.Clear();
		time.End();
		Engine.TimeScale = time.Scale;
		if (!InDungeon) dummy.Idle();
	}

	void Select()
	{
		if (!canAct()) return;
		if (!InTargetRange())
		{
			notify($"Chegue mais perto {Creature.Of} para escolhê-{Creature.Pronoun}.");
			return;
		}
		if (session is null && InDungeon) return;
		if (session is null) showOverlay(new TrainingSetupScreen(towers.All.Select(t => (t.Id, t.Role)).ToList(), TrainingSetup.Levels(unlockedLevel(), towers.MaxLevel), TryStart));
		else OpenPanel();
	}

	// O treino é de um Papel; o equipamento dele é o que vale na luta.
	public event Action<string>? RoleChosen;

	// A Energia extra do equipamento do Papel escolhido, antes de ele valer para o resto do jogo.
	public Func<string, int>? EnergyBonusFor { get; set; }

	string? TryStart(TrainingSetup setup)
	{
		var bonus = Math.Clamp(EnergyBonusFor?.Invoke(setup.Discipline) ?? extraEnergy, 0, 2);
		var started = CombatSession.Start(rules.Combat, rules.MaxEnergy + bonus, unlockedLevel(), setup, missions, random.NewStream());
		if (!started.IsOk) return started.Error;

		RoleChosen?.Invoke(setup.Discipline);
		session = started.Value!;
		bubbles.Reset();
		energyView = new EnergyView();
		energyView.LeaveRequested += () => Finish("Treino encerrado. Quando quiser, é só escolher o Boneco de novo.");
		uiRoot.AddChild(energyView);
		UpdateEnergy();
		dummy.ShowChallenge(session);
		OpenPanel();
		return null;
	}

	void OpenPanel()
	{
		if (session is null || panel is not null) return;
		var opened = AnswerPanels.Create(session, rules, voice, InTargetRange, Creature, allowPull: !InDungeon, new PanelEvents(
			reactions.Attacked, reactions.NameChecked, reactions.WordChecked, OnComposing,
			Hint: () => { if (session is not null) Tell($"Dica: {session.Hint()}"); },
			Read: () => { if (session is not null) Tell(session.Mission.Prompt); }));

		panel = opened;
		UpdateEnergyDock();
		uiRoot.AddChild(opened);
		dummy.SetSelected(true);
		// Fecha também se a tela sair da cena por outro caminho: o tempo nunca pode ficar preso.
		void Release()
		{
			if (panel != opened) return;
			panel = null;
			UpdateEnergyDock();
			dummy.SetSelected(false);
			time.End();
			Engine.TimeScale = time.Scale;
		}
		opened.Closed += () =>
		{
			Release();
			opened.QueueFree();
		};
		opened.TreeExited += Release;
		Tell(session.Mission.Prompt);
	}

	void OnComposing(bool composing)
	{
		if (composing) time.Begin(settings.Current.CalmMode);
		else time.End();
		Engine.TimeScale = time.Scale;
	}

	internal void Tell(string text)
	{
		if (!AnswerPanels.Tell(panel, text)) voice.Say(text, interrupt: true);
	}

	// --- Erro, travessura, dica

	internal void Mistake(string headline, string lead)
	{
		if (StopOnMistake is { } stop) { EndFight(); stop(); return; }
		session!.RevealHidden();
		epoch++;
		var outcome = session.Mistake();
		UpdateEnergy();
		if (outcome.Empty)
		{
			Finish("A névoa ficou forte demais desta vez. Vamos recarregar a energia?");
			return;
		}

		dummy.Prank();
		var prank = outcome.Prank == Prank.Hidden
			? $"{Creature.Start} fez uma travessura e escondeu uma peça por {rules.Combat.PrankSeconds:0} segundos."
			: $"{Creature.Start} fez uma travessura e embaralhou as peças.";
		dummy.ShowChallenge(session);
		AnswerPanels.Refresh(panel);

		var text = $"{lead} {prank} Novo Desafio: {session.Mission.Prompt}";
		if (outcome.HintOffered) text += $" Dica: {session.Hint()}";
		Say($"{headline} Travessura!", text, good: false);

		if (outcome.Prank != Prank.Hidden) return;
		var mine = epoch;
		// A peça escondida volta sozinha; o tempo é real, mesmo com o jogo em câmera lenta.
		GetTree().CreateTimer(rules.Combat.PrankSeconds, true, false, true).Timeout += () =>
		{
			if (epoch != mine || session is null) return;
			session.RevealHidden();
			AnswerPanels.Refresh(panel);
		};
	}

	// Num treino vem logo o próximo Desafio; numa Sala, vencer o Desafio vence a Sala.
	internal void Won()
	{
		if (fightWon is null) { NextChallengeSoon(); return; }
		reacting = true;
		bubbles.Clear();
		var mine = ++epoch;
		GetTree().CreateTimer(ReactionSeconds, true, false, true).Timeout += () =>
		{
			if (epoch != mine || session is null) return;
			var won = fightWon;
			var solved = session.Mission;
			EndFight();
			ChallengeSolved?.Invoke(solved);
			won();
		};
	}

	void NextChallengeSoon()
	{
		reacting = true;
		bubbles.Clear();
		var mine = epoch;
		GetTree().CreateTimer(ReactionSeconds, true, false, true).Timeout += () =>
		{
			if (epoch != mine || session is null) return;
			reacting = false;
			session.NextChallenge();
			dummy.ShowChallenge(session);
			AnswerPanels.Refresh(panel);
			Tell($"Novo Desafio: {session.Mission.Prompt}");
		};
	}

	// Mostra na cena e no painel; o painel também fala. Sem painel aberto, só o Boneco e a voz.
	internal void Say(string headline, string text, bool good = true)
	{
		dummy.Say(headline, good);
		if (panel is null) voice.Say(text, interrupt: true);
		else Tell(text);
	}

	internal void AttackAnimation()
	{
		var (ground, _) = stage.CreatureSpot;
		player.Attack(new Vector2(ground.X, ground.Z));
	}

	internal void UpdateEnergy()
	{
		if (session is not null) energyView?.Set(session.Energy.Current, session.Energy.Max);
	}

	// Energia zerada, "Sair do treino" ou troca de Perfil: a criança só sai da Área de treino, sem perder nada.
	void Finish(string message)
	{
		if (fightLost is not null)
		{
			var lost = fightLost;
			EndFight();
			lost();
			return;
		}
		epoch++;
		DropTarget();
		session = null;
		reacting = false;
		bubbles.Reset();
		if (ownsEnergyView) energyView?.QueueFree();
		energyView = null;
		dummy.Idle();
		time.End();
		Engine.TimeScale = time.Scale;
		if (message.Length == 0) return;
		notify(message);
		dummy.Say("Fim do treino por hoje.", good: false);
	}

	// --- Bolhas

	public override void _Process(double delta)
	{
		if (session is null || reacting || !canAct()) return;
		// Modo calmo e Puzzle: o Boneco não lança bolhas e só reage a erros.
		if (!bubbles.Tick((float)delta, stopped: settings.Current.CalmMode || !Bubbles)) return;
		session.BubbleHit();
		UpdateEnergy();
		if (session.IsOver) Finish("A névoa ficou forte demais desta vez. Vamos recarregar a energia?");
	}

	public override void _ExitTree() => Engine.TimeScale = 1.0;
}
