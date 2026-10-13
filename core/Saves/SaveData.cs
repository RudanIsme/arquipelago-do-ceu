namespace ArquipelagoDoCeu.Saves;

// Appearance guarda, por categoria, o id da opção escolhida; ausente significa a primeira opção.
// É opcional, então saves antigos continuam válidos na mesma versão do formato.
public sealed record ProfileSave(string Id, string Nickname, int Level, IReadOnlyDictionary<string, string>? Appearance = null);

// Cada campo ausente vale o padrão dos dados; por isso saves sem configurações continuam válidos.
public sealed record SettingsSave(string? TextSize = null, bool? Voice = null, bool? ReduceMotion = null, bool? DyslexiaFont = null, bool? CalmMode = null, string? Controls = null);

// Version é o formato do arquivo, não o progresso; muda só quando o formato muda.
public sealed record SaveData(int Version, IReadOnlyList<ProfileSave> Profiles, SettingsSave? Settings = null)
{
	public const int CurrentVersion = 1;

	public static SaveData Empty => new(CurrentVersion, []);
}
