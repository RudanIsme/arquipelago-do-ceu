using System;
using Godot;

namespace ArquipelagoDoCeu;

// O card do canto superior esquerdo: o avatar (a inicial do apelido), o apelido e a Maestria do Papel de agora.
// O lápis de Editar só existe no Lobby. Segurar o card (dedo ou mouse) lê tudo em voz alta.
public partial class AvatarCard : PanelContainer
{
	const float HoldSeconds = 0.5f;

	readonly Label nameLabel = new();
	readonly Label masteryLabel = new();
	readonly Badge badge = new();
	readonly Button edit;
	readonly Action<string> speak;
	string spoken = "";
	int pressEpoch;

	public event Action? EditRequested;

	public AvatarCard(Action<string> speak, bool canEdit)
	{
		Name = "AvatarCard";
		this.speak = speak;
		MouseFilter = MouseFilterEnum.Stop;
		AddThemeStyleboxOverride("panel", UiKit.Surface());

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(badge);

		var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		masteryLabel.AddThemeFontSizeOverride("font_size", 16);
		foreach (var label in new[] { nameLabel, masteryLabel })
		{
			label.AddThemeColorOverride("font_color", UiKit.TextColor);
			label.MouseFilter = MouseFilterEnum.Ignore;
			texts.AddChild(label);
		}
		row.AddChild(texts);

		edit = UiKit.BigButton("Editar", () => EditRequested?.Invoke(), 72);
		edit.Name = "Editar";
		edit.CustomMinimumSize = new Vector2(72, 72);
		edit.TooltipText = "Editar o Personagem";
		edit.Visible = canEdit;
		row.AddChild(edit);
		AddChild(row);
		CustomMinimumSize = new Vector2(canEdit ? 320 : 240, 0);
		SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize);
		OffsetLeft = 16;
		OffsetTop = 12;
	}

	public string Nickname => nameLabel.Text;

	public string MasteryText => masteryLabel.Text;

	public bool EditVisible => edit.Visible;

	public Button EditButton => edit;

	public void Set(string nickname, string role, int mastery)
	{
		nameLabel.Text = nickname;
		masteryLabel.Text = $"Maestria {mastery} · {role}";
		badge.SetInitial(nickname.Length > 0 ? nickname[..1].ToUpperInvariant() : "?");
		spoken = $"{nickname}. Maestria {mastery} no Papel {role}.";
		TooltipText = spoken;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } press) return;
		var mine = ++pressEpoch;
		if (!press.Pressed) return;
		GetTree().CreateTimer(HoldSeconds, true, false, true).Timeout += () =>
		{
			if (mine == pressEpoch && IsInsideTree() && spoken.Length != 0) speak(spoken);
		};
	}

	// O avatar é um círculo com a inicial: com o texto ao lado, nada depende só da cor.
	sealed partial class Badge : Control
	{
		string initial = "?";

		public Badge()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			CustomMinimumSize = new Vector2(56, 56);
		}

		public void SetInitial(string value)
		{
			initial = value;
			QueueRedraw();
		}

		public override void _Draw()
		{
			var center = Size / 2;
			DrawCircle(center, 27, Colors.Black);
			DrawCircle(center, 25, Color.FromHtml("#D9A64A"));
			var font = ThemeDB.FallbackFont;
			var width = font.GetStringSize(initial, HorizontalAlignment.Left, -1, 28).X;
			DrawString(font, center + new Vector2(-width / 2, 10), initial, HorizontalAlignment.Left, -1, 28, Colors.Black);
		}
	}
}
