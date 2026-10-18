using System;
using Godot;

namespace ArquipelagoDoCeu;

// Um quadrado da biblioteca ou da caixinha do Personagem: o nome do item e, num cantinho, o que ele é agora (equipado, novo ou a
// Maestria que falta). O estado vem sempre em texto, nunca só em cor; o item bloqueado também fica esmaecido.
public static class ItemToken
{
	const string BadgeName = "Badge";

	// Os estilos são os mesmos em todo quadrado: criados uma vez, com o texto começando abaixo do cantinho do estado.
	static readonly Lazy<(string State, StyleBoxFlat Style)[]> Styles = new(() =>
	[
		("normal", Padded(BookStyle.Box())),
		("hover", Padded(BookStyle.Emphasized(BookStyle.Page))),
		("focus", Padded(BookStyle.Emphasized(BookStyle.Page))),
		("pressed", Padded(BookStyle.Emphasized(BookStyle.Selected))),
		("disabled", Padded(BookStyle.Box(BookStyle.Disabled))),
	]);

	static StyleBoxFlat Padded(StyleBoxFlat box)
	{
		box.ContentMarginTop = 22;
		box.ContentMarginLeft = 3;
		box.ContentMarginRight = 3;
		box.ContentMarginBottom = 3;
		return box;
	}

	public static Button Create(string name, Action pressed, string nodeName, int size, string? badge = null, bool dim = false, Texture2D? icon = null)
	{
		var button = BookStyle.Button(name, pressed, nodeName, size);
		button.CustomMinimumSize = new Vector2(size, size);
		button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		button.ClipText = true;
		button.AddThemeFontSizeOverride("font_size", 13);
		foreach (var (state, style) in Styles.Value)
			button.AddThemeStyleboxOverride(state, style);
		button.TooltipText = name;
		if (icon is not null)
		{
			button.Icon = icon;
			button.IconAlignment = HorizontalAlignment.Center;
			button.VerticalIconAlignment = VerticalAlignment.Top;
			button.ExpandIcon = true;
			button.AddThemeConstantOverride("icon_max_width", size * 5 / 9);
			button.AddThemeFontSizeOverride("font_size", 11);
		}
		if (dim) button.Modulate = new Color(1f, 1f, 1f, 0.6f);
		SetBadge(button, badge);
		return button;
	}

	public static void SetBadge(Button button, string? badge)
	{
		if (button.GetNodeOrNull<Label>(BadgeName) is { } old)
		{
			button.RemoveChild(old);
			old.QueueFree();
		}
		if (badge is null) return;
		var label = BookStyle.Text(badge, 15);
		label.Name = BadgeName;
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.Position = new Vector2(3, 3);
		var box = BookStyle.Box(BookStyle.Selected);
		box.SetContentMarginAll(2);
		label.AddThemeStyleboxOverride("normal", box);
		button.AddChild(label);
	}
}
