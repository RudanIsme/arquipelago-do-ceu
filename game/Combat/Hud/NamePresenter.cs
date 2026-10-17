using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// O Escriba na HUD (sílabas, frase na ordem ou lacuna): a criança escolhe a peça e, nas sílabas, traça as letras uma de cada vez.
// Com todas as peças do nome postas, a última ação confere e golpeia, sem botão de conferir.
sealed class NamePresenter(NameBattle battle, Action<NameVerdict> checkedName) : ICombatPresenter
{
	public string Heading => battle.Kind switch { NameKind.Sentence => "Ponha as palavras na ordem", NameKind.Gap => "Complete a frase", _ => "Qual é o nome da criatura?" };

	public string Prompt => battle.Mission.Prompt;

	public string Assembled
	{
		get
		{
			var slots = Enumerable.Range(0, battle.Name.Count).Select(i => i < battle.Chosen.Count ? Label(battle.Pieces[battle.Chosen[i]]) : "__");
			return battle.Kind switch
			{
				NameKind.Sentence => string.Join(" ", slots),
				NameKind.Gap => $"{battle.Before}{slots.First()}{battle.After}",
				_ => string.Join("  ·  ", slots),
			};
		}
	}

	public IReadOnlyList<HudPiece> Pieces =>
		[.. battle.Pieces.Select((piece, i) => battle.IsHidden(i)
			? new HudPiece("?", "peça escondida", false)
			: new HudPiece(Label(piece), piece, !battle.Chosen.Contains(i) && battle.Chosen.Count < battle.Name.Count))];

	public bool Composing => battle.Chosen.Count > 0;

	public bool Resolved => battle.Remaining == 0;

	public string ResolvedText => "A criatura já foi libertada. Espere o novo nome.";

	public bool Complete => battle.IsComplete;

	public bool TypesLetters => false;

	public string KeyHelp => $"Teclado: 1 a {Math.Min(9, battle.Pieces.Count)} escolhem a peça e, nas sílabas, digite as letras para traçar. Backspace desfaz.";

	public string? ExtraLabel => null;

	// Sílabas se traçam letra por letra; palavras de frases entram direto: no 3º ano a criança já escreve, e o desafio é a ordem.
	public string? TraceText(int piece) => battle.Kind == NameKind.Syllables ? battle.Pieces[piece].ToUpperInvariant() : null;

	public string Announce(int piece) => battle.Kind == NameKind.Syllables ? $"Desenhe a sílaba {battle.Pieces[piece]}." : battle.Pieces[piece];

	public string TraceLabel(int piece) => $"Desenhe {battle.Pieces[piece].ToUpperInvariant()}";

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
