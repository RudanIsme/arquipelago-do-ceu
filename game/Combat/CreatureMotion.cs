using System;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Leva o movimento da criatura (CreatureMover, do core) para a cena: a criatura patrulha a Sala, se aproxima devagar da criança e
// contorna as rochas; aqui só se diz onde ela pode pisar e se move o corpo. Sem violência: aproximar-se não machuca.
public sealed class CreatureMotion
{
	// O chão em volta do pé da criatura em casa (onde o Lobby marca o objeto como ocupado) sempre vale.
	const float HomeClearance = 0.7f;

	readonly CreatureMover mover;
	readonly TrainingDummy dummy;
	readonly Vector2 home;

	public CreatureMotion(MovementRules rules, ICombatStage stage, TrainingDummy dummy, Func<double> nextUnit)
	{
		this.dummy = dummy;
		var (ground, _) = stage.CreatureSpot;
		home = new Vector2(ground.X, ground.Z);
		mover = new CreatureMover(rules, home, point => Vector2.Distance(point, home) < HomeClearance || stage.CanStand(point), nextUnit);
	}

	// Onde a criatura está agora no chão (x, z).
	public Vector2 Ground => mover.Position;

	// `moves` falso é a porta de um Puzzle: ela não sai do lugar.
	public void Tick(float seconds, Vector2 player, bool moves, bool engaged, bool calm)
	{
		if (!moves) return;
		mover.Tick(seconds, player, engaged, calm);
		dummy.MoveTo(new Godot.Vector2(mover.Position.X, mover.Position.Y));
	}
}
