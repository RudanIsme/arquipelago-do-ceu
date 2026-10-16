using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// A Área de treino: liga o Boneco, o Personagem, as telas de montagem e a voz à sessão de treino de core/.
// O treino tem Energia própria, o Boneco lança bolhas avisadas e faz travessuras nos erros; zerar a Energia só tira
// a criança da Área de treino. Nada aqui grava no save: o treino não conta para o progresso.
public partial class TrainingArea : Node
{
	const float DummyPickRadius = 1.3f;
	const float ReactionSeconds = 3.0f;

	readonly GameRules rules;
	readonly Lobby lobby;
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
	readonly BubbleAttack bubble;
	readonly IReadOnlyList<Mission> missions;
	CombatSession? session;
	OverlayScreen? panel;
	EnergyView? energyView;
	bool reacting;
	// Cada erro e cada fim de treino mudam a época; um temporizador de uma época antiga não faz mais nada.
	int epoch;
	int extraEnergy;
	public void SetEquipmentEnergy(int bonus)
	{
		extraEnergy = Math.Clamp(bonus, 0, 2);
		session?.Energy.SetMaximum(rules.MaxEnergy + extraEnergy);
		UpdateEnergy();
	}

	public TrainingArea(GameRules rules, Lobby lobby, Player player, TrainingDummy dummy, VoiceService voice, SettingsService settings, Control uiRoot,
		Func<int> unlockedLevel, Func<bool> canAct, Action<OverlayScreen> showOverlay, Action<string> notify)
	{
		this.rules = rules;
		this.lobby = lobby;
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
		bubble = new BubbleAttack(rules.Combat.Bubble);
		missions = MissionSource.Load().Missions;

		var (ground, top) = lobby.FootprintOf("boneco");
		dummy.Place(ground, top);
		dummy.Idle();
	}

	public bool HasTarget => panel is not null;
	public void RefreshPresentation() { energyView?.RefreshTheme(); UpdateEnergyDock(); }
	void UpdateEnergyDock() { if (energyView is not null) energyView.OffsetTop = UiKit.Compact ? (panel is null ? 154 : 12) : 126; }

	// Clicar na criatura a escolhe: o raio do clique passa perto do centro do Boneco.
	public bool HitsDummy(Godot.Vector2 screenPoint)
	{
		var origin = lobby.Camera.ProjectRayOrigin(screenPoint);
		var direction = lobby.Camera.ProjectRayNormal(screenPoint);
		var closest = origin + direction * (dummy.Center - origin).Dot(direction);
		return closest.DistanceTo(dummy.Center) <= DummyPickRadius;
	}

	public bool InTargetRange()
	{
		var (ground, _) = lobby.FootprintOf("boneco");
		return Vector2.Distance(player.GroundPosition, new Vector2(ground.X, ground.Z)) <= rules.Combat.TargetRange;
	}

	public void ToggleTarget()
	{
		if (panel is not null) DropTarget();
		else Select();
	}

	// Solta o alvo sem responder; o treino continua (com bolhas e Energia) até a criança sair ou a Energia zerar.
	public void DropTarget()
	{
		switch (panel)
		{
			case StaffPanel staff: staff.Abandon(); break;
			case NamePanel name: name.Abandon(); break;
			case WordPanel word: word.Abandon(); break;
		}
	}

	// Se a criança se afasta demais, o alvo se perde.
	public void CheckRange()
	{
		if (panel is null || InTargetRange()) return;
		notify("O Boneco ficou longe.");
		DropTarget();
	}

	// Trocar de Perfil ou fechar o jogo acaba o treino sem nenhuma mensagem.
	public void End()
	{
		DropTarget();
		if (session is null) return;
		Finish("");
	}

	void Select()
	{
		if (!canAct()) return;
		if (!InTargetRange())
		{
			notify("Chegue mais perto do Boneco para escolhê-lo.");
			return;
		}
		if (session is null) showOverlay(new TrainingSetupScreen(rules.Roles, TrainingSetup.Levels(unlockedLevel()), TryStart));
		else OpenPanel();
	}

	string? TryStart(TrainingSetup setup)
	{
		var started = CombatSession.Start(rules.Combat, rules.MaxEnergy + extraEnergy, unlockedLevel(), setup, missions, new Pcg32((ulong)Time.GetTicksUsec()));
		if (!started.IsOk) return started.Error;

		session = started.Value;
		bubble.Reset();
		energyView = new EnergyView();
		energyView.LeaveRequested += () => Finish("Treino encerrado. Quando quiser, é só escolher o Boneco de novo.");
		uiRoot.AddChild(energyView);
		UpdateEnergy();
		ShowChallenge();
		OpenPanel();
		return null;
	}

	// O Boneco mostra o Desafio atual: o escudo (Matemático) ou a criatura sem nome (Escriba).
	void ShowChallenge()
	{
		if (session!.Math is { } math)
		{
			dummy.ShowCreature(null);
			dummy.Reset(math.Shield.Remaining, "Escudo");
		}
		else if (session.Name is { } name)
		{
			dummy.ShowCreature(name.Image);
			dummy.Reset(name.Remaining, name.Kind switch { NameKind.Sentence => "Palavras que faltam", NameKind.Gap => "Lacunas que faltam", _ => "Sílabas que faltam" });
		}
		else
		{
			dummy.ShowCreature(session.Word!.Image);
			dummy.Reset(session.Word.Remaining, "Palavras que faltam");
		}
	}

