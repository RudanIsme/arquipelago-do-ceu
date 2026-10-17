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
	Action<bool> Composing,
	Action Hint,
	Action Read);

// A HUD de combate de cada Mecânica (Matemático, Escriba, palavra inteira) atrás de uma só porta: a mesma HUD, um apresentador por Mecânica.
public static class AnswerPanels
{
	public static CombatHud Create(CombatSession session, GameRules rules, VoiceService voice, Func<bool> inTargetRange, CreatureWords creature, AttackRhythm rhythm, Func<bool> reduceMotion, PanelEvents events)
	{
		var hud = new CombatHud(Presenter(session, creature, events), rules.Combat.Tracing, voice, rhythm, inTargetRange, creature, reduceMotion);
		hud.ComposingChanged += events.Composing;
		hud.HintRequested += events.Hint;
		hud.ReadRequested += events.Read;
		return hud;
	}

	static ICombatPresenter Presenter(CombatSession session, CreatureWords creature, PanelEvents events)
	{
		if (session.Math is { } math) return new MathPresenter(math, session.Rules, creature, events.Attacked);
		if (session.Name is { } name) return new NamePresenter(name, events.NameChecked);
		return new WordPresenter(session.Word!, events.WordChecked);
	}

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
