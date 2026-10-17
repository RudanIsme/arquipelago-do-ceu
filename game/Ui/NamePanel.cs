using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// A montagem do nome pelo Escriba: a criança junta sílabas da bolsa. Não é modal: o Personagem anda e o tempo só desacelera.
// Toque numa sílaba a lê e abre o pontilhado; teclado: 1 a 9, Backspace desfaz, Enter confere, Esc solta. Nenhum tempo é cobrado.
public partial class NamePanel : OverlayScreen
{
	static int PieceSize => 76;
	static int PanelWidth => 4 * PieceSize + 3 * 8;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");

	readonly NameBattle battle;
	readonly TracingRules tracingRules;
	readonly VoiceService voice;
	readonly List<Button> pieceButtons = [];
	HFlowContainer piecesRow = null!;
	VBoxContainer content = null!;
	TracePad pad = null!;
	Label nameLabel = null!;
	Label resultLabel = null!;
	Button undoButton = null!;
	bool tracing;
	bool composing;
	int tracingPiece;

	readonly bool allowPull;

	public NamePanel(NameBattle battle, TracingRules tracingRules, VoiceService voice, bool allowPull = true)
	{
		this.battle = battle;
		this.tracingRules = tracingRules;
		this.voice = voice;
		this.allowPull = allowPull;
	}

	// O nome foi conferido; quem trata golpes, erros e Energia é o treino.
	public event Action<NameVerdict>? Checked;

	// Verdadeiro enquanto a criança desenha ou tem sílabas no nome: é quando o tempo desacelera.
	public event Action<bool>? ComposingChanged;

	// O nome certo e a ordem das sílabas na bolsa; só o piloto de desenvolvimento usa, para jogar sem saber o nome.
	public IReadOnlyList<string> ExpectedName => battle.Name;

	public IReadOnlyList<string> PieceNames => battle.Pieces;
	public NameBattle PullBattle => battle;
	public void ReturnFromPull(bool check) { Refresh(); if (check) Check(); }
	public void OpenPullMode()
	{
		if (tracing || battle.Kind != NameKind.Syllables) return;
		GetTree().Root.GetChildren().OfType<PullTestGate>().FirstOrDefault()?.EnterWithPanel(this);
	}

	public event Action? HintRequested;

