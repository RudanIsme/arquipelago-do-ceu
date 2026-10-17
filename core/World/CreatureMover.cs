using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.World;

public enum CreatureMood { Patrolling, Approaching, Returning }

// O movimento de uma criatura pela Sala: patrulha perto de casa, se aproxima devagar da criança, desiste e volta, e contorna o que
// não dá para pisar. Nunca é mais rápida que a criança, nunca persegue para sempre, nunca fica presa e, no Modo calmo, não se aproxima.
public sealed class CreatureMover
{
	const float ArrivalDistance = 0.15f;
	const float StuckSeconds = 2f;
	const float StepAhead = 0.05f;
	static readonly float[] Detours = [0f, 35f, -35f, 70f, -70f, 105f, -105f];

	readonly MovementRules rules;
	readonly Vector2 home;
	readonly Func<Vector2, bool> canStand;
	readonly Func<double> nextUnit;
	Vector2 target;
	float pause;
	float chasing;
	float cooldown;
	float stuck;

	public CreatureMover(MovementRules rules, Vector2 home, Func<Vector2, bool> canStand, Func<double> nextUnit)
	{
		this.rules = rules;
		this.home = home;
		this.canStand = canStand;
		this.nextUnit = nextUnit;
		Position = home;
		target = home;
	}

	public Vector2 Position { get; private set; }

	public CreatureMood Mood { get; private set; } = CreatureMood.Patrolling;

	// `engaged` é a criança estar lutando com ela: a criatura chega perto e fica, sem desistir nem sair andando.
	public void Tick(float seconds, Vector2 player, bool engaged, bool calm)
	{
		cooldown = Math.Max(0f, cooldown - seconds);
		var distance = Vector2.Distance(Position, player);
		if (engaged) Hold(seconds, player, distance);
		else if (Mood == CreatureMood.Approaching && distance > rules.ApproachRadius * 1.25f) GiveUp();
		else if (!calm && cooldown <= 0f && Mood != CreatureMood.Returning && distance <= rules.ApproachRadius) Approach(seconds, player, distance);
		else if (Mood == CreatureMood.Approaching) GiveUp();
		else if (Mood == CreatureMood.Returning) GoHome(seconds);
		else Patrol(seconds);
	}

	void Hold(float seconds, Vector2 player, float distance)
	{
		Mood = CreatureMood.Approaching;
		chasing = 0f;
		if (distance > rules.StopDistance) Step(player - Position, rules.ApproachSpeed, seconds);
	}

	void Approach(float seconds, Vector2 player, float distance)
	{
		Mood = CreatureMood.Approaching;
		chasing += seconds;
		if (chasing >= rules.GiveUpSeconds)
		{
			GiveUp();
			return;
		}
		if (distance > rules.StopDistance) Step(player - Position, rules.ApproachSpeed, seconds);
	}

	// Desistir nunca pune ninguém: a criatura só volta para casa e fica um tempo sem se aproximar.
	void GiveUp()
	{
		Mood = CreatureMood.Returning;
		chasing = 0f;
		cooldown = rules.CooldownSeconds;
		stuck = 0f;
	}

	void GoHome(float seconds)
	{
		if (Vector2.Distance(Position, home) <= ArrivalDistance)
		{
			Mood = CreatureMood.Patrolling;
			pause = rules.PauseSeconds;
			return;
		}
		if (Step(home - Position, rules.PatrolSpeed, seconds) || stuck < StuckSeconds) return;
		// Presa no caminho de volta: o que importa é nunca ficar presa, então ela reaparece em casa.
		Position = home;
		stuck = 0f;
	}

	void Patrol(float seconds)
	{
		if (pause > 0f)
		{
			pause -= seconds;
			return;
		}
		if (Vector2.Distance(Position, target) <= ArrivalDistance || stuck >= StuckSeconds) PickTarget();
		else Step(target - Position, rules.PatrolSpeed, seconds);
	}

	void PickTarget()
	{
		pause = rules.PauseSeconds;
		stuck = 0f;
		var angle = (float)(nextUnit() * MathF.Tau);
		var reach = (float)nextUnit() * rules.PatrolRadius;
		var next = home + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
		target = canStand(next) ? next : home;
	}

	// Anda na direção pedida; se há obstáculo, contorna por um desvio. Devolve falso quando nenhum caminho deixa andar.
	bool Step(Vector2 way, float speed, float seconds)
	{
		if (way.LengthSquared() < 0.0001f) return true;
		var direction = Vector2.Normalize(way);
		var length = speed * seconds;
		foreach (var degrees in Detours)
		{
			var turned = Rotate(direction, degrees * MathF.PI / 180f);
			var next = Position + turned * length;
			if (!canStand(next) || !canStand(Position + turned * (length + StepAhead))) continue;
			Position = next;
			stuck = 0f;
			return true;
		}
		stuck += seconds;
		return false;
	}

	static Vector2 Rotate(Vector2 v, float radians) =>
		new(v.X * MathF.Cos(radians) - v.Y * MathF.Sin(radians), v.X * MathF.Sin(radians) + v.Y * MathF.Cos(radians));
}
