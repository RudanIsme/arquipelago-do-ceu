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
	// Na conta pronta a mochila é um teclado de dígitos: a resposta (7, 12) nem sempre sai das peças da mão.
	readonly List<int> digits = [];
	bool Answering => battle.Ask == MathAsk.Answer;

	public bool FreeDraw => true;

	public string? Request => battle.Ask == MathAsk.Answer ? $"{battle.Question} = ?" : $"Faça {battle.StartingShield}";

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

	public string Assembled => Answering ? $"{battle.Question} = {(digits.Count == 0 ? "?" : string.Concat(digits))}" : "Escreva uma conta que dê o resultado.";

	public IReadOnlyList<HudPiece> Pieces => Answering ? [.. Enumerable.Range(0, 10).Select(d => new HudPiece(d.ToString(CultureInfo.InvariantCulture), $"{d}", digits.Count < MaxDigits, d))] : [.. Numbers(), .. Signs()];

	const int MaxDigits = 4;

	public bool Composing => Answering ? digits.Count > 0 : battle.Staff.Tokens.Count > 0;

	public bool Resolved => battle.Shield.IsDissolved;

	public string ResolvedText => "A névoa está sumindo. Espere o novo escudo.";

	// Uma conta pronta se responde com um número só; um resultado pedido, com número, sinal e número.
	public bool Complete => Answering ? digits.Count >= battle.StartingShield.ToString(CultureInfo.InvariantCulture).Length : battle.Staff.IsComplete;

	public bool TypesLetters => false;

	public string KeyHelp => battle.Ask == MathAsk.Answer ? "Desenhe o número na tela, ou digite. Backspace apaga." : "Desenhe a conta na tela, ou digite números e sinais. Backspace apaga.";

	public string? ExtraLabel => null;


	public string Announce(int piece) =>
		Answering ? $"Número {piece}." : piece < HandSize ? $"Desenhe o número {battle.Bag.Hand[piece]}." : $"Desenhe o sinal de {(rules.Operators[piece - HandSize] == "+" ? "mais" : "menos")}.";


	public void Place(int piece)
	{
		if (Answering) digits.Add(piece);
		else if (piece < HandSize) battle.Staff.Place(piece);
		else battle.Staff.PlaceOperator(rules.Operators[piece - HandSize]);
	}

	public bool Undo()
	{
		if (!Answering) return battle.Staff.Undo();
		if (digits.Count == 0) return false;
		digits.RemoveAt(digits.Count - 1);
		return true;
	}

	public void Cancel()
	{
		digits.Clear();
		battle.Staff.Cancel();
	}

	public bool Attack(out string? error)
	{
		// A conta montada com peças segue as mesmas regras da desenhada.
		var text = Answering ? string.Concat(digits) : string.Concat(battle.Staff.Tokens);
		Cancel();
		return Write(text, out error);
	}

	public int? PieceForKey(InputEventKey key)
	{
		if (Answering)
		{
			var d = (int)key.PhysicalKeycode - (int)Key.Key0;
			return d is >= 0 and <= 9 ? d : null;
		}
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
		rules.Operators.Where(_ => battle.Ask == MathAsk.Result).Select(sign => new HudPiece(sign, sign == "+" ? "mais" : "menos", battle.Staff.CanPlaceOperator));
}
