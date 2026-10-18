using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// O Escriba do 2º ano na HUD: a criança desenha a palavra em qualquer lugar da tela, em pedaços ou inteira; a palavra completa liberta.
// A Dica mostra o pontilhado das próximas letras e nunca custa Energia.
sealed class WordPresenter(WordBattle battle, Action<WordResult> checkedWord, bool guideAlways) : ICombatPresenter
{
	const int GuideLetters = 4;

	public bool FreeDraw => true;

	public string? Request => Assembled;

	public string DrawAllowed => "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

	public string? GuideText => Remainder;

	public bool GuideAlways => guideAlways;

	public string? HintGuide => Remainder;

	// As próximas letras da palavra, para o pontilhado caber na tela.
	string? Remainder => battle.Typed.Length >= battle.Word.Length ? null : battle.Word[battle.Typed.Length..].ToUpperInvariant()[..Math.Min(GuideLetters, battle.Word.Length - battle.Typed.Length)];

	public bool Write(string text, out string? message)
	{
		message = null;
		checkedWord(battle.Write(text));
		return true;
	}

	char? suggested;

	public string Heading => "Escreva o nome da criatura";

	public string Prompt => battle.Mission.Prompt;

	public string Assembled => string.Join(" ", Enumerable.Range(0, battle.Word.Length).Select(i => i < battle.Typed.Length ? char.ToUpperInvariant(battle.Typed[i]).ToString() : "_"));

	// A letra sugerida aparece entre colchetes e destacada: forma, texto e cor.
	public IReadOnlyList<HudPiece> Pieces =>
		[.. battle.Keys.Select(letter => battle.IsHidden(letter)
			? new HudPiece("?", "letra escondida", false)
			: new HudPiece(suggested == letter ? $"[{Upper(letter)}]" : Upper(letter), Upper(letter), battle.Typed.Length < battle.Word.Length, Highlighted: suggested == letter))];

	public bool Composing => battle.Typed.Length > 0;

	public bool Resolved => battle.Remaining == 0;

	public string ResolvedText => "A criatura já foi libertada. Espere o novo nome.";

	public bool Complete => battle.IsComplete;

	public bool TypesLetters => true;

	public string KeyHelp => "Desenhe as letras na tela, ou digite. Backspace apaga. Para andar, use as setas.";

	public string? ExtraLabel => null;


	public string Announce(int piece) => $"Desenhe a letra {Upper(battle.Keys[piece])}.";


	public void Place(int piece)
	{
		suggested = null;
		battle.Type(battle.Keys[piece]);
	}

	public bool Undo()
	{
		suggested = null;
		return battle.Backspace();
	}

	public void Cancel()
	{
		suggested = null;
		battle.Clear();
	}

	public bool Attack(out string? error)
	{
		error = null;
		var text = battle.Typed;
		battle.Clear();
		checkedWord(battle.Write(text));
		return true;
	}

	public int? PieceForKey(InputEventKey key)
	{
		if (key.Unicode == 0 || !char.IsLetter((char)key.Unicode)) return null;
		var at = battle.Keys.ToList().IndexOf(char.ToLowerInvariant((char)key.Unicode));
		return at >= 0 ? at : null;
	}

	public string? Extra()
	{
		var (letter, erase) = battle.Suggest();
		if (erase) return $"A letra {Upper(letter)} não está certa. Apague com o botão Apagar e tente de novo.";
		suggested = letter;
		return $"Tente a letra {Upper(letter)}.";
	}

	static string Upper(char letter) => char.ToUpperInvariant(letter).ToString();
}
