using System.Numerics;

namespace ArquipelagoDoCeu.World;

// Um objeto do Lobby com que dá para interagir, no plano (x, z).
public sealed record Interactable(string Id, Vector2 Position);

public static class InteractionFinder
{
	// O objeto mais perto dentro do alcance; no empate vence o primeiro da lista, para a indicação
	// nunca ficar trocando de um quadro para o outro.
	public static Interactable? Nearest(Vector2 from, IEnumerable<Interactable> items, float reach)
	{
		Interactable? best = null;
		var bestDistance = float.MaxValue;
		foreach (var item in items)
		{
			var distance = Vector2.Distance(from, item.Position);
			if (distance > reach || distance >= bestDistance) continue;
			best = item;
			bestDistance = distance;
		}
		return best;
	}
}
