using System;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;

namespace ArquipelagoDoCeu;

// O que a criança faz no painel de resposta, para a luta reagir; cada Mecânica usa o seu evento.
public sealed record PanelEvents(
	Action<AttackResult> Attacked,
	Action<NameVerdict> NameChecked,
	Action<WordResult> WordChecked,
	Action<bool> Composing,
	Action Hint,
	Action Read);

// Os painéis de resposta de cada Mecânica (Matemático, Escriba, palavra inteira) atrás de uma só porta.
public static class AnswerPanels
{
	public static OverlayScreen Create(CombatSession session, GameRules rules, VoiceService voice, Func<bool> inTargetRange, CreatureWords creature, bool allowPull, PanelEvents events)
	{
		if (session.Math is { } math)
		{
			var staff = new StaffPanel(math, session.Rules, voice, inTargetRange, creature);
			staff.Attacked += events.Attacked;
			staff.ComposingChanged += events.Composing;
			staff.HintRequested += events.Hint;
			staff.ReadRequested += events.Read;
			return staff;
		}
		if (session.Name is { } nameBattle)
		{
			var name = new NamePanel(nameBattle, rules.Combat.Tracing, voice, allowPull);
			name.Checked += events.NameChecked;
			name.ComposingChanged += events.Composing;
			name.HintRequested += events.Hint;
			name.ReadRequested += events.Read;
			return name;
		}
		var word = new WordPanel(session.Word!, voice);
		word.Checked += events.WordChecked;
		word.ComposingChanged += events.Composing;
		word.HintRequested += events.Hint;
		word.ReadRequested += events.Read;
		return word;
	}

	// Devolve falso se não há painel aberto, para quem chama falar pela voz.
	public static bool Tell(OverlayScreen? panel, string text)
	{
		switch (panel)
		{
			case StaffPanel staff: staff.Tell(text); return true;
			case NamePanel name: name.Tell(text); return true;
			case WordPanel word: word.Tell(text); return true;
			default: return false;
		}
	}

	public static void Refresh(OverlayScreen? panel)
	{
		switch (panel)
		{
			case StaffPanel staff: staff.Refresh(); break;
			case NamePanel name: name.Reload(); break;
			case WordPanel word: word.Reload(); break;
		}
	}

	public static void Abandon(OverlayScreen? panel)
	{
		switch (panel)
		{
			case StaffPanel staff: staff.Abandon(); break;
			case NamePanel name: name.Abandon(); break;
			case WordPanel word: word.Abandon(); break;
		}
	}
}
