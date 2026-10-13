namespace ArquipelagoDoCeu.Generation;

// PCG32 próprio no lugar de System.Random: a sequência de uma seed não pode mudar entre versões do .NET ou do Godot.
public sealed class Pcg32
{
	const ulong Multiplier = 6364136223846793005UL;

	ulong state;
	readonly ulong increment;

	public Pcg32(ulong seed, ulong sequence = 54)
	{
		increment = (sequence << 1) | 1;
		NextUInt();
		state += seed;
		NextUInt();
	}

	public uint NextUInt()
	{
		var old = state;
		state = unchecked(old * Multiplier + increment);
		var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
		var rotation = (int)(old >> 59);
		return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
	}

	// Sorteio sem viés: descarta os valores que sobrariam de uma divisão inexata.
	public int NextInt(int bound)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
		var threshold = unchecked(0u - (uint)bound) % (uint)bound;
		while (true)
		{
			var r = NextUInt();
			if (r >= threshold) return (int)(r % (uint)bound);
		}
	}

	// Fisher-Yates com o próprio sorteio: a ordem de uma seed não muda entre versões do .NET.
	public void Shuffle<T>(IList<T> items)
	{
		for (var i = items.Count - 1; i > 0; i--)
		{
			var j = NextInt(i + 1);
			(items[i], items[j]) = (items[j], items[i]);
		}
	}
}