	void OpenPanel()
	{
		if (session is null || panel is not null) return;
		OverlayScreen opened;
		Action<string> tell = _ => { };
		if (session.Math is { } math)
		{
			var staff = new StaffPanel(math, session.Rules, voice, InTargetRange);
			staff.Attacked += OnAttacked;
			staff.ComposingChanged += OnComposing;
			staff.HintRequested += () => { if (session is not null) tell($"Dica: {session.Hint()}"); };
			staff.ReadRequested += () => { if (session is not null) tell(session.Mission.Prompt); };
			tell = staff.Tell;
			opened = staff;
		}
		else if (session.Name is { } nameBattle)
		{
			var name = new NamePanel(nameBattle, rules.Combat.Tracing, voice);
			name.Checked += OnChecked;
			name.ComposingChanged += OnComposing;
			name.HintRequested += () => { if (session is not null) tell($"Dica: {session.Hint()}"); };
			name.ReadRequested += () => { if (session is not null) tell(session.Mission.Prompt); };
			tell = name.Tell;
			opened = name;
		}
		else
		{
			var word = new WordPanel(session.Word!, voice);
			word.Checked += OnWordChecked;
			word.ComposingChanged += OnComposing;
			word.HintRequested += () => { if (session is not null) tell($"Dica: {session.Hint()}"); };
			word.ReadRequested += () => { if (session is not null) tell(session.Mission.Prompt); };
			tell = word.Tell;
			opened = word;
		}

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
		tell(session.Mission.Prompt);
	}

	void OnComposing(bool composing)
	{
		if (composing) time.Begin(settings.Current.CalmMode);
		else time.End();
		Engine.TimeScale = time.Scale;
	}

	void Tell(string text)
	{
		switch (panel)
		{
			case StaffPanel staff: staff.Tell(text); break;
			case NamePanel name: name.Tell(text); break;
			case WordPanel word: word.Tell(text); break;
			default: voice.Say(text, interrupt: true); break;
		}
	}

	// --- Matemático

	void OnAttacked(AttackResult result)
	{
		if (session is null) return;
		var from = player.GlobalPosition + Vector3.Up * 1f;
		AttackAnimation();
		switch (result.Outcome)
		{
			case HitOutcome.Dissolved:
				dummy.Ray(from, perfect: true);
				dummy.Dissolved();
				session.Hit(perfect: true);
				UpdateEnergy();
				Say("Golpe perfeito!", $"Golpe perfeito! {result.Equation} dissolveu a névoa. Você recuperou Energia.");
				NextChallengeSoon();
				break;
			case HitOutcome.Reduced:
				dummy.Ray(from, perfect: false);
				dummy.Reduced(result.Remaining);
				session.Hit(perfect: false);
				UpdateEnergy();
				Say($"Acertou! Faltam {result.Remaining}.", $"Acertou! {result.Equation}. Faltam {result.Remaining}.");
				break;
			default:
				dummy.Overshoot();
				Mistake("Passou do valor!", $"Passou do valor: {result.Equation} é mais que {result.Remaining}.");
				break;
		}
	}

	// --- Escriba

	void OnChecked(NameVerdict verdict)
	{
		if (session?.Name is not { } name) return;
		var from = player.GlobalPosition + Vector3.Up * 1f;
		var strikes = verdict.NewStrikes;
		if (strikes > 0)
		{
			AttackAnimation();
			dummy.Ray(from, perfect: verdict.Freed);
			dummy.Reduced(name.Remaining);
		}
		var piece = name.Kind == NameKind.Syllables ? "sílaba" : "palavra";
		var struck = strikes switch { 0 => "", 1 => $"1 {piece} certa, 1 golpe! ", _ => $"{strikes} {piece}s certas, {strikes} golpes! " };

		if (verdict.Freed)
		{
			dummy.Dissolved();
			session.Hit(perfect: false);
			UpdateEnergy();
			var done = name.Kind switch
			{
				NameKind.Sentence => $"Muito bem! A frase ficou assim: {name.Completed}",
				NameKind.Gap => $"Muito bem! A frase completa é: {name.Completed}",
				_ => $"Muito bem! O nome é {name.Word.ToUpperInvariant()}. A criatura foi libertada.",
			};
			Say(name.Kind == NameKind.Syllables ? "Nome certo!" : "Frase certa!", $"{struck}{done}");
			NextChallengeSoon();
		}
		else if (verdict.Mistaken)
		{
			dummy.Overshoot();
			Mistake(name.Kind switch { NameKind.Sentence => "Fora de ordem!", NameKind.Gap => "Palavra errada!", _ => "Sílaba errada!" },
				$"{struck}{name.Kind switch { NameKind.Sentence => "Uma palavra está fora do lugar.", NameKind.Gap => "Essa palavra não completa a frase.", _ => "Uma sílaba está errada." }}");
		}
		else Tell(strikes > 0 ? $"{struck}Faltam {name.Remaining}." : $"Ainda faltam {piece}s. Escolha mais uma.");
	}

