using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// A HUD do combate, sem modal. Matemática e Português por sílaba se escrevem desenhando (CombatHud.FreeDraw); as outras Mecânicas
// escolhem a peça e a última ação golpeia, sem botão de atacar. Nada depende de velocidade ou precisão: o teclado faz tudo.
public partial class CombatHud : OverlayScreen
{
	const int PieceSize = 72;
	const int Margin = 12;

	readonly ICombatPresenter presenter;
	readonly VoiceService voice;
	readonly AttackRhythm rhythm;
	readonly Func<bool> inRange;
	readonly CreatureWords creature;
	readonly List<Button> pieceButtons = [];
	readonly Label heading = BookStyle.Text("", 20);
	readonly Label prompt = BookStyle.Text("", 24);
	readonly Label message = BookStyle.Text("", 18);
	readonly Label assembled = BookStyle.Text("", 22);
	readonly Label recharging = BookStyle.Text("", 18);
	HFlowContainer pieces = null!;
	Button undo = null!;
	bool composing;
	bool wasReady = true;

	public CombatHud(ICombatPresenter presenter, CombatRules rules, VoiceService voice, AttackRhythm rhythm, Func<bool> inRange, CreatureWords creature)
	{
		Name = "CombatHud";
		this.presenter = presenter;
		this.voice = voice;
		this.rhythm = rhythm;
		this.inRange = inRange;
		this.creature = creature;
		if (presenter.FreeDraw) BuildLayer(rules);
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
		if (layer is not null) AddChild(layer);
		AddChild(BuildBottom());
		SetMessage("");
		Refresh();
		// Sem foco de teclado: as setas e WASD continuam movendo o Personagem com a HUD aberta.
		foreach (var button in FindChildren("*", "Button", true, false)) ((Button)button).FocusMode = FocusModeEnum.None;
	}

	// Uma mensagem escrita que também é dita pela voz: toda instrução tem as duas versões.
	public void Tell(string text)
	{
		if (layer is null) SetMessage(text);
		voice.Say(text, interrupt: true);
	}

	// A linha de recado só ocupa espaço quando há o que dizer.
	void SetMessage(string text)
	{
		message.Text = text;
		message.Visible = text.Length > 0;
	}

	// Perde o alvo sem responder: as peças voltam para a mão e o desenho some.
	public void Abandon()
	{
		layer?.Clear();
		presenter.Cancel();
		Requested?.Invoke(null);
		Close();
	}

	// Embaixo, sobre a cena: o pedido e a bolsa (ou, no desenho livre, só os botões).
	Control BuildBottom()
	{
		if (presenter.FreeDraw) return BuildCompactBar();
		var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkEnd, MouseFilter = MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 8);
		column.AddChild(BuildRequest());
		column.AddChild(BuildTray());
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(column);
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
		actions.AddChild(BookStyle.Button("Dica", AskHint, "Hint", PieceSize));
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
		undo.Disabled = !presenter.Composing && layer?.IsActive != true;
		RefreshGuide();
		Requested?.Invoke(presenter.Request);
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
			button.Disabled = !piece.Enabled || !ready || presenter.Resolved;
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

	// A criança escolhe uma peça (toque, mouse ou tecla); ela entra direto na montagem.
	void Choose(int index)
	{
		if (!rhythm.PlayerReady) return;
		if (presenter.Resolved)
		{
			Tell(presenter.ResolvedText);
			return;
		}
		if (presenter.Pieces[index] is not { Enabled: true }) return;

		voice.Say(presenter.Announce(index), interrupt: true);
		presenter.Place(index);
		AfterPlace();
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
		if (!rhythm.PlayerReady || !presenter.CanConfirm) return;
		if (presenter.Resolved)
		{
			Tell(presenter.ResolvedText);
			return;
		}
		Strike();
	}

	void Undo()
	{
		if (layer?.IsActive == true)
		{
			layer.Clear();
			return;
		}
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
		var now = layer?.IsActive == true || presenter.Composing;
		if (now == composing) return;
		composing = now;
		ComposingChanged?.Invoke(now);
	}

	// No teclado, a tecla escolhe a peça (ou, no desenho livre, digita o que está pedido). Backspace apaga.
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (presenter.FreeDraw)
		{
			TypeKey(key);
			return;
		}
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

	// Esc apaga o desenho em andamento; sem desenho, solta o alvo: as peças voltam para a mão e nada conta como resposta.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		if (layer?.IsActive == true) layer.Clear();
		else Abandon();
	}
}
