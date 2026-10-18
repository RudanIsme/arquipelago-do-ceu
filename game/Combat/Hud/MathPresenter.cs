using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// A Matemática na HUD: a criança desenha em qualquer lugar da tela. Com um resultado pedido, vale só uma conta que o dê; com uma conta
// pronta ("3 + 5 = ?"), vale só o número, sem sinais. Quem soma é a criança: o total só aparece depois do golpe.
sealed class MathPresenter(TrainingBattle battle, CombatRules rules, CreatureWords creature, Action<AttackResult> attacked) : ICombatPresenter
{
	public bool FreeDraw => true;

	public string DrawAllowed => DrawnMath.Allowed(battle.Ask, rules.MathLevel);

	// Resultado pedido: o número da criatura é o alvo e a criança faz a conta. Conta pronta: o número é a resposta e não aparece.
	public string? HintGuide => battle.Example(rules.MathLevel);

	public bool Write(string text, out string? message)
	{
		var attack = battle.Draw(text, rules.MathLevel);
		message = attack.Error;
		if (!attack.IsOk) return false;
		attacked(attack.Value!);
		return true;
	}

	int HandSize => battle.Bag.Hand.Count;

	public string Heading => battle.Ask == MathAsk.Answer ? creature.Title : $"{creature.Title} · Escudo: {battle.Shield.Remaining}";

	public string Prompt => battle.Mission.Prompt;

	public string Assembled => battle.Ask == MathAsk.Answer ? $"{battle.Question} = ?" : "Escreva uma conta que dê o resultado.";

	public IReadOnlyList<HudPiece> Pieces => [.. Numbers(), .. Signs()];

	public bool Composing => battle.Staff.Tokens.Count > 0;

	public bool Resolved => battle.Shield.IsDissolved;

	public string ResolvedText => "A névoa está sumindo. Espere o novo escudo.";

	public bool Complete => battle.Staff.IsComplete;

	public bool TypesLetters => false;

	public string KeyHelp => battle.Ask == MathAsk.Answer ? "Desenhe o número na tela, ou digite. Backspace apaga." : "Desenhe a conta na tela, ou digite números e sinais. Backspace apaga.";

	public string? ExtraLabel => null;


	public string Announce(int piece) =>
		piece < HandSize ? $"Desenhe o número {battle.Bag.Hand[piece]}." : $"Desenhe o sinal de {(rules.Operators[piece - HandSize] == "+" ? "mais" : "menos")}.";


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
