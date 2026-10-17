using System;
using Godot;

namespace ArquipelagoDoCeu;

// Uma pergunta de Sim ou Não por cima da tela, dita em voz. Esc e "Não" desistem; o foco começa no "Não", para um toque
// sem querer nunca gastar moedas.
public partial class ConfirmDialog : Control
{
	const int TargetHeight = 72;

	readonly string message;
	readonly Action<string> say;
	readonly Action confirmed;
	readonly Action? cancelled;

	public ConfirmDialog(string message, Action<string> say, Action confirmed, Action? cancelled = null)
	{
		Name = "ConfirmDialog";
		this.message = message;
		this.say = say;
		this.confirmed = confirmed;
		this.cancelled = cancelled;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Ready()
	{
		var shade = new ColorRect { Color = new Color(0.03f, 0.06f, 0.09f, 0.55f), MouseFilter = MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);
		AddChild(BuildCard(out var no));
		say(message);
		no.GrabFocus();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
		Finish(confirmed: false);
		GetViewport().SetInputAsHandled();
	}

	Control BuildCard(out Button no)
	{
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
		column.AddThemeConstantOverride("separation", 12);
		var text = BookStyle.Text(message, 22);
		text.CustomMinimumSize = new Vector2(0, 80);
		column.AddChild(text);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		no = BookStyle.Button("Não", () => Finish(confirmed: false), "ConfirmNo", TargetHeight);
		var yes = BookStyle.Button("Sim", () => Finish(confirmed: true), "ConfirmYes", TargetHeight);
		no.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		yes.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(no);
		row.AddChild(yes);
		column.AddChild(row);
		var card = BookStyle.Panel(column);
		card.Name = "ConfirmCard";
		card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
		card.GrowHorizontal = GrowDirection.Both;
		card.GrowVertical = GrowDirection.Both;
		return card;
	}

	void Finish(bool confirmed)
	{
		QueueFree();
		if (confirmed) this.confirmed();
		else cancelled?.Invoke();
	}
}
