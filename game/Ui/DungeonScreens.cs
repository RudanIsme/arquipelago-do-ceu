using System;
using Godot;

namespace ArquipelagoDoCeu;

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
	readonly bool mathDone;
	readonly bool lettersDone;
	readonly Action start;

	public MasterScreen(int level, bool mathDone, bool lettersDone, Action start)
	{
		this.level = level;
		this.mathDone = mathDone;
		this.lettersDone = lettersDone;
		this.start = start;
	}

	public override void _Ready()
	{
		base._Ready();
		var open = mathDone && lettersDone;
		var box = UiKit.Card(this, "Desafio mestre", out var footer);
		box.AddChild(UiKit.Hint(open
			? $"As duas torres do Nível {level} estão concluídas! O Guardião do Nível mistura Matemática e Português. Vencendo, o Nível {level + 1} abre nas duas torres."
			: $"O portal só abre quando você vencer o Desafio épico do Nível {level} nas duas torres (jogando a torre ou pelo Atalho de Nível)."));
		box.AddChild(UiKit.Hint($"Torre dos Números: {(mathDone ? "★ concluída" : "ainda não")}"));
		box.AddChild(UiKit.Hint($"Torre das Letras: {(lettersDone ? "★ concluída" : "ainda não")}"));
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
		box.AddChild(UiKit.Hint($"Você venceu o Desafio épico do Nível {level} da {tower}. Parabéns!{item} Quer continuar explorando ou voltar ao Lobby?"));
		footer.AddChild(UiKit.BigButton("Continuar explorando", Close, 420));
		footer.AddChild(UiKit.BigButton("Voltar ao Lobby", () => { Close(); leave(); }, 420));
	}
}
