using System;
using Godot;

namespace ArquipelagoDoCeu;

// Uma moldura da biblioteca, da loja ou da caixinha do Personagem: só o ícone e, num cantinho, o que o item é agora (equipado, novo,
// preço ou a Maestria que falta). O nome fica na dica do mouse e nos detalhes. O estado vem sempre em texto, nunca só em cor.
public static class ItemToken
{
	const string BadgeName = "Badge";
	const string IconName = "Icon";

	// Os estilos são os mesmos em toda moldura: criados uma vez.
	static readonly Lazy<(string State, StyleBoxFlat Style)[]> Styles = new(() =>
	[
		("normal", Flat(BookStyle.Box())),
		("hover", Flat(BookStyle.Emphasized(BookStyle.Page))),
		("focus", Flat(BookStyle.Emphasized(BookStyle.Page))),
		("pressed", Flat(BookStyle.Emphasized(BookStyle.Selected))),
		("disabled", Flat(BookStyle.Box(BookStyle.Disabled))),
	]);

	// Sem margem de conteúdo: a moldura tem sempre o mesmo tamanho, no normal, com o mouse por cima ou depois de usada.
	static StyleBoxFlat Flat(StyleBoxFlat box)
	{
		box.SetContentMarginAll(0);
		return box;
	}

	public static Button Create(string name, Action pressed, string nodeName, int size, string? badge = null, bool dim = false, Texture2D? icon = null)
	{
		var button = BookStyle.Button("", pressed, nodeName, size);
		button.CustomMinimumSize = new Vector2(size, size);
		button.TooltipText = name;
		foreach (var (state, style) in Styles.Value)
			button.AddThemeStyleboxOverride(state, style);
		if (dim) button.Modulate = new Color(1f, 1f, 1f, 0.6f);
		var picture = new TextureRect { Name = IconName, Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
		picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var inset = size * 0.16f;
		picture.OffsetLeft = inset;
		picture.OffsetTop = inset;
		picture.OffsetRight = -inset;
		picture.OffsetBottom = -inset;
		button.AddChild(picture);
		// Sem ícone, a moldura traz o nome, para nunca ficar vazia.
		if (icon is null) button.Text = name;
		button.ClipText = true;
		button.AddThemeFontSizeOverride("font_size", 12);
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
