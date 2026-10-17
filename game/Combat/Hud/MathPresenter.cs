using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// A Matemática na HUD: a bolsa de números e os sinais. Cada peça são duas ações (escolher e traçar), e o terceiro traço,
// número sinal número, valida a conta e o golpe sai na hora. Quem soma é a criança: o total só aparece depois do golpe.
sealed class MathPresenter(TrainingBattle battle, CombatRules rules, CreatureWords creature, Action<AttackResult> attacked) : ICombatPresenter
{
	int HandSize => battle.Bag.Hand.Count;

	public string Heading => $"{creature.Title} · Escudo: {battle.Shield.Remaining}";

	public string Prompt => battle.Mission.Prompt;

	public string Assembled
	{
		get
		{
			var tokens = battle.Staff.Tokens;
			return tokens.Count == 0 ? "Monte uma conta: número, sinal e número." : string.Join(" ", tokens) + (battle.Staff.IsComplete ? "" : " ?");
		}
	}

	public IReadOnlyList<HudPiece> Pieces => [.. Numbers(), .. Signs()];

	public bool Composing => battle.Staff.Tokens.Count > 0;

	public bool Resolved => battle.Shield.IsDissolved;

	public string ResolvedText => "A névoa está sumindo. Espere o novo escudo.";

	public bool Complete => battle.Staff.IsComplete;

	public bool TypesLetters => false;

	public string KeyHelp => $"Teclado: 1 a {HandSize} escolhem o número, + e - escolhem o sinal, depois digite o que está pedido. Backspace apaga.";

	public string? ExtraLabel => null;

	public string? TraceText(int piece) => piece < HandSize ? battle.Bag.Hand[piece]?.ToString(CultureInfo.InvariantCulture) : rules.Operators[piece - HandSize];

	public string Announce(int piece) =>
		piece < HandSize ? $"Desenhe o número {battle.Bag.Hand[piece]}." : $"Desenhe o sinal de {(rules.Operators[piece - HandSize] == "+" ? "mais" : "menos")}.";

	public string TraceLabel(int piece) => $"Desenhe o {TraceText(piece)}";

	public void Place(int piece)
	{
		if (piece < HandSize) battle.Staff.Place(piece);
		else battle.Staff.PlaceOperator(rules.Operators[piece - HandSize]);
	}

	public bool Undo() => battle.Staff.Undo();

	public void Cancel() => battle.Staff.Cancel();

	public bool Attack(out string? error)
	{
		var attack = battle.Confirm();
		error = attack.Error;
		if (!attack.IsOk) return false;
		attacked(attack.Value!);
		return true;
	}

	public int? PieceForKey(InputEventKey key)
	{
		var digit = (int)key.PhysicalKeycode - (int)Key.Key1;
		if (digit >= 0 && digit < HandSize) return digit;
		var sign = key.PhysicalKeycode switch { Key.Plus or Key.KpAdd => "+", Key.Minus or Key.KpSubtract => "-", _ => null };
		var at = sign is null ? -1 : rules.Operators.ToList().IndexOf(sign);
		return at >= 0 ? HandSize + at : null;
	}

	public string? Extra() => null;

	// O numeral e os pontos aparecem juntos: a quantidade nunca depende só do símbolo.
	IEnumerable<HudPiece> Numbers() =>
		Enumerable.Range(0, HandSize).Select(slot => battle.Bag.Hand[slot] is { } n
			? new HudPiece(n.ToString(CultureInfo.InvariantCulture), $"{n}", battle.Staff.CanPlaceNumber, n)
			: new HudPiece(battle.Bag.IsHidden(slot) ? "?" : "", "peça escondida", false));

	IEnumerable<HudPiece> Signs() =>
		rules.Operators.Select(sign => new HudPiece(sign, sign == "+" ? "mais" : "menos", battle.Staff.CanPlaceOperator));
}
