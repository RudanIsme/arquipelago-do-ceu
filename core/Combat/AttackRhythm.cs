using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// O relógio de ataque da criatura (na escala de CombatTime: lento ao montar a resposta, parado no Modo calmo)
// e a recarga do jogador, que corre em tempo real. Demorar não pune: o golpe só chega mais devagar.
public sealed class AttackRhythm
{
	readonly CombatRules rules;
	readonly CombatTime time;
	float recharge;
	string kind = RhythmRules.DefaultKind;

	public AttackRhythm(CombatRules rules, CombatTime time)
	{
		this.rules = rules;
		this.time = time;
		Bubble = new BubbleAttack(rules.Rhythm.BubbleFor(RhythmRules.DefaultKind, rules.Bubble));
	}

	public BubbleAttack Bubble { get; private set; }

	public bool PlayerReady => recharge <= 0f;

	// O quanto da recarga já passou, de 0 a 1, para a tela mostrar a espera.
	public float RechargeProgress => rules.Rhythm.PlayerRechargeSeconds <= 0f ? 1f : Math.Clamp(1f - recharge / rules.Rhythm.PlayerRechargeSeconds, 0f, 1f);

	// Trocar de criatura troca o ritmo e recomeça o relógio; pedir a mesma de novo não faz nada.
	public void UseCreature(string next)
	{
		if (next == kind) return;
		kind = next;
		Bubble = new BubbleAttack(rules.Rhythm.BubbleFor(next, rules.Bubble));
	}

	public void PlayerAnswered() => recharge = rules.Rhythm.PlayerRechargeSeconds;

	// Uma luta nova começa sem recarga e sem bolha no ar.
	public void Reset()
	{
		recharge = 0f;
		Bubble.Reset();
	}

	// `realSeconds` é o tempo de relógio do quadro. Parado (Modo calmo, Puzzle), a criatura só reage a erros e a bolha no ar some.
	public BubbleEvent Tick(float realSeconds, Vector2 from, Vector2 player, bool stopped)
	{
		recharge = Math.Max(0f, recharge - realSeconds);
		if (stopped)
		{
			Bubble.Reset();
			return BubbleEvent.None;
		}
		return Bubble.Update(realSeconds * time.Scale, from, player);
	}
}