	// O Escriba do 2º ano: a palavra inteira, com a grafia exata. Acertar liberta a criatura; errar é erro.
	void OnWordChecked(WordResult result)
	{
		if (session?.Word is not { } word) return;
		var from = player.GlobalPosition + Vector3.Up * 1f;
		AttackAnimation();
		if (result == WordResult.Freed)
		{
			dummy.Ray(from, perfect: true);
			dummy.Dissolved();
			session.Hit(perfect: false);
			UpdateEnergy();
			Say("Nome certo!", $"Muito bem! Você escreveu {word.Word.ToUpperInvariant()}. A criatura foi libertada.");
			NextChallengeSoon();
			return;
		}
		dummy.Overshoot();
		Mistake("Grafia errada!", $"Você escreveu {word.Typed.ToUpperInvariant()}, mas não é assim que se escreve.");
	}

	// --- Erro, travessura, dica

	void Mistake(string headline, string lead)
	{
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
			? $"O Boneco fez uma travessura e escondeu uma peça por {rules.Combat.PrankSeconds:0} segundos."
			: "O Boneco fez uma travessura e embaralhou as peças.";
		ShowChallenge();
		RefreshPanel();

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
			RefreshPanel();
		};
	}

	void NextChallengeSoon()
	{
		reacting = true;
		bubble.Reset();
		dummy.HideWarning();
		dummy.HideBubble();
		var mine = epoch;
		GetTree().CreateTimer(ReactionSeconds, true, false, true).Timeout += () =>
		{
			if (epoch != mine || session is null) return;
			reacting = false;
			session.NextChallenge();
			ShowChallenge();
			RefreshPanel();
			Tell($"Novo Desafio: {session.Mission.Prompt}");
		};
	}

	void RefreshPanel()
	{
		switch (panel)
		{
			case StaffPanel staff: staff.Refresh(); break;
			case NamePanel name: name.Reload(); break;
			case WordPanel word: word.Reload(); break;
		}
	}

	// Mostra na cena e no painel; o painel também fala. Sem painel aberto, só o Boneco e a voz.
	void Say(string headline, string text, bool good = true)
	{
		dummy.Say(headline, good);
		if (panel is null) voice.Say(text, interrupt: true);
		else Tell(text);
	}

	void AttackAnimation()
	{
		var (ground, _) = lobby.FootprintOf("boneco");
		player.Attack(new Vector2(ground.X, ground.Z));
	}

	void UpdateEnergy()
	{
		if (session is not null) energyView?.Set(session.Energy.Current, session.Energy.Max);
	}

	// Energia zerada, "Sair do treino" ou troca de Perfil: a criança só sai da Área de treino, sem perder nada.
	void Finish(string message)
	{
		epoch++;
		DropTarget();
		session = null;
		reacting = false;
		bubble.Reset();
		energyView?.QueueFree();
		energyView = null;
		dummy.Idle();
		time.End();
		Engine.TimeScale = time.Scale;
		if (message == "") return;
		notify(message);
		dummy.Say("Fim do treino por hoje.", good: false);
	}

	// --- Bolhas

	public override void _Process(double delta)
	{
		if (session is null || reacting || !canAct()) return;
		if (settings.Current.CalmMode)
		{
			// Modo calmo: o Boneco não lança bolhas e só reage a erros.
			if (bubble.Phase != BubblePhase.Waiting)
			{
				bubble.Reset();
				dummy.HideWarning();
				dummy.HideBubble();
			}
			return;
		}

		var (ground, _) = lobby.FootprintOf("boneco");
		var origin = new Vector2(ground.X, ground.Z);
		var radius = rules.Combat.Bubble.Radius;
		switch (bubble.Update((float)delta, origin, player.GroundPosition))
		{
			case BubbleEvent.Warned:
				voice.Say("Cuidado! Bolha de névoa vindo. Saia do caminho.", interrupt: false);
				break;
			case BubbleEvent.Launched:
				dummy.HideWarning();
				break;
			case BubbleEvent.Hit:
				dummy.HideBubble();
				dummy.Hurt();
				session.BubbleHit();
				UpdateEnergy();
				if (session.IsOver) Finish("A névoa ficou forte demais desta vez. Vamos recarregar a energia?");
				return;
			case BubbleEvent.Missed:
				dummy.HideBubble();
				break;
		}

		if (bubble.Phase == BubblePhase.Warning)
		{
			var way = bubble.DirectionFrom(origin);
			dummy.ShowWarning(new Vector3(origin.X, 0f, origin.Y), new Vector3(way.X, 0f, way.Y), rules.Combat.Bubble.Range, radius * 2f, bubble.WarningProgress);
		}
		else if (bubble.Phase == BubblePhase.Flying) dummy.MoveBubble(new Vector3(bubble.Position.X, 0f, bubble.Position.Y), radius);
	}

	public override void _ExitTree() => Engine.TimeScale = 1.0;
}
