using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// A montagem do cajado, no próprio cenário: uma calculadora na lateral, para o Boneco continuar à vista.
// A criança monta a conta tecla a tecla e faz a soma de cabeça: o painel nunca mostra o resultado.
// Com mouse ou toque, tocar num número abre o pontilhado para desenhá-lo; no teclado, digitar o número vale.
// Nada depende de velocidade. Só Confirmar conta como resposta.
public partial class StaffPanel : OverlayScreen
{
	const int KeySize = 96;
	const int PanelWidth = 4 * KeySize + 3 * 8;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");

	readonly TrainingBattle battle;
	readonly CombatRules rules;
	readonly VoiceService voice;
	readonly List<Button> numberKeys = [];
	readonly List<Button> operatorKeys = [];
	VBoxContainer keypad = null!;
	TracePad pad = null!;
	Label shieldLabel = null!;
	Label expressionLabel = null!;
	Label messageLabel = null!;
	Button undoButton = null!;
	Button confirmButton = null!;
	bool tracing;
	int tracingSlot;

	public StaffPanel(TrainingBattle battle, CombatRules rules, VoiceService voice)
	{
		this.battle = battle;
		this.rules = rules;
		this.voice = voice;
	}

	// Preenchido quando a criança confirma; fica nulo se ela cancelou.
	public AttackResult? Result { get; private set; }

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		Refresh();
		FocusFirstKey();
	}

	void Build()
	{
		var style = new StyleBoxFlat { BgColor = PanelFill, BorderColor = Gold };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(16);
		style.SetContentMarginAll(18);
		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		// Na lateral direita, para o Boneco e a ilha continuarem à vista durante a montagem.
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight, LayoutPresetMode.Minsize);
		panel.GrowHorizontal = GrowDirection.Begin;
		panel.GrowVertical = GrowDirection.Both;
		panel.OffsetRight = -16;
		panel.AddThemeStyleboxOverride("panel", style);
		AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		shieldLabel = UiKit.Heading("");
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
		actions.AddChild(UiKit.BigButton("Cancelar", Cancel, 196));
		confirmButton = UiKit.BigButton("Confirmar", Confirm, 196);
		actions.AddChild(confirmButton);
		keypad.AddChild(actions);

		var keys = UiKit.Hint("Teclado: digite os números e +, Backspace apaga, Enter confirma, Esc cancela.");
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
		FocusFirstKey();
	}

	void PlaceNumber(int slot)
	{
		if (!battle.Staff.Place(slot)) return;
		messageLabel.Text = "";
		Refresh();
		FocusFirstKey();
	}

	void PlaceOperator(string symbol)
	{
		if (!battle.Staff.PlaceOperator(symbol)) return;
		messageLabel.Text = "";
		Refresh();
		FocusFirstKey();
	}

	void Undo()
	{
		if (!battle.Staff.Undo()) return;
		messageLabel.Text = "";
		Refresh();
		FocusFirstKey();
	}

	void Cancel()
	{
		battle.Staff.Cancel();
		Result = null;
		Close();
	}

	void Confirm()
	{
		var attack = battle.Confirm();
		if (!attack.IsOk)
		{
			messageLabel.Text = attack.Error!;
			return;
		}
		Result = attack.Value;
		Close();
	}

	// A conta aparece como a criança a montou, sem o resultado: somar é com ela.
	void Refresh()
	{
		shieldLabel.Text = $"Escudo: {battle.Shield.Remaining}";
		var tokens = battle.Staff.Tokens;
		expressionLabel.Text = tokens.Count == 0
			? "Monte uma conta com as teclas."
			: string.Join(" ", tokens) + (battle.Staff.CanPlaceNumber ? " ?" : "");

		for (var slot = 0; slot < numberKeys.Count; slot++)
		{
			var piece = battle.Bag.Hand[slot];
			// O numeral e os pontos aparecem juntos: a quantidade nunca depende só do símbolo.
			numberKeys[slot].Text = piece is { } n ? $"{n}\n{Dots(n)}" : "";
			numberKeys[slot].Disabled = piece is null || !battle.Staff.CanPlaceNumber;
			// O slot vazio continua no lugar, esmaecido, para as outras teclas não pularem de posição.
			numberKeys[slot].Modulate = new Color(1f, 1f, 1f, piece is null ? 0.2f : 1f);
		}
		foreach (var key in operatorKeys) key.Disabled = !battle.Staff.CanPlaceOperator;
		undoButton.Disabled = tokens.Count == 0;
	}

	static string Dots(int n) => new string('•', Math.Min(n, 10));

	void FocusFirstKey()
	{
		if (tracing) return;
		foreach (var key in numberKeys)
			if (!key.Disabled)
			{
				key.GrabFocus();
				return;
			}
		foreach (var key in operatorKeys)
			if (!key.Disabled)
			{
				key.GrabFocus();
				return;
			}
		confirmButton.GrabFocus();
	}

	// No teclado, digitar o número vale como escrevê-lo; Enter confirma mesmo com um botão em foco.
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

	// Esc volta do desenho, ou cancela a montagem: as peças voltam para a mão e nada conta como resposta.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		if (tracing) EndTracing();
		else Cancel();
	}
}
