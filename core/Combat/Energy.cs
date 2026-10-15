namespace ArquipelagoDoCeu.Combat;

// A Energia do treino: começa cheia, nunca passa do máximo e nunca fica abaixo de zero.
public sealed class Energy
{
	public Energy(int max)
	{
		Max = Math.Max(1, max);
		Current = Max;
	}

	public int Max { get; private set; }

	public int Current { get; private set; }

	public bool IsEmpty => Current == 0;

	// Equipar amplia a capacidade, mas nunca recupera Energia perdida.
	public void SetMaximum(int max)
	{
		Max = Math.Max(1, max);
		Current = Math.Min(Current, Max);
	}

	public void Lose(int amount) => Current = Math.Max(0, Current - amount);

	public void Recover(int amount) => Current = Math.Min(Max, Current + amount);
}
