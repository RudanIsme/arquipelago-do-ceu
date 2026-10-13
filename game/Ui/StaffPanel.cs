using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// A UI de combate: aparece quando há uma criatura escolhida e fica na lateral, para o cenário continuar à vista.
// A criança monta a conta tecla a tecla e faz a soma de cabeça: o painel nunca mostra o resultado.
// Com mouse ou toque, tocar num número abre o pontilhado para desenhá-lo; no teclado, digitar o número vale.
// Não é modal: o Personagem continua andando enquanto o painel está aberto, e só fica lento (ou parado, no Modo
// calmo) enquanto a criança monta a resposta. Nada depende de velocidade. Só Confirmar conta como resposta.
public partial class StaffPanel : OverlayScreen
{
	const int KeySize = 96;
	const int PanelWidth = 4 * KeySize + 3 * 8;

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
	int tracingSlot;

	// `inRange` diz se o Personagem está perto o bastante para a conta valer como ataque.
	public StaffPanel(TrainingBattle battle, CombatRules rules, VoiceService voice, Func<bool> inRange)
	{
		this.battle = battle;
		this.rules = rules;
		this.voice = voice;
		this.inRange = inRange;
	}

	// A conta foi confirmada e avaliada; o painel continua aberto para a criança seguir atacando.
	public event Action<AttackResult>? Attacked;

	// Verdadeiro enquanto a criança desenha ou tem peças no cajado: é quando o tempo desacelera.
	public event Action<bool>? ComposingChanged;

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		// Sem foco de teclado: as setas e WASD continuam movendo o Personagem com o painel aberto.
		foreach (var button in FindChildren("*", "Button", true, false)) ((Button)button).FocusMode = FocusModeEnum.None;
		Refresh();
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
		panel.AddThemeStyleboxOverride("panel", style);
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
			key.AddThemeFontSizeOverride("font_size", 44);
			signs.AddChild(key);
			operatorKeys.Add(key);
		}
		undoButton = UiKit.BigButton("Apagar", Undo, PanelWidth - (KeySize + 8) * rules.Operators.Count);
		signs.AddChild(undoButton);
		keypad.AddChild(signs);

		messageLabel = UiKit.Hint("");
		messageLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		keypad.AddChild(messageLabel);

		var actions = KeyFlow();
		actions.AddChild(UiKit.BigButton("Soltar alvo", Abandon, 196));
		actions.AddChild(UiKit.BigButton("Atacar", Confirm, 196));
		keypad.AddChild(actions);

		var keys = UiKit.Hint("Teclado: digite os números e +, Backspace apaga, Enter ataca, Esc solta o alvo.");
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
		await pad.Begin(number.ToString(), $"Desenhe o {number}");
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
			messageLabel.Text = "A névoa está sumindo. Espere o novo escudo.";
			return;
		}
		if (!inRange())
		{
			messageLabel.Text = "Chegue mais perto do Boneco para atacar.";
			return;
		}
		var attack = battle.Confirm();
		if (!attack.IsOk)
		{
			messageLabel.Text = attack.Error!;
			return;
		}
		messageLabel.Text = "";
		Refresh();
		Attacked?.Invoke(attack.Value!);
	}

	// A conta aparece como a criança a montou, sem o resultado: somar é com ela.
	public void Refresh()
	{
		shieldLabel.Text = $"Boneco de treino\nEscudo: {battle.Shield.Remaining}";
		var tokens = battle.Staff.Tokens;
		expressionLabel.Text = tokens.Count == 0
			? "Monte uma conta com as teclas."
			: string.Join(" ", tokens) + (battle.Staff.CanPlaceNumber ? " ?" : "");

		for (var slot = 0; slot < numberKeys.Count; slot++)
		{
			var piece = battle.Bag.Hand[slot];
			// O numeral e os pontos aparecem juntos: a quantidade nunca depende só do símbolo.
			numberKeys[slot].Text = piece is { } n ? $"{n}\n " : "";
			dotViews[slot].Count = piece ?? 0;
			numberKeys[slot].Disabled = piece is null || !battle.Staff.CanPlaceNumber;
			// O slot vazio continua no lugar, esmaecido, para as outras teclas não pularem de posição.
			numberKeys[slot].Modulate = new Color(1f, 1f, 1f, piece is null ? 0.2f : 1f);
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
		}

		var digit = (int)key.Unicode - '0';
		if (digit is < 1 or > 9) return;
		var slot = SlotWith(digit);
		if (slot < 0) return;
		GetViewport().SetInputAsHandled();
		PlaceNumber(slot);
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

// As bolinhas de uma tecla de número, desenhadas em vez de escritas: algumas fontes (a para dislexia) não têm o "•".
partial class DotsView : Control
{
	const float Radius = 5f;
	const int PerRow = 5;

	int count;

	public DotsView() => MouseFilter = MouseFilterEnum.Ignore;

	public int Count
	{
		get => count;
		set
		{
			count = value;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		var rows = Mathf.CeilToInt(count / (float)PerRow);
		for (var i = 0; i < count; i++)
		{
			var row = i / PerRow;
			var inRow = Mathf.Min(PerRow, count - row * PerRow);
			var x = Size.X / 2f + (i % PerRow - (inRow - 1) / 2f) * (Radius * 2.6f);
			var y = Size.Y / 2f + (row - (rows - 1) / 2f) * (Radius * 2.6f);
			DrawCircle(new Vector2(x, y), Radius, Colors.White);
		}
	}
}
