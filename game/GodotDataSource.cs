using ArquipelagoDoCeu.Data;
using Godot;

namespace ArquipelagoDoCeu;

// Os arquivos de dados do jogo ficam em res://data; é o único lugar que sabe disso.
public sealed class GodotDataSource : IDataSource
{
	const string Folder = "res://data/";

	public Result<string> Read(string name)
	{
		using var file = FileAccess.Open(Folder + name, FileAccess.ModeFlags.Read);
		return file is null
			? Result<string>.Fail($"Não foi possível abrir {Folder}{name}: {FileAccess.GetOpenError()}")
			: Result<string>.Ok(file.GetAsText());
	}
}
