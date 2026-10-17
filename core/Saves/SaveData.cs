namespace ArquipelagoDoCeu.Saves;

// Os campos além de Level são opcionais, então saves antigos continuam válidos na mesma versão do formato.
// Appearance guarda a opção escolhida por categoria; Worn, os Cosméticos vestidos; Seen e GuideSeen, o que o Inventário já mostrou.
public sealed record ProfileSave(string Id, string Nickname, int Level, IReadOnlyDictionary<string, string>? Appearance = null, ProgressSave? Progress = null, IReadOnlyDictionary<string, ArquipelagoDoCeu.Equipment.Gear>? Gear = null,
	IReadOnlyList<string>? Worn = null, IReadOnlyList<string>? Seen = null, bool? GuideSeen = null);

// Cada campo ausente vale o padrão dos dados; por isso saves sem configurações continuam válidos.
public sealed record SettingsSave(string? TextSize = null, bool? Voice = null, bool? ReduceMotion = null, bool? DyslexiaFont = null, bool? CalmMode = null, string? Controls = null, string? VoiceId = null, string? VoiceSpeed = null, bool? ReduceEffects = null);

// Version é o formato do arquivo, não o progresso; muda só quando o formato muda.
public sealed record SaveData(int Version, IReadOnlyList<ProfileSave> Profiles, SettingsSave? Settings = null)
{
	public const int CurrentVersion = 1;

	public static SaveData Empty => new(CurrentVersion, []);
}
