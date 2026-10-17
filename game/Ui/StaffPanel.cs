using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// A UI de combate do Matemático, na lateral: monta a conta tecla a tecla e a soma é de cabeça (o resultado nunca aparece).
// Não é modal: o Personagem anda e o tempo só desacelera. Nada depende de velocidade; só Confirmar conta como resposta.
public partial class StaffPanel : OverlayScreen
{
	static int KeySize => 64;
	static int PanelWidth => 5 * KeySize + 4 * 8;
	const double TensWaitSeconds = 0.7;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");

	readonly TrainingBattle battle;
	readonly CombatRules rules;
	readonly VoiceService voice;
	readonly Func<bool> inRange;
	readonly List<Button> numberKeys = [];
	readonly List<Button> operatorKeys = [];
	readonly List<DotsView> dotViews = [];
	VBoxContainer keypad = null!;
	TracePad pad = null!;
	Label shieldLabel = null!;
	Label expressionLabel = null!;
	Label messageLabel = null!;
	Button undoButton = null!;
	bool tracing;
	bool composing;
	int pendingDigit;
	int pendingVersion;
	int tracingSlot;

	// `inRange` diz se o Personagem está perto o bastante para a conta valer como ataque.
	readonly CreatureWords creature;

	public StaffPanel(TrainingBattle battle, CombatRules rules, VoiceService voice, Func<bool> inRange, CreatureWords? creature = null)
	{
		this.creature = creature ?? CreatureWords.Dummy;
		this.battle = battle;
		this.rules = rules;
		this.voice = voice;
		this.inRange = inRange;
	}

	// A conta foi confirmada e avaliada; o painel continua aberto para a criança seguir atacando.
	public event Action<AttackResult>? Attacked;

	// Verdadeiro enquanto a criança desenha ou tem peças no cajado: é quando o tempo desacelera.
	public event Action<bool>? ComposingChanged;

	public event Action? HintRequested;

