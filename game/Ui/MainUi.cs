using System;
using Godot;

namespace ArquipelagoDoCeu;

// Toda a interface do Lobby vive num Control que muda de escala: o tamanho do texto vale na hora em todas as telas,
// e o Control cobre a janela inteira na nova escala para as âncoras continuarem certas.
public sealed class RootUi
{
	public RootUi(CanvasLayer layer)
	{
		Root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		layer.AddChild(Root);
		foreach (var name in new[] { "Message", "Notice" })
		{
			var label = layer.GetNode<Label>(name);
			layer.RemoveChild(label);
			Root.AddChild(label);
		}
		Title = Root.GetNode<Label>("Message");
		Notice = Root.GetNode<Label>("Notice");
		// Abaixo da fileira de botões, para nunca cobri-los com texto maior ou janela estreita.
		Title.HorizontalAlignment = HorizontalAlignment.Left;
		Title.OffsetLeft = 16;
		Title.OffsetTop = 80;
		Title.OffsetBottom = 120;
		UiKit.Outline(Title);
		UiKit.Outline(Notice);
	}

	public Control Root { get; }

	public Label Title { get; }

	public Label Notice { get; }

	public void Fit(Viewport viewport, float textScale, bool compensateStretch)
	{
		var stretch = Mathf.Max(.1f, viewport.GetFinalTransform().Scale.X);
		var effectiveScale = compensateStretch ? textScale / stretch : textScale;
		Root.Scale = Vector2.One * effectiveScale;
		Root.Size = viewport.GetVisibleRect().Size / effectiveScale;
	}
}

// A barra antiga do Lobby (Perfis, Personagem, Configurações) e, embaixo, o botão de interação com a dica de controle.
public sealed class LegacyHud
{
	public LegacyHud(Control uiRoot, Action profiles, Action character, Action settings, Action interact)
	{
		Bar = new HBoxContainer();
		Bar.AddThemeConstantOverride("separation", 8);
		Bar.AddChild(UiKit.BigButton("Perfis", profiles, 160));
		CharacterButton = UiKit.BigButton("Personagem", character, 220);
		CharacterButton.Visible = false;
		Bar.AddChild(CharacterButton);
		Bar.AddChild(UiKit.BigButton("Configurações", settings, 260));
		uiRoot.AddChild(Bar);

		// O botão de interação e a dica de controle ficam embaixo, centralizados, longe das telas do alto.
		Bottom = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
		Bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		Bottom.OffsetTop = -200;
		Bottom.OffsetBottom = -90;
		Bottom.AddThemeConstantOverride("separation", 8);
		InteractButton = UiKit.BigButton("", interact, 380);
		InteractButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		InteractButton.Visible = false;
		Bottom.AddChild(InteractButton);
		Hint = UiKit.Hint("");
		Hint.CustomMinimumSize = new Vector2(760, 0);
		var hintBar = UiKit.HintBar(Hint);
		hintBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		Bottom.AddChild(hintBar);
		uiRoot.AddChild(Bottom);
	}

	public Control Bar { get; }

	public Button CharacterButton { get; }

	public Control Bottom { get; }

	public Button InteractButton { get; }

	public Label Hint { get; }
}
