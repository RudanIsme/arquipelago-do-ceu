using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// O Escriba na HUD. Nas sílabas a criança desenha o nome em qualquer lugar da tela, uma sílaba por vez (BO, depois LA) ou inteiro;
// frase na ordem e lacuna seguem escolhendo a peça, e a última ação confere e golpeia, sem botão de conferir.
sealed class NamePresenter(NameBattle battle, Action<NameVerdict> checkedName, bool guideAlways) : ICombatPresenter
{
	// As sílabas se escrevem desenhando, uma por vez ou o nome inteiro; frases e lacunas seguem escolhendo a peça.
	public bool FreeDraw => battle.Kind == NameKind.Syllables;

	public string DrawAllowed => "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

	public string? GuideText => battle.NextSyllable?.ToUpperInvariant();

	public bool GuideAlways => guideAlways;

	public string? HintGuide => battle.NextSyllable?.ToUpperInvariant();

	public bool Write(string text, out string? message)
	{
		message = null;
		checkedName(battle.Write(text));
		return true;
	}

	public string Heading => battle.Kind switch { NameKind.Sentence => "Ponha as palavras na ordem", NameKind.Gap => "Complete a frase", _ => "Qual é o nome da criatura?" };

	public string Prompt => battle.Mission.Prompt;

	public string Assembled
	{
		get
		{
			if (FreeDraw) return DrawnSlots();
			var slots = Enumerable.Range(0, battle.Name.Count).Select(i => i < battle.Chosen.Count ? Label(battle.Pieces[battle.Chosen[i]]) : "__");
			return battle.Kind switch
			{
				NameKind.Sentence => string.Join(" ", slots),
				NameKind.Gap => $"{battle.Before}{slots.First()}{battle.After}",
				_ => string.Join("  ·  ", slots),
			};
		}
	}

	// Cada sílaba certa aparece escrita no lugar dela; as que faltam ficam em branco.
	string DrawnSlots()
	{
		var done = battle.Name.Count - battle.Remaining;
		return string.Join("  ·  ", battle.Name.Select((syllable, i) => i < done ? syllable.ToUpperInvariant() : "__"));
	}

	public IReadOnlyList<HudPiece> Pieces =>
		[.. battle.Pieces.Select((piece, i) => battle.IsHidden(i)
			? new HudPiece("?", "peça escondida", false)
			: new HudPiece(Label(piece), piece, !battle.Chosen.Contains(i) && battle.Chosen.Count < battle.Name.Count))];

	public bool Composing => battle.Chosen.Count > 0;

	public bool Resolved => battle.Remaining == 0;

	public string ResolvedText => "A criatura já foi libertada. Espere o novo nome.";

	public bool Complete => battle.IsComplete;

	public bool TypesLetters => FreeDraw;

	public string KeyHelp => FreeDraw ? "Desenhe as letras na tela, ou digite. Backspace apaga. Para andar, use as setas." : $"Teclado: 1 a {Math.Min(9, battle.Pieces.Count)} escolhem a peça. Backspace desfaz.";

	public string? ExtraLabel => null;


	public string Announce(int piece) => battle.Kind == NameKind.Syllables ? $"Desenhe a sílaba {battle.Pieces[piece]}." : battle.Pieces[piece];


	public void Place(int piece) => battle.Place(piece);

	public bool Undo() => battle.Undo();

	public void Cancel() => battle.UndoAll();

	// A luta diz o resultado em palavras ("certa", "errada"): a cor sozinha nunca diz.
	public bool Attack(out string? error)
	{
		error = null;
		checkedName(battle.Check());
		return true;
	}

	public int? PieceForKey(InputEventKey key)
	{
		var index = (int)key.PhysicalKeycode - (int)Key.Key1;
		return index >= 0 && index < Math.Min(9, battle.Pieces.Count) ? index : null;
	}

	public string? Extra() => null;

	// Sílabas vão em maiúscula, como na trilha; palavras de frases ficam como foram escritas, com a ortografia à vista.
	string Label(string piece) => battle.Kind == NameKind.Syllables ? piece.ToUpperInvariant() : piece;
}
