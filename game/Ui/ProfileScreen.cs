using System;
using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// Tela de Perfis: criar o primeiro, escolher quem joga, criar outro e mudar o apelido.
// A criança só digita um apelido; nenhum campo pede nome real, foto ou escola.
public partial class ProfileScreen : Control
{
	readonly ProfileService profiles;

	public event Action<ProfileSave>? Chosen;
	public event Action? Cancelled;

	// Com um Perfil já em jogo, dá para sair sem trocar (Esc ou "Voltar ao jogo"); na primeira vez não há para onde voltar.
	readonly bool canCancel;

	public ProfileScreen(ProfileService profiles, bool canCancel)
	{
		this.profiles = profiles;
		this.canCancel = canCancel;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!canCancel || !@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		Cancelled?.Invoke();
	}

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		ShowHome();
	}

	// Sem nenhum Perfil só dá para criar; com algum, a criança escolhe.
	public void ShowHome()
	{
		if (profiles.Profiles.Count == 0) ShowForm(null);
		else ShowList();
	}

	void ShowList()
	{
		var box = UiKit.Card(this, "Quem vai jogar?");
		foreach (var profile in profiles.Profiles)
		{
			var picked = profile;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			var play = UiKit.BigButton(profile.Nickname, () => Chosen?.Invoke(picked), 280);
			play.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(play);
			row.AddChild(UiKit.BigButton("Editar", () => ShowForm(picked), 140));
			box.AddChild(row);
		}
		if (profiles.CanCreate) box.AddChild(UiKit.BigButton("Novo perfil", () => ShowForm(null)));
		if (canCancel) box.AddChild(UiKit.BigButton("Voltar ao jogo", () => Cancelled?.Invoke()));
		FocusFirstButton(box);
	}

	// Criar (editing == null) ou mudar o apelido de um Perfil existente.
	void ShowForm(ProfileSave? editing)
	{
		var box = UiKit.Card(this, editing is null ? "Como você quer ser chamado?" : "Mudar o apelido");
		var input = new LineEdit
		{
			PlaceholderText = "Seu apelido",
			Text = editing?.Nickname ?? "",
			MaxLength = profiles.MaxNicknameLength,
			CustomMinimumSize = new Vector2(240, UiKit.MinTouch),
		};
		input.AddThemeFontSizeOverride("font_size", UiKit.BodyFont);
		var message = UiKit.Hint("Use letras, números e espaços.");

		void Submit()
		{
			var result = editing is null ? profiles.Create(input.Text) : profiles.Rename(editing.Id, input.Text);
			if (!result.IsOk)
			{
				message.Text = result.Error!;
				return;
			}
			if (editing is null) Chosen?.Invoke(result.Value!);
			else ShowList();
		}

		input.TextSubmitted += _ => Submit();
		box.AddChild(input);
		box.AddChild(message);
		box.AddChild(UiKit.BigButton(editing is null ? "Criar" : "Salvar", Submit));
		if (profiles.Profiles.Count > 0) box.AddChild(UiKit.BigButton("Voltar", ShowList));
		input.CallDeferred(Control.MethodName.GrabFocus);
	}

	static void FocusFirstButton(Node box)
	{
		var first = System.Linq.Enumerable.FirstOrDefault(box.FindChildren("*", "Button", true, false));
		first?.CallDeferred(Control.MethodName.GrabFocus);
	}
}
