using System;
using ArquipelagoDoCeu.Dungeon;
using Godot;

namespace ArquipelagoDoCeu;

// O mapa em tela cheia: a cena escurece e o Personagem abre a folha e a segura pelos cantos. Pausa o jogo enquanto está aberto.
// A legenda tem ícone e texto, e segurar uma Sala (ou o botão) fala em voz alta. Abre por toque, mouse ou tecla M.
public partial class MapScreen : OverlayScreen
{
	const float RiseSeconds = 0.35f;
	const float RiseDistance = 140f;

	readonly Expedition expedition;
	readonly string title;
	readonly bool reduceMotion;
	readonly bool floorDone;
	readonly Action<int> speakRoom;
	readonly Action speakMap;
	PanelContainer sheet = null!;
	CenterContainer holder = null!;
	bool unpaused;

	public MapScreen(Expedition expedition, string title, bool floorDone, bool reduceMotion, Action<int> speakRoom, Action speakMap)
	{
		this.expedition = expedition;
		this.title = title;
		this.floorDone = floorDone;
		this.reduceMotion = reduceMotion;
		this.speakRoom = speakRoom;
		this.speakMap = speakMap;
		Name = "MapaEmTelaCheia";
		ProcessMode = ProcessModeEnum.Always;
	}

	// A Sala (ou o botão) que a criança segura fala; a tela não anda sozinha enquanto isso.
	public bool Paused => GetTree().Paused;

	public override void _Ready()
	{
		base._Ready();
		GetTree().Paused = true;
		TreeExiting += Resume;
		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.62f), MouseFilter = MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		holder = new CenterContainer();
		holder.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(holder);

		sheet = new PanelContainer { Name = "Folha" };
		sheet.AddThemeStyleboxOverride("panel", UiKit.Surface());
		holder.AddChild(sheet);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		sheet.AddChild(box);

		var heading = UiKit.Heading(title);
		box.AddChild(heading);

		var map = new FloorMap(expedition.Floor, new Vector2(760f, 400f)) { Source = expedition, Here = expedition.CurrentRoom, Cleared = new System.Collections.Generic.HashSet<int>(expedition.Cleared), Complete = floorDone };
		map.RoomPointed += room => speakRoom(room);
		box.AddChild(map);

		box.AddChild(Legend());
		var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		footer.AddThemeConstantOverride("separation", 12);
		footer.AddChild(UiKit.BigButton("Ouvir o mapa", () => speakMap(), 200));
		var back = UiKit.BigButton("Voltar", Close, 200);
		footer.AddChild(back);
		box.AddChild(footer);
		back.GrabFocus();

		AddChild(new Hands(sheet));
		Rise();
		speakMap();
	}

	// A folha sobe e as mãos a seguram; com "reduzir animações" ela aparece direto.
	void Rise()
	{
		if (reduceMotion) return;
		holder.Modulate = new Color(1f, 1f, 1f, 0f);
		holder.Position += new Vector2(0f, RiseDistance);
		var tween = CreateTween().SetParallel();
		tween.TweenProperty(holder, "modulate:a", 1f, RiseSeconds);
		tween.TweenProperty(holder, "position:y", 0f, RiseSeconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(Controls.Map))
		{
			GetViewport().SetInputAsHandled();
			Close();
			return;
		}
		base._UnhandledInput(@event);
	}

	static Control Legend()
	{
		var row = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("h_separation", 22);
		foreach (var (icon, text) in new[] { (LegendIcon.Mark.You, "Você"), (LegendIcon.Mark.Done, "Vencida"), (LegendIcon.Mark.Portal, "Portal"), (LegendIcon.Mark.Unseen, "Não vista") })
		{
			var item = new HBoxContainer();
			item.AddThemeConstantOverride("separation", 6);
			item.AddChild(new LegendIcon(icon));
			var label = new Label { Text = text };
			label.AddThemeFontSizeOverride("font_size", 20);
			label.AddThemeColorOverride("font_color", UiKit.TextColor);
			item.AddChild(label);
			row.AddChild(item);
		}
		return row;
	}

	void Resume()
	{
		if (unpaused) return;
		unpaused = true;
		if (GetTree() is { } tree) tree.Paused = false;
	}

	// Cada marca do mapa tem um ícone desenhado, e nunca só uma cor.
	sealed partial class LegendIcon : Control
	{
		public enum Mark { You, Done, Portal, Unseen }

		readonly Mark mark;

		public LegendIcon(Mark mark)
		{
			this.mark = mark;
			CustomMinimumSize = new Vector2(30f, 30f);
		}

		public override void _Draw()
		{
			var c = new Vector2(15f, 15f);
			switch (mark)
			{
				case Mark.You:
					DrawCircle(c + new Vector2(0f, -6f), 5f, Colors.Black);
					DrawRect(new Rect2(c + new Vector2(-5f, -1f), new Vector2(10f, 11f)), Colors.Black);
					DrawCircle(c + new Vector2(0f, -6f), 3.5f, Colors.White);
					DrawRect(new Rect2(c + new Vector2(-3.5f, 0f), new Vector2(7f, 9f)), Colors.White);
					break;
				case Mark.Done:
					Vector2[] check = [c + new Vector2(-8f, 0f), c + new Vector2(-2f, 6f), c + new Vector2(9f, -7f)];
					DrawPolyline(check, Colors.Black, 7f, true);
					DrawPolyline(check, Color.FromHtml("#7CFC8A"), 4f, true);
					break;
				case Mark.Portal:
					DrawLine(c + new Vector2(-9f, 0f), c + new Vector2(9f, 0f), Colors.Black, 7f);
					DrawLine(c + new Vector2(0f, -9f), c + new Vector2(0f, 9f), Colors.Black, 7f);
					DrawLine(c + new Vector2(-9f, 0f), c + new Vector2(9f, 0f), Colors.White, 3.5f);
					DrawLine(c + new Vector2(0f, -9f), c + new Vector2(0f, 9f), Colors.White, 3.5f);
					break;
				default:
					DrawCircle(c, 12f, Color.FromHtml("#8A8399"));
					DrawArc(c, 12f, 0f, Mathf.Tau, 20, Color.FromHtml("#E6E0F2"), 2f, true);
					DrawString(ThemeDB.FallbackFont, c + new Vector2(-5f, 7f), "?", HorizontalAlignment.Left, -1, 20, Colors.Black);
					break;
			}
		}
	}

	// As mãos do Personagem nos cantos de cima da folha, com os braços saindo pela borda de baixo da tela.
	sealed partial class Hands : Control
	{
		static readonly Color Skin = Color.FromHtml("#E8B48A");

		readonly Control sheet;

		public Hands(Control sheet)
		{
			this.sheet = sheet;
			MouseFilter = MouseFilterEnum.Ignore;
			SetAnchorsPreset(LayoutPreset.FullRect);
		}

		public override void _Process(double delta) => QueueRedraw();

		public override void _Draw()
		{
			var rect = sheet.GetGlobalRect();
			foreach (var corner in new[] { rect.Position + new Vector2(6f, 6f), rect.Position + new Vector2(rect.Size.X - 6f, 6f) })
			{
				var foot = new Vector2(corner.X < rect.GetCenter().X ? corner.X - 70f : corner.X + 70f, Size.Y + 20f);
				DrawLine(corner, foot, Colors.Black, 26f, true);
				DrawLine(corner, foot, Skin, 20f, true);
				DrawCircle(corner, 17f, Colors.Black);
				DrawCircle(corner, 14f, Skin);
			}
		}
	}
}
