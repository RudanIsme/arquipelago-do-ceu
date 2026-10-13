using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// A montagem do nome pelo Escriba: a criança junta sílabas da bolsa para formar o nome da criatura.
// Mouse e toque usam os botões; no teclado, 1 a 9 escolhem a sílaba, Backspace desfaz, Enter confere e Esc fecha.
// Nenhum tempo é cobrado, e a fonte para dislexia vem do tema da interface.
public partial class NamePanel : OverlayScreen
{
	const int PieceSize = 120;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");

	readonly IReadOnlyList<string> name;
	readonly IReadOnlyList<string> pieces;
	readonly List<int> chosen = [];
	readonly List<Button> pieceButtons = [];
	Label nameLabel = null!;
	Label resultLabel = null!;
	Button undoButton = null!;
	Button checkButton = null!;

	public NamePanel(IReadOnlyList<string> name, IReadOnlyList<string> pieces)
	{
		this.name = name;
		this.pieces = pieces;
	}

	// Verdadeiro quando o nome completo foi montado certo.
	public bool Completed { get; private set; }

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
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize);
		panel.AnchorLeft = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical = GrowDirection.Begin;
		panel.OffsetBottom = -16;
		panel.AddThemeStyleboxOverride("panel", style);
		AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		column.AddChild(UiKit.Heading($"Monte o nome com {name.Count} sílaba{(name.Count == 1 ? "" : "s")}"));
		nameLabel = UiKit.Caption("");
		column.AddChild(nameLabel);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 10);
		for (var i = 0; i < pieces.Count; i++)
		{
			var index = i;
			var button = UiKit.BigButton(pieces[i].ToUpperInvariant(), () => Choose(index), PieceSize);
			button.CustomMinimumSize = new Vector2(PieceSize, PieceSize);
			row.AddChild(button);
			pieceButtons.Add(button);
		}
		column.AddChild(row);

		resultLabel = UiKit.Hint("");
		column.AddChild(resultLabel);

		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		actions.AddThemeConstantOverride("separation", 12);
		undoButton = UiKit.BigButton("Desfazer", Undo, 200);
		actions.AddChild(undoButton);
		checkButton = UiKit.BigButton("Conferir", Check, 240);
		actions.AddChild(checkButton);
		actions.AddChild(UiKit.BigButton("Fechar", Close, 200));
		column.AddChild(actions);

		column.AddChild(UiKit.Hint($"Teclado: 1 a {pieces.Count} escolhem a sílaba, Backspace desfaz, Enter confere, Esc fecha."));
	}

	IReadOnlyList<string> Assembled => chosen.Select(i => pieces[i]).ToList();

	void Choose(int index)
	{
		if (chosen.Contains(index) || chosen.Count >= name.Count) return;
		chosen.Add(index);
		resultLabel.Text = "";
		Refresh();
		FocusFirstFree();
	}

	void Undo()
	{
		if (chosen.Count == 0) return;
		chosen.RemoveAt(chosen.Count - 1);
		resultLabel.Text = "";
		Refresh();
		FocusFirstFree();
	}

	void Check()
	{
		var check = NameChecker.Check(name, Assembled);
		Completed = check.IsComplete;
		// Cada sílaba leva a palavra "certa" ou "errada": a cor sozinha nunca diz o resultado.
		var parts = name.Select((syllable, i) =>
			i < chosen.Count ? $"{Assembled[i].ToUpperInvariant()}: {(check.Correct[i] ? "certa" : "errada")}" : $"{i + 1}ª sílaba: falta");
		resultLabel.Text = check.IsComplete
			? $"Muito bem! Você escreveu {string.Concat(name).ToUpperInvariant()}."
			: string.Join("   ", parts) + "\nDesfaça o que está errado e tente de novo.";
	}

	void Refresh()
	{
		var slots = Enumerable.Range(0, name.Count).Select(i => i < chosen.Count ? pieces[chosen[i]].ToUpperInvariant() : "__");
		nameLabel.Text = string.Join("  ·  ", slots);
		for (var i = 0; i < pieceButtons.Count; i++) pieceButtons[i].Disabled = chosen.Contains(i);
		undoButton.Disabled = chosen.Count == 0;
	}

	void FocusFirstFree()
	{
		foreach (var button in pieceButtons)
			if (!button.Disabled)
			{
				button.GrabFocus();
				return;
			}
		checkButton.GrabFocus();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.PhysicalKeycode)
		{
			case Key.Enter or Key.KpEnter:
				GetViewport().SetInputAsHandled();
				Check();
				break;
			case Key.Backspace:
				GetViewport().SetInputAsHandled();
				Undo();
				break;
			case >= Key.Key1 and <= Key.Key9:
				var index = (int)(key.PhysicalKeycode - Key.Key1);
				if (index >= pieceButtons.Count) break;
				GetViewport().SetInputAsHandled();
				Choose(index);
				break;
		}
	}
}
