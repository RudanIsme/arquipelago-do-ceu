namespace ArquipelagoDoCeu.Saves;

public sealed record ProfileSave(string Id, string Nickname, int Level);

// Version é o formato do arquivo, não o progresso; muda só quando o formato muda.
public sealed record SaveData(int Version, IReadOnlyList<ProfileSave> Profiles)
{
	public const int CurrentVersion = 1;

	public static SaveData Empty => new(CurrentVersion, []);
}
