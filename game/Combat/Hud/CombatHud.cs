using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// A HUD do combate, sem modal: o pedido e a bolsa ficam embaixo, sobre a cena. A peça escolhida sobe até a área de traço, a criança
// a traça e a última ação confere e golpeia, sem botão de atacar. Nada depende de velocidade ou precisão: o teclado faz tudo.
public partial class CombatHud : OverlayScreen
{
	const int PieceSize = 72;
	const int Margin = 12;

	readonly ICombatPresenter presenter;
	readonly VoiceService voice;
	readonly AttackRhythm rhythm;
	readonly Func<bool> inRange;
	readonly Func<bool> reduceMotion;
	readonly CreatureWords creature;
	readonly TraceZone zone;
	readonly List<Button> pieceButtons = [];
	readonly Label heading = BookStyle.Text("", 20);
	readonly Label prompt = BookStyle.Text("", 24);
	readonly Label message = BookStyle.Text("", 18);
	readonly Label assembled = BookStyle.Text("", 22);
	readonly Label recharging = BookStyle.Text("", 18);
	HFlowContainer pieces = null!;
	Button undo = null!;
	bool tracing;
	bool composing;
	bool wasReady = true;
	int tracingPiece;

	public CombatHud(ICombatPresenter presenter, TracingRules tracingRules, VoiceService voice, AttackRhythm rhythm, Func<bool> inRange, CreatureWords creature, Func<bool> reduceMotion)
	{
		Name = "CombatHud";
		this.presenter = presenter;
		this.voice = voice;
		this.rhythm = rhythm;
		this.inRange = inRange;
		this.creature = creature;
		this.reduceMotion = reduceMotion;
		zone = new TraceZone(tracingRules);
		zone.Completed += OnTraced;
		zone.Cancelled += EndTracing;
	}

	public ICombatPresenter Presenter => presenter;

	// Verdadeiro enquanto a criança traça ou tem peças montadas: é quando o tempo desacelera.
	public event Action<bool>? ComposingChanged;

	public event Action? HintRequested;

	public event Action? ReadRequested;

	public override void _EnterTree()
	{
		if (presenter.TypesLetters) Controls.TypingPanels++;
	}

