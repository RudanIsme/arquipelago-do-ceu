using System;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;

namespace ArquipelagoDoCeu;

// O que a criança faz na HUD de combate, para a luta reagir; cada Mecânica usa o seu evento.
public sealed record PanelEvents(
	Action<AttackResult> Attacked,
	Action<NameVerdict> NameChecked,
	Action<WordResult> WordChecked,
	Action<ChangeResult> ChangeChecked,
	Action<bool> Composing,
	Action<Godot.Vector2> Tapped,
	Action Hint,
	Action Read);

// A HUD de combate de cada Mecânica (Matemático, Escriba, palavra inteira) atrás de uma só porta: a mesma HUD, um apresentador por Mecânica.
public static class AnswerPanels
{
	public static CombatHud Create(CombatSession session, GameRules rules, VoiceService voice, Func<bool> inTargetRange, CreatureWords creature, AttackRhythm rhythm, PanelEvents events)
	{
		var hud = new CombatHud(Presenter(session, rules, creature, events), rules.Combat, voice, rhythm, inTargetRange, creature);
		hud.Tapped += events.Tapped;
		hud.ComposingChanged += events.Composing;
		hud.HintRequested += events.Hint;
		hud.ReadRequested += events.Read;
		return hud;
	}

	static ICombatPresenter Presenter(CombatSession session, GameRules rules, CreatureWords creature, PanelEvents events)
	{
		var guideAlways = rules.Combat.FreeDraw.GuideAlwaysShown(session.Setup.Year);
		if (session.Math is { } math) return new MathPresenter(math, session.Rules, creature, events.Attacked);
		if (session.Name is { } name) return new NamePresenter(name, events.NameChecked, guideAlways);
		if (session.Change is { } change) return new ChangePresenter(change, creature, events.ChangeChecked);
		return new WordPresenter(session.Word!, events.WordChecked, guideAlways);
	}

	// A travessura em palavras. No desenho livre não há peças para embaralhar ou esconder: o Boneco só faz graça.
	public static string PrankText(OverlayScreen? panel, CreatureWords creature, Prank? prank, float hiddenSeconds) =>
		panel is CombatHud { Presenter.FreeDraw: true } ? $"{creature.Start} fez uma travessura."
		: prank == Prank.Hidden ? $"{creature.Start} fez uma travessura e escondeu uma peça por {hiddenSeconds:0} segundos."
		: $"{creature.Start} fez uma travessura e embaralhou as peças.";

	// Devolve falso se não há HUD aberta, para quem chama falar pela voz.
	public static bool Tell(OverlayScreen? panel, string text)
	{
		if (panel is not CombatHud hud) return false;
		hud.Tell(text);
		return true;
	}

	public static void Refresh(OverlayScreen? panel) => (panel as CombatHud)?.Refresh();

	public static void Abandon(OverlayScreen? panel) => (panel as CombatHud)?.Abandon();
}
