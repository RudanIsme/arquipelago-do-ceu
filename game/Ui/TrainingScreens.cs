using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// Antes de treinar, a criança escolhe o Papel, o Nível (só os liberados) e a dificuldade.
// `tryStart` devolve um aviso escrito se não der para treinar assim (a tela fica aberta) e null quando o treino começou.
public partial class TrainingSetupScreen : OverlayScreen
{
	static readonly string[] DifficultyNames = ["Bem fácil", "Fácil", "Médio", "Difícil", "Muito difícil"];

	readonly IReadOnlyList<(string Id, string Role)> roles;
	readonly IReadOnlyList<int> levels;
	readonly Func<TrainingSetup, string?> tryStart;
	string discipline;
	int level = 1;
	int difficulty = 1;
	Label message = null!;

	public TrainingSetupScreen(IReadOnlyList<(string Id, string Role)> roles, IReadOnlyList<int> levels, Func<TrainingSetup, string?> tryStart)
	{
		this.roles = roles;
		discipline = roles[0].Id;
		this.levels = levels;
		this.tryStart = tryStart;
	}

	public override void _Ready()
	{
		base._Ready();
		var box = UiKit.Card(this, "Treinar com o Boneco", out var footer);
		box.AddChild(UiKit.Hint("Treinar não conta para o progresso. É só para praticar, sem pressa."));

		box.AddChild(UiKit.Hint("Papel"));
		box.AddChild(Choices(roles, discipline, id => discipline = id, 260));

		box.AddChild(UiKit.Hint("Nível"));
		var levelChoices = new List<(int, string)>();
		foreach (var year in levels) levelChoices.Add((year, $"Nível {year}"));
		box.AddChild(Choices(levelChoices, level, id => level = id, 200));

		box.AddChild(UiKit.Hint("Dificuldade"));
		var difficultyChoices = new List<(int, string)>();
		for (var d = 1; d <= TrainingSetup.LastDifficulty; d++) difficultyChoices.Add((d, $"{d} · {DifficultyNames[d - 1]}"));
		box.AddChild(Choices(difficultyChoices, difficulty, id => difficulty = id, 260));

		message = UiKit.Hint("");
		footer.AddChild(message);
		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		actions.AddThemeConstantOverride("separation", 12);
		actions.AddChild(UiKit.BigButton("Começar", Start, 280));
		actions.AddChild(UiKit.BigButton("Fechar", Close, 280));
		footer.AddChild(actions);
	}

	void Start()
	{
		var error = tryStart(new TrainingSetup(discipline, level, difficulty));
		if (error is null) Close();
		else message.Text = error;
	}

	// Uma fileira de botões de escolha única; o escolhido fica marcado em palavras e forma, não só em cor.
	static Control Choices<T>(IReadOnlyList<(T Id, string Name)> options, T current, Action<T> onChosen, int width) where T : notnull
	{
		var row = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(420, 0) };
		row.AddThemeConstantOverride("h_separation", 10);
		row.AddThemeConstantOverride("v_separation", 10);
		var group = new ButtonGroup();
		foreach (var (id, name) in options)
		{
			var chosen = id;
			var button = UiKit.BigButton(name, () => onChosen(chosen), width);
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = EqualityComparer<T>.Default.Equals(id, current);
			row.AddChild(button);
		}
		return row;
	}
}

// A Energia do treino no alto da tela: bolinhas cheias e vazias (forma) e o número por escrito, nunca só a cor.
public partial class EnergyView : PanelContainer
{
	readonly EnergyDots dots = new();
	readonly Label label = new();
	Button leave;

	public EnergyView(string leaveText = "Sair do treino")
	{
		MouseFilter = MouseFilterEnum.Stop;
		AddThemeStyleboxOverride("panel", UiKit.Surface());

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", UiKit.TextColor);
		row.AddChild(label);
		row.AddChild(dots);
		leave = UiKit.BigButton(leaveText, () => LeaveRequested?.Invoke(), 230);
		leave.FocusMode = FocusModeEnum.None;
		row.AddChild(leave);
		AddChild(row);

		SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize);

		OffsetLeft = 16;
		OffsetTop = 154;
	}

	public event Action? LeaveRequested;

	public void Set(int current, int max)
	{
		label.Text = $"Energia: {current} de {max}";
		dots.Show(current, max);
	}
}

sealed partial class EnergyDots : Control
{
	float Radius => 7f;
	float Gap => 4f;

	int current;
	int max = 1;

	public EnergyDots()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Radius * 2f, Radius * 2f);
	}

	public void Show(int newCurrent, int newMax)
	{
		current = newCurrent;
		max = newMax;
		CustomMinimumSize = new Vector2(max * (Radius * 2f + Gap), Radius * 2f + 4f);
		QueueRedraw();
	}

	// Energia que sobrou: raio cheio. Energia que falta: só o contorno do raio.
	public override void _Draw()
	{
		var gold = UiKit.TextColor;
		for (var i = 0; i < max; i++)
		{
			var center = new Vector2(Radius + i * (Radius * 2f + Gap), Size.Y / 2f);
			var bolt = Bolt(center);
			if (i < current) DrawColoredPolygon(bolt, gold);
			else DrawPolyline([.. bolt, bolt[0]], gold, 2.5f, true);
		}
	}

	Vector2[] Bolt(Vector2 c) =>
	[
		c + new Vector2(.35f, -1f) * Radius, c + new Vector2(-.7f, .15f) * Radius, c + new Vector2(-.05f, .15f) * Radius,
		c + new Vector2(-.35f, 1f) * Radius, c + new Vector2(.7f, -.15f) * Radius, c + new Vector2(.05f, -.15f) * Radius,
	];
}
