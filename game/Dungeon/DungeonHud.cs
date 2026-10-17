using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Dungeon;
using Godot;

namespace ArquipelagoDoCeu;

// O HUD da Dungeon: placa do Andar, mapa, card da criança, Energia, recados e aviso de moedas.
// Só mostra o que a Expedição já decidiu; quem reage aos botões é a DungeonRun, por eventos.
public partial class DungeonHud : Node
{
	const float MessageSeconds = 4f;
	const float CoinToastSeconds = 4f;

	// A Energia fica logo abaixo do card do avatar.
	public const int EnergyTop = 104;

	readonly DungeonContext ctx;
	readonly Expedition expedition;
	readonly Control hud = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
	readonly PanelContainer plate = new() { Name = "Placa" };
	readonly Label header = new();
	readonly Label message = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
	readonly VBoxContainer mapBox = new();
	readonly Label coinToast = new() { Name = "AvisoDeMoedas", MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
	readonly Label floorLabel = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
	readonly ProgressBar floorBar = new() { ShowPercentage = false, CustomMinimumSize = new Godot.Vector2(0, 14) };
	AvatarCard card = null!;
	Dock dock = null!;
	Button interactButton = null!;
	FloorMap? map;
	int toastEpoch;
	int messageEpoch;

	public DungeonHud(DungeonContext ctx, Expedition expedition)
	{
		this.ctx = ctx;
		this.expedition = expedition;
	}

	public EnergyView Energy { get; } = new("Voltar ao Lobby");

	// Diz se há uma luta aberta; com ela o Inventário não abre e o dock some.
	public Func<bool> InCombat { get; set; } = () => false;

	public event Action? InteractPressed;

	public event Action? LeaveRequested;

	public event Action? MapSpeechRequested;

	public event Action<int>? RoomPointed;

	public string Message => message.Text;

	// O último aviso de moedas (fica guardado mesmo depois de sumir da tela), para o piloto conferir.
	public string CoinToast => coinToast.Text;

	public override void _Ready()
	{
		Build();
		ctx.UiRoot.AddChild(hud);
		// O card (avatar, apelido e Maestria) fica no canto; a Energia, em raios, logo abaixo.
		card = new AvatarCard(text => ctx.Voice.Say(text, interrupt: true), canEdit: false);
		hud.AddChild(card);
		dock = BuildDock();
		BuildCoinToast();
		ShowCoins($"Moedas: {ctx.Progress.Wallet.Balance(ctx.ProfileId)}");
		ctx.UiRoot.AddChild(Energy);
		Energy.LeaveRequested += () => LeaveRequested?.Invoke();
		RefreshEnergy();
		Energy.OffsetTop = EnergyTop;
	}

	void Build()
	{
		hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		header.AddThemeFontSizeOverride("font_size", 18);
		header.AddThemeColorOverride("font_color", UiKit.TextColor);
		header.AutowrapMode = TextServer.AutowrapMode.WordSmart;

		// O mapa do Andar fica no canto, com a legenda das letras e formas.
		var panel = new PanelContainer { Name = "Mapa" };
		panel.AddThemeStyleboxOverride("panel", UiKit.Surface());
		panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		panel.OffsetLeft = -300;
		panel.OffsetRight = -12;
		panel.OffsetTop = 12;
		mapBox.AddThemeConstantOverride("separation", 4);
		floorLabel.AddThemeFontSizeOverride("font_size", 15);
		floorLabel.AddThemeColorOverride("font_color", UiKit.TextColor);
		mapBox.AddChild(floorBar);
		mapBox.AddChild(floorLabel);
		mapBox.AddChild(UiKit.BigButton("Ouvir o mapa", () => MapSpeechRequested?.Invoke(), 200));
		panel.AddChild(mapBox);
		hud.AddChild(panel);

		// A placa do centro: Nível, Andar e o objetivo (o Conceito do Andar, que vem das Missões).
		plate.AddThemeStyleboxOverride("panel", UiKit.Surface());
		plate.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		plate.OffsetLeft = -200;
		plate.OffsetRight = 200;
		plate.OffsetTop = 12;
		plate.GrowHorizontal = Control.GrowDirection.Both;
		header.HorizontalAlignment = HorizontalAlignment.Center;
		plate.AddChild(header);
		hud.AddChild(plate);

		message.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		message.OffsetTop = -190;
		message.OffsetBottom = -110;
		message.OffsetRight = -310;
		message.AddThemeFontSizeOverride("font_size", 22);
		message.AddThemeColorOverride("font_color", Colors.White);
		UiKit.Outline(message);
		hud.AddChild(message);

		interactButton = UiKit.BigButton("", () => InteractPressed?.Invoke(), 380);
		interactButton.Visible = false;
		interactButton.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		interactButton.OffsetTop = -96;
		interactButton.OffsetBottom = -36;
		interactButton.OffsetLeft = -190;
		interactButton.OffsetRight = 190;
		hud.AddChild(interactButton);
	}

	// O dock do Inventário no canto de baixo, igual ao do Lobby: o botão e o nome do Papel.
	Dock BuildDock()
	{
		var built = new Dock();
		built.SetRole(ctx.RoleName);
		built.InventoryRequested += () => ctx.OpenInventory?.Invoke();
		hud.AddChild(built);
		return built;
	}

	public void RefreshDock() => dock.SetNewCount(ctx.NewInventoryItems?.Invoke() ?? 0);

	public override void _Process(double delta) => dock.Visible = !InCombat();

	// Aviso curto das moedas ("+3 moedas"): some sozinho e nunca bloqueia o jogo nem o combate.
	void BuildCoinToast()
	{
		coinToast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		coinToast.OffsetLeft = -150;
		coinToast.OffsetRight = 150;
		coinToast.OffsetTop = 96;
		coinToast.Visible = false;
		coinToast.AddThemeFontSizeOverride("font_size", 26);
		coinToast.AddThemeColorOverride("font_color", Color.FromHtml("#FFD54F"));
		UiKit.Outline(coinToast);
		hud.AddChild(coinToast);
	}

	public void ShowCoins(string text)
	{
		coinToast.Text = text;
		coinToast.Visible = true;
		var mine = ++toastEpoch;
		GetTree().CreateTimer(CoinToastSeconds, true, false, true).Timeout += () =>
		{
			if (mine == toastEpoch && IsInstanceValid(coinToast)) coinToast.Visible = false;
		};
	}

	// Texto na tela e na voz; some sozinho, e um recado novo nunca é apagado pelo temporizador de um antigo.
	public void Say(string text)
	{
		message.Text = text;
		ctx.Voice.Say(text, interrupt: true);
		var mine = ++messageEpoch;
		GetTree().CreateTimer(MessageSeconds, true, false, true).Timeout += () =>
		{
			if (mine == messageEpoch && IsInstanceValid(message)) message.Text = "";
		};
	}

	// O botão de interação mostra a ação da porta ou da escada ao alcance; sem texto, some.
	public void ShowPrompt(string? text)
	{
		interactButton.Visible = text is not null;
		if (text is not null) interactButton.Text = text;
	}

	public void RefreshEnergy() => Energy.Set(expedition.Energy.Current, expedition.Energy.Max);

	public void BuildMap()
	{
		map?.QueueFree();
		map = new FloorMap(expedition.Floor, new Godot.Vector2(272, 250)) { Source = expedition };
		map.RoomPointed += room => RoomPointed?.Invoke(room);
		mapBox.AddChild(map);
		mapBox.MoveChild(map, 0);
	}

	public void Refresh(string title, bool floorComplete)
	{
		RefreshDock();
		header.Text = title;
		card.Set(ctx.Nickname, ctx.RoleName, ctx.Progress.Mastery.Of(ctx.ProfileId, ctx.Discipline));
		var progress = expedition.ProgressOf(expedition.Floor.Number);
		floorBar.MaxValue = progress.Total;
		floorBar.Value = progress.Resolved;
		floorLabel.Text = $"{progress.Resolved} de {progress.Total} Salas resolvidas" + (floorComplete ? "  ★ Andar completo" : "");
		if (map is null) return;
		map.Here = expedition.CurrentRoom;
		map.Cleared = new HashSet<int>(expedition.Cleared);
		map.Complete = floorComplete;
		map.Refresh();
	}

	public void Close()
	{
		hud.QueueFree();
		Energy.QueueFree();
	}
}
