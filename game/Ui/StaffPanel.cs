using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// A montagem do cajado, no próprio cenário: o painel fica na parte de baixo e o Boneco continua à vista.
// Nada aqui depende de velocidade. A conta aparece antes de confirmar e só Confirmar conta como resposta.
// Mouse e toque usam os botões; no teclado, 1 a 5 escolhem a peça, Backspace desfaz, Enter confirma e Esc cancela.
public partial class StaffPanel : OverlayScreen
{
	const int PieceSize = 96;
	const int PanelWidth = 4 * PieceSize + 3 * 8;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");

	readonly TrainingBattle battle;
	readonly List<Button> pieceButtons = [];
	Label shieldLabel = null!;
	Label expressionLabel = null!;
	Label messageLabel = null!;
	Button undoButton = null!;
	Button confirmButton = null!;

	public StaffPanel(TrainingBattle battle) => this.battle = battle;

	// Preenchido quando a criança confirma; fica nulo se ela cancelou.
	public AttackResult? Result { get; private set; }

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		Refresh();
		pieceButtons[0].GrabFocus();
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

		var hand = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(PanelWidth, 0) };
		hand.AddThemeConstantOverride("h_separation", 8);
		hand.AddThemeConstantOverride("v_separation", 8);
		for (var slot = 0; slot < battle.Bag.Hand.Count; slot++)
		{
			var index = slot;
			var button = UiKit.BigButton("", () => Place(index), PieceSize);
			button.CustomMinimumSize = new Vector2(PieceSize, PieceSize);
			hand.AddChild(button);
			pieceButtons.Add(button);
		}
		column.AddChild(hand);

		messageLabel = UiKit.Hint("");
		messageLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(messageLabel);

		var actions = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
		actions.AddThemeConstantOverride("h_separation", 8);
		actions.AddThemeConstantOverride("v_separation", 8);
		undoButton = UiKit.BigButton("Desfazer", Undo, 196);
		actions.AddChild(undoButton);
		actions.AddChild(UiKit.BigButton("Cancelar", Cancel, 196));
		confirmButton = UiKit.BigButton("Confirmar", Confirm, PanelWidth);
		actions.AddChild(confirmButton);
		column.AddChild(actions);

		var keys = UiKit.Hint($"Teclado: 1 a {battle.Bag.Hand.Count} peça, Backspace desfaz, Enter confirma, Esc cancela.");
		keys.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(keys);
	}

	void Place(int slot)
	{
		if (!battle.Staff.Place(slot)) return;
		messageLabel.Text = "";
		Refresh();
		FocusNextPiece(slot);
	}

	void Undo()
	{
		if (!battle.Staff.Undo()) return;
		messageLabel.Text = "";
		Refresh();
		FocusFirstPiece();
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

	void Refresh()
	{
		shieldLabel.Text = $"Escudo: {battle.Shield.Remaining}";
		var pieces = battle.Staff.Pieces;
		expressionLabel.Text = pieces.Count == 0
			? "Escolha as peças para montar a conta."
			: $"{string.Join(" + ", pieces)} = {battle.Staff.Total}";

		for (var slot = 0; slot < pieceButtons.Count; slot++)
		{
			var piece = battle.Bag.Hand[slot];
			// O numeral e os pontos aparecem juntos: a quantidade nunca depende só do símbolo.
			pieceButtons[slot].Text = piece is { } n ? $"{n}\n{Dots(n)}" : "";
			pieceButtons[slot].Disabled = piece is null;
			// O slot vazio continua no lugar, esmaecido, para as outras peças não pularem de posição.
			pieceButtons[slot].Modulate = new Color(1f, 1f, 1f, piece is null ? 0.2f : 1f);
		}
		undoButton.Disabled = pieces.Count == 0;
	}

	static string Dots(int n) => new string('•', Math.Min(n, 10));

	void FocusNextPiece(int after)
	{
		for (var offset = 1; offset <= pieceButtons.Count; offset++)
		{
			var button = pieceButtons[(after + offset) % pieceButtons.Count];
			if (button.Disabled) continue;
			button.GrabFocus();
			return;
		}
		confirmButton.GrabFocus();
	}

	void FocusFirstPiece()
	{
		foreach (var button in pieceButtons)
			if (!button.Disabled)
			{
				button.GrabFocus();
				return;
			}
		confirmButton.GrabFocus();
	}

	// Enter confirma mesmo com um botão de peça em foco (Espaço aciona o botão em foco).
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.PhysicalKeycode)
		{
			case Key.Enter or Key.KpEnter:
				GetViewport().SetInputAsHandled();
				Confirm();
				break;
			case Key.Backspace:
				GetViewport().SetInputAsHandled();
				Undo();
				break;
			case >= Key.Key1 and <= Key.Key9:
				var slot = (int)(key.PhysicalKeycode - Key.Key1);
				if (slot >= pieceButtons.Count) break;
				GetViewport().SetInputAsHandled();
				Place(slot);
				break;
		}
	}

	// Esc cancela: as peças voltam para a mão e nada conta como resposta.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		Cancel();
	}
}
