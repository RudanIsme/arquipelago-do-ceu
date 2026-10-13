using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

public enum BubblePhase { Waiting, Warning, Flying }

// O que aconteceu neste instante, para o Godot tocar o aviso, soltar a bolha ou mostrar o acerto.
public enum BubbleEvent { None, Warned, Launched, Hit, Missed }

// A bolha de névoa do Boneco: espera, avisa o caminho que vai fazer (a mira trava quando o aviso começa) e voa devagar
// em linha reta por ele, até sumir. Quem sai do caminho não é atingida. Velocidade, aviso e intervalo vêm de dados
// e não dependem do Nível: a ação nunca fica mais difícil com o ano escolar.
public sealed class BubbleAttack
{
	readonly BubbleRules rules;
	float clock;
	Vector2 direction = Vector2.UnitX;
	Vector2 origin;
	float flown;

	public BubbleAttack(BubbleRules rules) => this.rules = rules;

	public BubblePhase Phase { get; private set; } = BubblePhase.Waiting;

	public Vector2 Position { get; private set; }

	// Para onde a bolha vai: travada quando o aviso começa, para a criança ter o aviso inteiro para sair do caminho.
	public Vector2 Aim { get; private set; }

	public float WarningProgress => Phase == BubblePhase.Warning ? Math.Min(1f, clock / rules.WarningSeconds) : 0f;

	public BubbleEvent Update(float seconds, Vector2 from, Vector2 player)
	{
		clock += seconds;
		switch (Phase)
		{
			case BubblePhase.Waiting when clock >= rules.IntervalSeconds:
				Phase = BubblePhase.Warning;
				clock = 0f;
				Aim = player;
				return BubbleEvent.Warned;
			case BubblePhase.Warning:
				if (clock < rules.WarningSeconds) return BubbleEvent.None;
				Launch(from);
				return BubbleEvent.Launched;
			case BubblePhase.Flying:
				return Fly(seconds, player);
			default:
				return BubbleEvent.None;
		}
	}

	// Desfaz a bolha no ar (o Boneco foi vencido, a criança saiu do treino).
	public void Reset()
	{
		Phase = BubblePhase.Waiting;
		clock = 0f;
	}

	// O caminho da bolha a partir do Boneco: o mesmo que o aviso mostra e que o voo segue.
	public Vector2 DirectionFrom(Vector2 from)
	{
		var way = Aim - from;
		return way.LengthSquared() < 0.0001f ? Vector2.UnitX : Vector2.Normalize(way);
	}

	void Launch(Vector2 from)
	{
		direction = DirectionFrom(from);
		origin = from;
		Position = from;
		flown = 0f;
		Phase = BubblePhase.Flying;
	}

	BubbleEvent Fly(float seconds, Vector2 player)
	{
		flown += rules.Speed * seconds;
		Position = origin + direction * flown;
		if (Vector2.Distance(Position, player) <= rules.Radius) return Finish(BubbleEvent.Hit);
		return flown >= rules.Range ? Finish(BubbleEvent.Missed) : BubbleEvent.None;
	}

	BubbleEvent Finish(BubbleEvent result)
	{
		Phase = BubblePhase.Waiting;
		clock = 0f;
		return result;
	}
}