	public override void _ExitTree()
	{
		if (presenter.TypesLetters) Controls.TypingPanels--;
		base._ExitTree();
	}

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		AddChild(BuildBottom());
		SetMessage("");
		Refresh();
		// Sem foco de teclado: as setas e WASD continuam movendo o Personagem com a HUD aberta.
		foreach (var button in FindChildren("*", "Button", true, false)) ((Button)button).FocusMode = FocusModeEnum.None;
	}

	// Uma mensagem escrita que também é dita pela voz: toda instrução tem as duas versões.
	public void Tell(string text)
	{
		SetMessage(text);
		voice.Say(text, interrupt: true);
	}

	// A linha de recado só ocupa espaço quando há o que dizer.
	void SetMessage(string text)
	{
		message.Text = text;
		message.Visible = text.Length > 0;
	}

	// Perde o alvo sem responder: as peças voltam para a mão.
	public void Abandon()
	{
		zone.End();
		presenter.Cancel();
		tracing = false;
		Close();
	}

	// Embaixo, sobre a cena: o pedido e a bolsa à esquerda e, quando a criança traça, a área de traço ao lado.
	Control BuildBottom()
	{
		var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkEnd, MouseFilter = MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 8);
		column.AddChild(BuildRequest());
		column.AddChild(BuildTray());
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(column);
		row.AddChild(zone);
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.Minsize);
		row.OffsetLeft = Margin;
		row.OffsetRight = -Margin;
		row.OffsetBottom = -Margin;
		row.GrowVertical = GrowDirection.Begin;
		return row;
	}

	Control BuildRequest()
	{
		var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		texts.AddChild(heading);
		texts.AddChild(prompt);
		texts.AddChild(message);
		texts.AddChild(BookStyle.Text(presenter.KeyHelp, 14));
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(texts);
		row.AddChild(BookStyle.Button("Ouvir", () => ReadRequested?.Invoke(), "Read", PieceSize));
		var panel = BookStyle.Panel(row);
		panel.Name = "Request";
		return panel;
	}

	Control BuildTray()
	{
		pieces = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = FlowContainer.AlignmentMode.Center, Name = "Pieces" };
		pieces.AddThemeConstantOverride("h_separation", 8);
		pieces.AddThemeConstantOverride("v_separation", 8);
		var side = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
		side.AddChild(assembled);
		side.AddChild(recharging);
		side.AddChild(BuildActions());
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		row.AddChild(pieces);
		row.AddChild(side);
		var panel = BookStyle.Panel(row);
		panel.Name = "Tray";
		return panel;
	}

	HFlowContainer BuildActions()
	{
		var actions = new HFlowContainer();
		actions.AddThemeConstantOverride("h_separation", 8);
		actions.AddThemeConstantOverride("v_separation", 8);
		undo = BookStyle.Button("Apagar", Undo, "Undo", PieceSize);
		actions.AddChild(undo);
		actions.AddChild(BookStyle.Button("Dica", () => HintRequested?.Invoke(), "Hint", PieceSize));
		if (presenter.ExtraLabel is { } extra) actions.AddChild(BookStyle.Button(extra, Extra, "Extra", PieceSize));
		if (presenter.ConfirmLabel is { } confirm) actions.AddChild(BookStyle.Button(confirm, Confirm, "Confirm", PieceSize));
		actions.AddChild(BookStyle.Button("Soltar alvo", Abandon, "Release", PieceSize));
		return actions;
	}

	// Mostra o Desafio de agora: o pedido, o que já foi montado e as peças (podem ter mudado: novo Desafio, travessura).
	public void Refresh()
	{
		heading.Text = presenter.Heading;
		prompt.Text = presenter.Prompt;
		assembled.Text = presenter.Assembled;
		RefreshPieces();
		undo.Disabled = !presenter.Composing || tracing;
		NotifyComposing();
	}

	void RefreshPieces()
	{
		foreach (var button in pieceButtons)
		{
			pieces.RemoveChild(button);
			button.QueueFree();
		}
		pieceButtons.Clear();
		var ready = rhythm.PlayerReady;
		recharging.Text = ready ? "" : "Recarregando…";
		var all = presenter.Pieces;
		for (var i = 0; i < all.Count; i++)
		{
			var index = i;
			var piece = all[i];
			var button = BookStyle.Button(piece.Dots > 0 ? $"{piece.Label}\n " : piece.Label, () => Choose(index), $"Piece_{i}", PieceSize);
			button.CustomMinimumSize = new Vector2(PieceSize, PieceSize);
			button.AddThemeFontSizeOverride("font_size", 26);
			button.Disabled = !piece.Enabled || !ready || tracing || presenter.Resolved;
			button.TooltipText = piece.Name;
			button.FocusMode = FocusModeEnum.None;
			// A peça escondida fica no lugar, esmaecida e com "?", para as outras não pularem de posição.
			if (piece.Label is "?" or "") button.Modulate = new Color(1f, 1f, 1f, 0.45f);
			if (piece.Highlighted) button.AddThemeStyleboxOverride("normal", BookStyle.Emphasized(BookStyle.Selected));
			AddDots(button, piece.Dots);
			pieces.AddChild(button);
			pieceButtons.Add(button);
		}
	}

	// O numeral e os pontos aparecem juntos: a quantidade nunca depende só do símbolo.
	static void AddDots(Button button, int count)
	{
		if (count <= 0) return;
		var dots = new DotsView { Count = count };
		dots.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		dots.OffsetTop = -30;
		dots.OffsetBottom = -4;
		button.AddChild(dots);
	}

	public override void _Process(double delta)
	{
		var ready = rhythm.PlayerReady;
		if (ready == wasReady) return;
		wasReady = ready;
		Refresh();
	}

	// A criança escolhe uma peça (toque, mouse ou tecla); ela sobe animada e é traçada. Frases entram direto, sem traço.
	void Choose(int index)
	{
		if (tracing) return;
		if (!rhythm.PlayerReady) return;
		if (presenter.Resolved)
		{
			Tell(presenter.ResolvedText);
			return;
		}
		if (presenter.Pieces[index] is not { Enabled: true }) return;

		voice.Say(presenter.Announce(index), interrupt: true);
		if (presenter.TraceText(index) is not { } text)
		{
			presenter.Place(index);
			AfterPlace();
			return;
		}
		tracing = true;
		tracingPiece = index;
		SetMessage("");
		var from = pieceButtons[index].GlobalPosition;
		zone.Begin(text, presenter.TraceLabel(index), from, animate: !reduceMotion());
		Refresh();
	}

	void OnTraced()
	{
		var piece = tracingPiece;
		EndTracing();
		presenter.Place(piece);
		AfterPlace();
	}

	void EndTracing()
	{
		zone.End();
		tracing = false;
		Refresh();
	}

	void AfterPlace()
	{
		SetMessage("");
		Refresh();
		if (presenter.Complete) Strike();
	}

	// A última ação confere e o golpe sai; sem botão de atacar.
	void Strike()
	{
		if (!inRange())
		{
			Tell($"Chegue mais perto {creature.Of} para atacar.");
			presenter.Cancel();
			Refresh();
			return;
		}
		rhythm.PlayerAnswered();
		if (!presenter.Attack(out var error))
		{
			if (error is not null) Tell(error);
			presenter.Cancel();
		}
		Refresh();
	}

	void Confirm()
	{
		if (tracing || !rhythm.PlayerReady || !presenter.CanConfirm) return;
		if (presenter.Resolved)
		{
			Tell(presenter.ResolvedText);
			return;
		}
		Strike();
	}

	void Undo()
	{
		if (!presenter.Undo()) return;
		SetMessage("");
		Refresh();
	}

	void Extra()
	{
		if (presenter.Extra() is not { } text) return;
		Refresh();
		Tell(text);
	}

	void NotifyComposing()
	{
		var now = tracing || presenter.Composing;
		if (now == composing) return;
		composing = now;
		ComposingChanged?.Invoke(now);
	}

	// No teclado, a tecla escolhe a peça; o traço se faz digitando o que está pedido. Backspace apaga.
	public override void _Input(InputEvent @event)
	{
		if (tracing || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.PhysicalKeycode is Key.Enter or Key.KpEnter && presenter.ConfirmLabel is not null)
		{
			GetViewport().SetInputAsHandled();
			Confirm();
			return;
		}
		if (key.PhysicalKeycode == Key.Backspace)
		{
			GetViewport().SetInputAsHandled();
			Undo();
			return;
		}
		if (presenter.PieceForKey(key) is not { } piece) return;
		GetViewport().SetInputAsHandled();
		Choose(piece);
	}

	// Esc volta do traço; sem traço aberto, solta o alvo: as peças voltam para a mão e nada conta como resposta.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		if (tracing) EndTracing();
		else Abandon();
	}
}