	public event Action? ReadRequested;

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		// Sem foco de teclado: as setas e WASD continuam movendo o Personagem com o painel aberto.
		foreach (var button in FindChildren("*", "Button", true, false)) ((Button)button).FocusMode = FocusModeEnum.None;
		Refresh();
	}

	// Uma mensagem escrita que também é dita pela voz: toda instrução tem as duas versões.
	public void Tell(string text)
	{
		messageLabel.Text = text;
		voice.Say(text, interrupt: true);
	}

	// Perde o alvo sem responder: as peças voltam para a mão.
	public void Abandon()
	{
		battle.Staff.Cancel();
		Close();
	}

	void Build()
	{
		var style = new StyleBoxFlat { BgColor = PanelFill, BorderColor = Gold };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(16);
		style.SetContentMarginAll(18);
		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		// Na lateral direita, para o Boneco e a ilha continuarem à vista; encolhe se não couber.
		panel.AddThemeStyleboxOverride("panel", UiKit.Surface());
		AddChild(panel);
		UiKit.DockFit(this, panel, right: true);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		shieldLabel = UiKit.Heading("");
		shieldLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		shieldLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		column.AddChild(shieldLabel);
		expressionLabel = UiKit.Caption("");
		expressionLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(expressionLabel);

		keypad = new VBoxContainer();
		keypad.AddThemeConstantOverride("separation", 10);
		column.AddChild(keypad);
		BuildKeypad();

		pad = new TracePad(rules.Tracing) { Visible = false };
		pad.Completed += OnTraced;
		pad.Cancelled += EndTracing;
		column.AddChild(pad);
	}

	void BuildKeypad()
	{
		var numbers = KeyFlow();
		for (var slot = 0; slot < battle.Bag.Hand.Count; slot++)
		{
			var index = slot;
			var key = UiKit.BigButton("", () => ChooseNumber(index), KeySize);
			key.AddThemeFontSizeOverride("font_size", 24);
			var dots = new DotsView();
			dots.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
			dots.OffsetTop = -38;
			dots.OffsetBottom = -6;
			key.AddChild(dots);
			dotViews.Add(dots);
			key.CustomMinimumSize = new Vector2(KeySize, KeySize);
			numbers.AddChild(key);
			numberKeys.Add(key);
		}
		keypad.AddChild(numbers);

		var signs = KeyFlow();
		foreach (var symbol in rules.Operators)
		{
			var sign = symbol;
			var key = UiKit.BigButton(sign, () => PlaceOperator(sign), KeySize);
			key.CustomMinimumSize = new Vector2(KeySize, KeySize);
			key.AddThemeFontSizeOverride("font_size", 30);
			signs.AddChild(key);
			operatorKeys.Add(key);
		}
		undoButton = UiKit.BigButton("Apagar", Undo, PanelWidth - (KeySize + 8) * rules.Operators.Count);
		signs.AddChild(undoButton);
		keypad.AddChild(signs);

		messageLabel = UiKit.Hint("");
		messageLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		keypad.AddChild(messageLabel);

		var help = KeyFlow();
		help.AddChild(UiKit.BigButton("Ouvir", () => ReadRequested?.Invoke(), 196));
		help.AddChild(UiKit.BigButton("Dica", () => HintRequested?.Invoke(), 196));
		keypad.AddChild(help);

		var actions = KeyFlow();
		actions.AddChild(UiKit.BigButton("Soltar alvo", Abandon, 196));
		actions.AddChild(UiKit.BigButton("Atacar", Confirm, 196));
		keypad.AddChild(actions);

		var keys = UiKit.Hint("Teclado: digite os números (10 e 20: o algarismo e o 0), + e -, Backspace apaga, Enter ataca, Esc solta o alvo.");
		keys.CustomMinimumSize = new Vector2(PanelWidth, 0);
		keypad.AddChild(keys);
	}

	static HFlowContainer KeyFlow()
	{
		var flow = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(PanelWidth, 0) };
		flow.AddThemeConstantOverride("h_separation", 8);
		flow.AddThemeConstantOverride("v_separation", 8);
		return flow;
	}

	// Com o mouse ou o dedo, o número só entra na conta depois de desenhado.
	async void ChooseNumber(int slot)
	{
		if (tracing || !battle.Staff.CanPlaceNumber || battle.Bag.Hand[slot] is not { } number) return;
		tracing = true;
		tracingSlot = slot;
		keypad.Visible = false;
		shieldLabel.Visible = expressionLabel.Visible = false;
		pad.Visible = true;
		NotifyComposing();
		voice.Say($"Desenhe o número {number}.", interrupt: true);
		await pad.Begin(number.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Desenhe o {number}");
	}

	void OnTraced()
	{
		var slot = tracingSlot;
		EndTracing();
		PlaceNumber(slot);
	}

	void EndTracing()
	{
		pad.Abort();
		tracing = false;
		pad.Visible = false;
		keypad.Visible = true;
		shieldLabel.Visible = expressionLabel.Visible = true;
		Refresh();
	}

	void PlaceNumber(int slot)
	{
		if (!battle.Staff.Place(slot)) return;
		messageLabel.Text = "";
		Refresh();
	}

	void PlaceOperator(string symbol)
	{
		if (!battle.Staff.PlaceOperator(symbol)) return;
		messageLabel.Text = "";
		Refresh();
	}

	void Undo()
	{
		if (!battle.Staff.Undo()) return;
		messageLabel.Text = "";
		Refresh();
	}

	void Confirm()
	{
		if (battle.Shield.IsDissolved)
		{
			Tell("A névoa está sumindo. Espere o novo escudo.");
			return;
		}
		if (!inRange())
		{
			Tell($"Chegue mais perto {creature.Of} para atacar.");
			return;
		}
		var attack = battle.Confirm();
		if (!attack.IsOk)
		{
			Tell(attack.Error!);
			return;
		}
		messageLabel.Text = "";
		Refresh();
		Attacked?.Invoke(attack.Value!);
	}

	// A conta aparece como a criança a montou, sem o resultado: somar é com ela.
	public void Refresh()
	{
		shieldLabel.Text = $"{creature.Title}\nEscudo: {battle.Shield.Remaining}";
		var tokens = battle.Staff.Tokens;
		expressionLabel.Text = tokens.Count == 0
			? "Monte uma conta com as teclas."
			: string.Join(" ", tokens) + (battle.Staff.CanPlaceNumber ? " ?" : "");

		for (var slot = 0; slot < numberKeys.Count; slot++)
		{
			var piece = battle.Bag.Hand[slot];
			// O numeral e os pontos aparecem juntos: a quantidade nunca depende só do símbolo.
			numberKeys[slot].Text = piece is { } n ? $"{n}\n " : battle.Bag.IsHidden(slot) ? "?" : "";
			dotViews[slot].Count = piece ?? 0;
			numberKeys[slot].Disabled = piece is null || !battle.Staff.CanPlaceNumber;
			// O slot vazio continua no lugar, esmaecido, para as outras teclas não pularem de posição.
			numberKeys[slot].Modulate = new Color(1f, 1f, 1f, piece is null ? (battle.Bag.IsHidden(slot) ? 0.5f : 0.2f) : 1f);
		}
		foreach (var key in operatorKeys) key.Disabled = !battle.Staff.CanPlaceOperator;
		undoButton.Disabled = tokens.Count == 0;
		NotifyComposing();
	}

	void NotifyComposing()
	{
		var now = tracing || battle.Staff.Tokens.Count > 0;
		if (now == composing) return;
		composing = now;
		ComposingChanged?.Invoke(now);
	}


	// No teclado, digitar o número vale como escrevê-lo; Enter ataca.
	public override void _Input(InputEvent @event)
	{
		if (tracing || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
		var digit = (int)key.Unicode - '0';
		var isDigit = digit is >= 0 and <= 9 && key.Unicode != 0;
		// Qualquer outra tecla encerra a espera pelo segundo algarismo e solta o número já digitado.
		if (!isDigit) FlushPendingDigit();
		switch (key.Keycode)
		{
			case Key.Enter or Key.KpEnter:
				GetViewport().SetInputAsHandled();
				Confirm();
				return;
			case Key.Backspace:
				GetViewport().SetInputAsHandled();
				Undo();
				return;
			case Key.Plus or Key.KpAdd:
				GetViewport().SetInputAsHandled();
				PlaceOperator("+");
				return;
			case Key.Minus or Key.KpSubtract:
				GetViewport().SetInputAsHandled();
				PlaceOperator("-");
				return;
		}
		if (isDigit && TypeDigit(digit)) GetViewport().SetInputAsHandled();
	}

	// Peças de 1 a 9 se escolhem pelo algarismo. As dezenas (10, 20...) se digitam com os dois algarismos: o 1 e depois o 0.
	// Se a mão tem o 1 e o 10, o jogo espera um instante pelo 0 antes de escolher o 1.
	bool TypeDigit(int digit)
	{
		if (pendingDigit > 0)
		{
			var first = pendingDigit;
			pendingDigit = 0;
			if (digit == 0 && SlotWith(first * 10) is var tens and >= 0)
			{
				PlaceNumber(tens);
				return true;
			}
			if (SlotWith(first) is var single and >= 0) PlaceNumber(single);
		}
		if (digit == 0) return false;
		if (SlotWith(digit * 10) >= 0)
		{
			pendingDigit = digit;
			var mine = ++pendingVersion;
			GetTree().CreateTimer(TensWaitSeconds, true, false, true).Timeout += () =>
			{
				if (IsInsideTree() && mine == pendingVersion) FlushPendingDigit();
			};
			return true;
		}
		if (SlotWith(digit) < 0) return false;
		PlaceNumber(SlotWith(digit));
		return true;
	}

	void FlushPendingDigit()
	{
		if (pendingDigit == 0) return;
		var first = pendingDigit;
		pendingDigit = 0;
		pendingVersion++;
		if (SlotWith(first) is var slot and >= 0) PlaceNumber(slot);
	}

	int SlotWith(int value)
	{
		for (var slot = 0; slot < battle.Bag.Hand.Count; slot++)
			if (battle.Bag.Hand[slot] == value) return slot;
		return -1;
	}

	// Esc volta do desenho, ou solta o alvo: as peças voltam para a mão e nada conta como resposta.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		if (tracing) EndTracing();
		else Abandon();
	}
}
