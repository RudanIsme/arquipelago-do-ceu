using System;
using Godot;

namespace ArquipelagoDoCeu;

// O guia curto da primeira vez: as caixinhas, a comparação e como equipar. Tem voz, "Pular" e funciona com toque, mouse e teclado.
public partial class InventoryGuide : Control
{
	const int TargetHeight = 72;

	static readonly string[] Pages =
	[
		"As caixinhas mostram o que o Personagem veste: Capacete, Armadura e Acessório.",
		"Aponte, toque ou use as setas em um equipamento. O Personagem o veste só na prévia, e o painel mostra apenas o que muda: ▲ sobe e ▼ desce.",
		"Para vestir de verdade, aperte Equipar. Equipar nunca cura a Energia. O cadeado quer dizer que falta Maestria.",
	];

	readonly Action<string> say;
	readonly Action done;
	Label? text;
	Button? next;
	int page;

	public InventoryGuide(Action<string> say, Action done)
	{
		Name = "InventoryGuide";
		this.say = say;
		this.done = done;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Ready()
	{
		var shade = new ColorRect { Color = new Color(0.03f, 0.06f, 0.09f, 0.55f), MouseFilter = MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);
		AddChild(BuildCard());
		ShowPage();
		next?.GrabFocus();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
		Finish();
		GetViewport().SetInputAsHandled();
	}

	Control BuildCard()
	{
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
		column.AddThemeConstantOverride("separation", 10);
		column.AddChild(BookStyle.Text("Como usar o Inventário", 24));
		text = BookStyle.Text("", 20);
		text.CustomMinimumSize = new Vector2(0, 110);
		column.AddChild(text);
		column.AddChild(BuildButtons());
		var card = BookStyle.Panel(column);
		card.Name = "GuideCard";
		card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
		card.GrowHorizontal = GrowDirection.Both;
		card.GrowVertical = GrowDirection.Both;
		return card;
	}

	HBoxContainer BuildButtons()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(Expanded(BookStyle.Button("Ouvir de novo", () => say(Pages[page]), "GuideReplay", TargetHeight)));
		next = Expanded(BookStyle.Button("Próximo", Advance, "GuideNext", TargetHeight));
		row.AddChild(next);
		row.AddChild(Expanded(BookStyle.Button("Pular", Finish, "GuideSkip", TargetHeight)));
		return row;
	}

	static Button Expanded(Button button)
	{
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return button;
	}

	void Advance()
	{
		if (page + 1 >= Pages.Length)
		{
			Finish();
			return;
		}
		page++;
		ShowPage();
	}

	void ShowPage()
	{
		text!.Text = Pages[page];
		next!.Text = page + 1 >= Pages.Length ? "Entendi" : "Próximo";
		say(Pages[page]);
	}

	void Finish()
	{
		done();
		QueueFree();
	}
}