	public event Action? ReadRequested;

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		Reload();
		// Sem foco de teclado: as setas e WASD continuam movendo o Personagem com o painel aberto.
		foreach (var button in FindChildren("*", "Button", true, false)) ((Button)button).FocusMode = FocusModeEnum.None;
	}

	// Perde o alvo sem responder: o que estava montado se desfaz.
	public void Abandon()
	{
		pad.Abort();
		battle.UndoAll();
		tracing = false;
		Close();
	}

	void Build()
	{
		var style = new StyleBoxFlat { BgColor = PanelFill, BorderColor = Gold };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(16);
		style.SetContentMarginAll(18);
		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		panel.AddThemeStyleboxOverride("panel", UiKit.Surface());
		AddChild(panel);
		UiKit.DockFit(this, panel, right: true);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		var heading = UiKit.Heading(battle.Kind switch { NameKind.Sentence => "Ponha as palavras na ordem", NameKind.Gap => "Complete a frase", _ => "Qual é o nome da criatura?" });
		heading.CustomMinimumSize = new Vector2(PanelWidth, 0);
		heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		column.AddChild(heading);
		nameLabel = UiKit.Caption("");
		nameLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(nameLabel);

		content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		column.AddChild(content);

		piecesRow = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(PanelWidth, 0) };
		piecesRow.AddThemeConstantOverride("h_separation", 8);
		piecesRow.AddThemeConstantOverride("v_separation", 8);
		content.AddChild(piecesRow);

		resultLabel = UiKit.Hint("");
		resultLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		content.AddChild(resultLabel);

		var help = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(PanelWidth, 0) };
		help.AddThemeConstantOverride("h_separation", 8);
		help.AddThemeConstantOverride("v_separation", 8);
		help.AddChild(UiKit.BigButton("Ouvir", () => ReadRequested?.Invoke(), 150));
		help.AddChild(UiKit.BigButton("Dica", () => HintRequested?.Invoke(), 150));
		undoButton = UiKit.BigButton("Desfazer", Undo, 200);
		help.AddChild(undoButton);
		content.AddChild(help);

		var actions = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(PanelWidth, 0) };
		actions.AddThemeConstantOverride("h_separation", 8);
		actions.AddChild(UiKit.BigButton("Soltar alvo", Abandon, 196));
		actions.AddChild(UiKit.BigButton("Conferir", Check, 196));
		content.AddChild(actions);
		// A pesca de teste (F9) só existe no Lobby; dentro de uma Dungeon o botão nem aparece.
		if (allowPull && battle.Kind == NameKind.Syllables) content.AddChild(UiKit.BigButton("Pescar com puxada", OpenPullMode, 260));

		var keys = UiKit.Hint("Teclado: 1 a 9 escolhem a peça, Backspace desfaz, Enter confere, Esc solta o alvo.");
		keys.CustomMinimumSize = new Vector2(PanelWidth, 0);
		content.AddChild(keys);

		pad = new TracePad(tracingRules) { Visible = false };
		pad.Completed += OnTraced;
		pad.Cancelled += EndTracing;
		column.AddChild(pad);
	}

	// Mostra o nome do Desafio atual: as sílabas da bolsa podem ter mudado (novo nome, embaralhada, peça escondida).
	public void Reload()
	{
		foreach (var button in pieceButtons)
		{
			piecesRow.RemoveChild(button);
			button.QueueFree();
		}
		pieceButtons.Clear();
		for (var i = 0; i < battle.Pieces.Count; i++)
		{
			var index = i;
			var button = UiKit.BigButton("", () => ChooseWithDrawing(index), PieceSize);
			button.CustomMinimumSize = new Vector2(PieceSize, PieceSize);
			button.FocusMode = FocusModeEnum.None;
			piecesRow.AddChild(button);
			pieceButtons.Add(button);
		}
		resultLabel.Text = "";
		Refresh();
	}

	// Uma mensagem escrita que também é dita pela voz: toda instrução tem as duas versões.
	public void Tell(string text)
	{
		resultLabel.Text = text;
		voice.Say(text, interrupt: true);
	}

	public void Refresh()
	{
		var slots = Enumerable.Range(0, battle.Name.Count).Select(i => i < battle.Chosen.Count ? Label(battle.Pieces[battle.Chosen[i]]) : "__");
		nameLabel.Text = battle.Kind switch
		{
			NameKind.Sentence => string.Join(" ", slots),
			NameKind.Gap => $"{battle.Before}{slots.First()}{battle.After}",
			_ => string.Join("  ·  ", slots),
		};
		for (var i = 0; i < pieceButtons.Count; i++)
		{
			var hidden = battle.IsHidden(i);
			pieceButtons[i].Text = hidden ? "?" : Label(battle.Pieces[i]);
			pieceButtons[i].Disabled = hidden || battle.Chosen.Contains(i);
			// A peça escondida fica no lugar, esmaecida e com "?", para as outras não pularem de posição.
			pieceButtons[i].Modulate = new Color(1f, 1f, 1f, hidden ? 0.4f : 1f);
		}
		undoButton.Disabled = battle.Chosen.Count == 0;
		NotifyComposing();
	}

	// Sílabas vão em maiúscula, como no pontilhado; palavras de frases ficam como foram escritas, com a ortografia à vista.
	string Label(string piece) => battle.Kind == NameKind.Syllables ? piece.ToUpperInvariant() : piece;

	// Com o mouse ou o dedo, tocar na sílaba a diz e abre o pontilhado; ela só entra no nome depois de desenhada.
	// Palavras de frases entram direto: no 3º ano a criança já escreve, e o desafio é a ordem e a ortografia.
	async void ChooseWithDrawing(int index)
	{
		if (tracing || battle.Chosen.Contains(index) || battle.IsHidden(index) || battle.Chosen.Count >= battle.Name.Count) return;
		if (battle.Kind != NameKind.Syllables)
		{
			Choose(index);
			return;
		}
		tracing = true;
		tracingPiece = index;
		content.Visible = false;
		pad.Visible = true;
		NotifyComposing();
		var syllable = battle.Pieces[index];
		voice.Say($"Desenhe a sílaba {syllable}.", interrupt: true);
		await pad.Begin(syllable.ToUpperInvariant(), $"Desenhe {syllable.ToUpperInvariant()}");
	}

	void OnTraced()
	{
		var index = tracingPiece;
		EndTracing();
		Choose(index);
	}

	void EndTracing()
	{
		pad.Abort();
		tracing = false;
		pad.Visible = false;
		content.Visible = true;
		Refresh();
	}

	void Choose(int index)
	{
		if (!battle.Place(index)) return;
		voice.Say(battle.Pieces[index], interrupt: true);
		resultLabel.Text = "";
		Refresh();
	}

	void Undo()
	{
		if (!battle.Undo()) return;
		resultLabel.Text = "";
		Refresh();
	}

	void Check()
	{
		if (battle.Remaining == 0)
		{
			Tell("A criatura já foi libertada. Espere o novo nome.");
			return;
		}
		if (battle.Chosen.Count == 0)
		{
			Tell("Escolha as sílabas do nome antes de conferir.");
			return;
		}
		var verdict = battle.Check();
		// Cada sílaba leva a palavra "certa" ou "errada": a cor sozinha nunca diz o resultado.
		var parts = battle.Chosen.Select((piece, i) => $"{Label(battle.Pieces[piece])}: {(verdict.Correct[i] ? "certa" : "errada")}");
		resultLabel.Text = string.Join("   ", parts);
		Refresh();
		Checked?.Invoke(verdict);
	}

	void NotifyComposing()
	{
		var now = tracing || battle.Chosen.Count > 0;
		if (now == composing) return;
		composing = now;
		ComposingChanged?.Invoke(now);
	}

	// No teclado, escolher a sílaba pelo número vale como escrevê-la.
	public override void _Input(InputEvent @event)
	{
		if (tracing || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
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

	// Esc volta do desenho; sem desenho aberto, solta o alvo.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		if (tracing) EndTracing();
		else Abandon();
	}
}
