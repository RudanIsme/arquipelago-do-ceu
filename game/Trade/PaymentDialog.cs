using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// A pergunta de cada compra ou venda: "Eu pago" (a criança monta o valor ou o troco com dinheiro do Real) ou "Pagar sozinho".
// Errar no "Eu pago" não custa nada e não impede a compra, e nada daqui conta para a aprendizagem.
public partial class PaymentDialog : Control
{
	const int TargetHeight = 72;

	readonly string question;
	readonly string prompt;
	readonly ChangeChallenge challenge;
	readonly MoneyCatalog money;
	readonly Action<string> say;
	readonly Action paid;
	readonly bool canPayMyself;
	readonly List<int> table = [];
	readonly VBoxContainer content = new() { CustomMinimumSize = new Vector2(560, 0) };
	readonly Label message = BookStyle.Text("", 18);
	readonly Label assembled = BookStyle.Text("", 22);
	List<int> pieceValues;

	public PaymentDialog(string question, string prompt, ChangeChallenge challenge, MoneyCatalog money, Action<string> say, Action paid)
	{
		pieceValues = PiecesFor(challenge, money);
		canPayMyself = challenge.Target <= ChangeChallenge.MaxCents && ChangeChallenge.CanMake(challenge.Target, pieceValues);
		Name = "PaymentDialog";
		this.question = question;
		this.prompt = prompt;
		this.challenge = challenge;
		this.money = money;
		this.say = say;
		this.paid = paid;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Ready()
	{
		var shade = new ColorRect { Color = new Color(0.03f, 0.06f, 0.09f, 0.55f), MouseFilter = MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);
		content.AddThemeConstantOverride("separation", 12);
		var card = BookStyle.Panel(content);
		card.Name = "PaymentCard";
		card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
		card.GrowHorizontal = GrowDirection.Both;
		card.GrowVertical = GrowDirection.Both;
		AddChild(card);
		ShowChoice();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
		QueueFree();
		GetViewport().SetInputAsHandled();
	}

	// As peças que a mesa oferece: as do Real até o valor da nota entregue (ou do valor a montar).
	static List<int> PiecesFor(ChangeChallenge challenge, MoneyCatalog money) =>
		[.. money.Denominations.Where(d => d.Value <= Math.Max(challenge.Given, challenge.Target)).Select(d => d.Value)];

	void Clear()
	{
		foreach (var child in content.GetChildren())
		{
			content.RemoveChild(child);
			child.QueueFree();
		}
	}

	// Primeiro a criança escolhe como pagar; o foco começa no "Não", para um toque sem querer nunca gastar moedas.
	void ShowChoice()
	{
		Clear();
		var text = BookStyle.Text(question, 22);
		text.CustomMinimumSize = new Vector2(0, 80);
		content.AddChild(text);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var no = Button("Não", () => QueueFree(), "PayNo");
		row.AddChild(no);
		if (canPayMyself) row.AddChild(Button("Eu pago", StartPaying, "PayMyself"));
		row.AddChild(Button("Pagar sozinho", Finish, "PayAlone"));
		content.AddChild(row);
		say(question);
		no.GrabFocus();
	}

	void StartPaying()
	{
		Clear();
		table.Clear();
		var heading = BookStyle.Text(prompt, 22);
		heading.CustomMinimumSize = new Vector2(0, 70);
		content.AddChild(heading);
		content.AddChild(BuildPieces());
		assembled.Text = TableText();
		content.AddChild(assembled);
		message.Text = "";
		content.AddChild(message);
		content.AddChild(BuildActions());
		say(prompt);
	}

	Control BuildPieces()
	{
		var flow = new HFlowContainer { Name = "MoneyPieces" };
		flow.AddThemeConstantOverride("h_separation", 8);
		flow.AddThemeConstantOverride("v_separation", 8);
		foreach (var denomination in money.Denominations.Where(d => pieceValues.Contains(d.Value)))
		{
			var piece = denomination;
			var button = Button($"{piece.Name}\n{piece.Kind}", () => Add(piece), "Money_" + piece.Value);
			button.CustomMinimumSize = new Vector2(110, TargetHeight);
			flow.AddChild(button);
		}
		return flow;
	}

	Control BuildActions()
	{
		var row = new HFlowContainer();
		row.AddThemeConstantOverride("h_separation", 8);
		row.AddThemeConstantOverride("v_separation", 8);
		row.AddChild(Button("Apagar", Undo, "PayUndo"));
		row.AddChild(Button("Sugerir peça", Suggest, "PaySuggest"));
		row.AddChild(Button("Conferir", Check, "PayCheck"));
		row.AddChild(Button("Pagar sozinho", Finish, "PayAloneNow"));
		return row;
	}

	string TableText() =>
		table.Count == 0 ? "Toque nas moedas e notas para montar o valor." : string.Join(" + ", table.Select(v => money.Denominations.First(d => d.Value == v).Name));

	void Add(Denomination piece)
	{
		table.Add(piece.Value);
		Show(TableText(), "");
		say(piece.Name);
	}

	void Undo()
	{
		if (table.Count == 0) return;
		table.RemoveAt(table.Count - 1);
		Show(TableText(), "");
	}

	// A dica nunca custa nada: destaca a próxima peça em texto, ou manda desfazer se já passou do valor.
	void Suggest()
	{
		var (piece, undo) = challenge.Suggest(table, pieceValues);
		var text = undo || piece is null ? "Desfaça a última peça com o botão Apagar." : $"Tente {money.Denominations.First(d => d.Value == pieceValues[piece.Value]).Name}.";
		Show(assembled.Text, text);
		say(text);
	}

	// Errar aqui não custa nada e não impede a compra: a criança tenta de novo.
	void Check()
	{
		var result = challenge.Check(table);
		if (result.Correct)
		{
			say("Muito bem! O valor está certo.");
			Finish();
			return;
		}
		var text = result.Total > result.Target ? "Passou do valor. Tente de novo." : "Ainda falta dinheiro. Tente de novo.";
		Show(assembled.Text, text);
		say(text);
	}

	void Show(string pieces, string note)
	{
		assembled.Text = pieces;
		message.Text = note;
	}

	void Finish()
	{
		QueueFree();
		paid();
	}

	static Button Button(string text, Action action, string name)
	{
		var button = BookStyle.Button(text, action, name, TargetHeight);
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return button;
	}
}
