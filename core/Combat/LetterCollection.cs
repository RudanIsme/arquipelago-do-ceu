using System.Collections.Generic;
using System.Linq;
namespace ArquipelagoDoCeu.Combat;

public sealed class LetterCollection
{
	readonly List<int> chosen = new();
	public IReadOnlyList<string> Target { get; } = new[] { "B", "O", "A" };
	public IReadOnlyList<string> Pieces { get; } = new[] { "A", "B", "O", "E", "U" };
	public IReadOnlyList<int> Chosen => chosen;
	public bool Complete => chosen.Count == Target.Count && chosen.Select(i => Pieces[i]).SequenceEqual(Target);
	public bool Place(int i)
	{
		if (i < 0 || i >= Pieces.Count || chosen.Contains(i) || chosen.Count >= Target.Count) return false;
		chosen.Add(i);
		return true;
	}

	public bool Undo()
	{
		if (chosen.Count == 0) return false;
		chosen.RemoveAt(chosen.Count - 1);
		return true;
	}
	public void Reset() => chosen.Clear();
}
