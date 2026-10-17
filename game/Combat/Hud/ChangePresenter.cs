using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// "Pagar e dar troco" na HUD: a criança toca nas moedas e notas do Real para montar o valor e confere no fim. Não há traço: cada peça
// entra direto, e o número de peças é livre, então a conferência é um botão. O troco é calculado pelo jogo, e a dica nunca custa.
sealed class ChangePresenter(ChangeBattle battle, CreatureWords creature, Action<ChangeResult> checkedChange) : ICombatPresenter
{
	string suggestion = "";

	public string Heading => battle.Task == MoneyTask.Pay
		? $"{creature.Title} · Preço: {MoneyCatalog.Format(battle.Price)}"
		: $"{creature.Title} · Preço: {MoneyCatalog.Format(battle.Price)} · Você deu: {MoneyCatalog.Format(battle.Given)}";

	public string Prompt => battle.Mission.Prompt;

	// O que está na mesa, peça por peça, sem a soma: somar é com a criança.
	public string Assembled => battle.Table.Count == 0 ? "Toque nas moedas e notas para montar o valor." : string.Join(" + ", battle.Table.Select(d => d.Name));

	public IReadOnlyList<HudPiece> Pieces =>
		[.. battle.Pieces.Select((piece, i) => new HudPiece(Label(piece), piece.Name, !battle.Remaining.Equals(0), Highlighted: suggestion == piece.Name))];

	public bool Composing => battle.Table.Count > 0;

	public bool Resolved => battle.Remaining == 0;

	public string ResolvedText => "O valor já está certo. Espere o próximo.";

	public bool Complete => false;

	public bool TypesLetters => false;

	public string KeyHelp => "Teclado: 1 a 9 escolhem a peça, Backspace desfaz e Enter confere.";

	public string? ExtraLabel => "Sugerir peça";

	public string? ConfirmLabel => "Conferir";

	public bool CanConfirm => battle.Table.Count > 0;

	public string? TraceText(int piece) => null;

	public string Announce(int piece) => battle.Pieces[piece].Name;

	public string TraceLabel(int piece) => battle.Pieces[piece].Name;

	public void Place(int piece)
	{
		suggestion = "";
		battle.Add(piece);
	}

	public bool Undo()
	{
		suggestion = "";
		return battle.Undo();
	}

	public void Cancel()
	{
		suggestion = "";
		battle.Clear();
	}

	public bool Attack(out string? error)
	{
		error = null;
		suggestion = "";
		checkedChange(battle.Confirm());
		return true;
	}

	public int? PieceForKey(InputEventKey key)
	{
		var index = (int)key.PhysicalKeycode - (int)Key.Key1;
		return index >= 0 && index < Math.Min(9, battle.Pieces.Count) ? index : null;
	}

	// A sugestão destaca a próxima peça com forma e texto; se já passou do valor, manda desfazer.
	public string? Extra()
	{
		var (piece, undo) = battle.Suggest();
		suggestion = piece is { } i ? battle.Pieces[i].Name : "";
		return undo ? "Passou do valor. Desfaça a última peça com o botão Apagar." : $"Tente {battle.Pieces[piece!.Value].Name}.";
	}

	// Moeda ou nota e o valor, em texto: o tipo nunca depende só do desenho nem da cor.
	static string Label(Denomination piece) => $"{piece.Name}\n{piece.Kind}";
}
