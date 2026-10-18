using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

// Ao voltar da Expedição: o total de moedas com o "+X" e o número subindo até o saldo novo. Enter ou um toque pulam a
// animação (e, depois dela, fecham); com "reduzir animações" o valor aparece direto.
public partial class CoinsArrivalScreen : OverlayScreen
{
	readonly int before;
	readonly int after;
	readonly bool reduceMotion;
	readonly Action<string> speak;
	readonly Label total = new() { HorizontalAlignment = HorizontalAlignment.Center, Name = "Moedas" };
	Tween? tween;
	bool counting;

	public CoinsArrivalScreen(int before, int after, bool reduceMotion, Action<string> speak)
	{
		this.before = before;
		this.after = after;
		this.reduceMotion = reduceMotion;
		this.speak = speak;
	}

	public static string Spoken(int gained, int now) => $"Você ganhou {(gained == 1 ? "1 moeda" : $"{gained} moedas")}, agora tem {now}.";

	public string Shown => total.Text;

	public override void _Ready()
	{
		base._Ready();
		var gained = after - before;
		var box = UiKit.Card(this, "De volta ao Lobby", out var footer);
		total.AddThemeFontSizeOverride("font_size", 56);
		total.AddThemeColorOverride("font_color", UiKit.TextColor);
		box.AddChild(UiKit.Hint("Suas moedas"));
		box.AddChild(total);
		var plus = UiKit.Hint($"+{gained}");
		plus.HorizontalAlignment = HorizontalAlignment.Center;
		plus.AddThemeFontSizeOverride("font_size", 32);
		box.AddChild(plus);
		footer.AddChild(UiKit.BigButton("Continuar", Close, 300));
		speak(Spoken(gained, after));

		if (reduceMotion || gained <= 0)
		{
			total.Text = after.ToString(System.Globalization.CultureInfo.InvariantCulture);
			return;
		}
		total.Text = before.ToString(System.Globalization.CultureInfo.InvariantCulture);
		counting = true;
		tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(value => total.Text = Mathf.RoundToInt(value).ToString(System.Globalization.CultureInfo.InvariantCulture)), (float)before, (float)after, Mathf.Min(2f, 0.4f + gained * 0.05f));
		tween.Finished += () => counting = false;
	}

	void Skip()
	{
		tween?.Kill();
		counting = false;
		total.Text = after.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } && counting) Skip();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (counting && @event.IsActionPressed("ui_accept"))
		{
			GetViewport().SetInputAsHandled();
			Skip();
			return;
		}
		base._UnhandledInput(@event);
	}
}

// A oferta do Atalho de Nível, na porta da torre: a criança escolhe com calma, e o texto diz que só há uma tentativa.
public partial class ShortcutOfferScreen : OverlayScreen
{
	readonly string tower;
	readonly int level;
	readonly int challenges;
	readonly Action tryShortcut;
	readonly Action enterNormally;

	public ShortcutOfferScreen(string tower, int level, int challenges, Action tryShortcut, Action enterNormally)
	{
		this.tower = tower;
		this.level = level;
		this.challenges = challenges;
		this.tryShortcut = tryShortcut;
		this.enterNormally = enterNormally;
	}

	public override void _Ready()
	{
		base._Ready();
		var box = UiKit.Card(this, "Atalho de Nível", out var footer);
		box.AddChild(UiKit.Hint($"Quer pular o Nível {level} da {tower}? Você enfrenta direto o Desafio épico, com {challenges} desafios sorteados de todo o Nível. " +
			"Se acertar todos, o Nível conta como concluído. Errar não custa Energia, mas cada Nível só deixa tentar o Atalho uma vez: depois você joga o Nível normalmente."));
		footer.AddChild(UiKit.BigButton("Tentar o Atalho", () => { Close(); tryShortcut(); }, 420));
		footer.AddChild(UiKit.BigButton("Entrar na torre", () => { Close(); enterNormally(); }, 420));
	}

	// Esc é entrar normalmente: o Atalho nunca é gasto sem a criança querer.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		Close();
		enterNormally();
	}
}

// O portal do Desafio mestre: diz o que falta para abrir e, aberto, deixa começar. Nunca bloqueia nada: é só um convite.
public partial class MasterScreen : OverlayScreen
{
	readonly int level;
	readonly IReadOnlyList<(string Name, bool Done)> towers;
	readonly Action start;

	public MasterScreen(int level, IReadOnlyList<(string Name, bool Done)> towers, Action start)
	{
		this.level = level;
		this.towers = towers;
		this.start = start;
	}

	public override void _Ready()
	{
		base._Ready();
		var open = towers.All(t => t.Done);
		var box = UiKit.Card(this, "Desafio mestre", out var footer);
		box.AddChild(UiKit.Hint(open
			? $"Todas as torres do Nível {level} estão concluídas! O Guardião do Nível mistura as matérias de todas elas. Vencendo, o Nível {level + 1} abre nas torres."
			: $"O portal só abre quando você vencer o Desafio épico do Nível {level} em todas as torres (jogando a torre ou pelo Atalho de Nível)."));
		foreach (var (name, done) in towers) box.AddChild(UiKit.Hint($"{name}: {(done ? "★ concluída" : "ainda não")}"));
		if (open) footer.AddChild(UiKit.BigButton("Começar o Desafio mestre", () => { Close(); start(); }, 420));
		footer.AddChild(UiKit.BigButton("Fechar", Close, 420));
	}
}

// O que a criança escolhe ao concluir o Desafio épico: continuar na torre ou voltar ao Lobby.
public partial class LevelDoneScreen : OverlayScreen
{
	readonly string tower;
	readonly int level;
	readonly bool gotItem;
	readonly Action leave;

	public LevelDoneScreen(string tower, int level, bool gotItem, Action leave)
	{
		this.tower = tower;
		this.level = level;
		this.gotItem = gotItem;
		this.leave = leave;
	}

	public override void _Ready()
	{
		base._Ready();
		var box = UiKit.Card(this, "Nível concluído!", out var footer);
		var item = gotItem ? " Você também ganhou o Item de troca desta torre." : "";
		box.AddChild(UiKit.Hint($"Você venceu o Desafio épico do Nível {level} da {tower}. Parabéns!{item} Quer continuar explorando (a escada lateral desta Sala leva aos Andares profundos) ou voltar ao Lobby?"));
		footer.AddChild(UiKit.BigButton("Continuar explorando", Close, 420));
		footer.AddChild(UiKit.BigButton("Voltar ao Lobby", () => { Close(); leave(); }, 420));
	}
}
