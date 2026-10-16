using System.Text;

namespace ArquipelagoDoCeu.Generation;

// Mistura as partes da seed (mundo, dungeon, nível, andar, versão do gerador) num número só.
// FNV-1a escrito aqui, e não GetHashCode, porque a seed de um Andar não pode mudar entre versões do .NET.
public static class Seed
{
	const ulong Offset = 14695981039346656037UL;
	const ulong Prime = 1099511628211UL;

	public static ulong Mix(ulong world, string discipline, int level, int floor, int version)
	{
		var hash = Offset;
		Add(ref hash, BitConverter.GetBytes(world));
		Add(ref hash, Encoding.UTF8.GetBytes(discipline));
		Add(ref hash, BitConverter.GetBytes(level));
		Add(ref hash, BitConverter.GetBytes(floor));
		Add(ref hash, BitConverter.GetBytes(version));
		return hash;
	}

	static void Add(ref ulong hash, byte[] bytes)
	{
		foreach (var b in bytes)
		{
			hash ^= b;
			hash = unchecked(hash * Prime);
		}
		// Separa os campos, para "ab" + "c" não ser igual a "a" + "bc".
		hash ^= 0xFF;
		hash = unchecked(hash * Prime);
	}
}
