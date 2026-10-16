using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Generation;

// O que um Andar do Plano manda ter: a Sala do Item, o Marco e o Desafio épico (o último Andar).
public sealed record FloorPlan(int Number, bool HoldsItem, bool HasMarco, bool HasEpic);

// A divisão de um Nível em Andares (ADR 0003). O Item fica sempre num Andar anterior ao do Marco, e o Marco
// nunca depois do épico, então as duas torres nunca esperam uma pela outra (ADR 0002).
public sealed record LevelPlan(string Discipline, int Level, IReadOnlyList<FloorPlan> Floors)
{
	// O Plano usa o Andar 0 na seed: ele nasce antes de qualquer Andar.
	public static LevelPlan Create(DungeonRules rules, ulong world, string discipline, int level, int generatorVersion)
	{
		var rng = new Pcg32(Seed.Mix(world, discipline, level, 0, generatorVersion));
		var count = rules.FloorsMin + rng.NextInt(rules.FloorsMax - rules.FloorsMin + 1);
		var itemFloor = 1 + rng.NextInt(count - 1);
		var marcoFloor = itemFloor + 1 + rng.NextInt(count - itemFloor);

		var floors = Enumerable.Range(1, count)
			.Select(n => new FloorPlan(n, n == itemFloor, n == marcoFloor, n == count))
			.ToList();
		return new LevelPlan(discipline, level, floors);
	}
}
