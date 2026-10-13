using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;

namespace ArquipelagoDoCeu.Settings;

// As configurações já resolvidas: sempre valores que existem nos dados, nunca nulos.
public sealed record GameSettings(string TextSize, float TextScale, bool Voice, bool ReduceMotion, bool DyslexiaFont, bool CalmMode, string Controls, string? VoiceId, string VoiceSpeed, float VoiceRate);

// Configurações do aparelho: valem para todos os Perfis e ficam no save. Ausente ou fora dos dados
// vale o padrão (pensado para uma criança pequena); a mudança só vale depois de gravada.
public sealed class SettingsService
{
	readonly SaveSession session;
	readonly GameRules rules;
	GameSettings current;

	public SettingsService(SaveSession session, GameRules rules)
	{
		this.session = session;
		this.rules = rules;
		current = Resolve(session.Data.Settings);
	}

	public event Action<GameSettings>? Changed;

	public GameSettings Current => current;

	public SettingsRules Options => rules.Settings;

	public Result<GameSettings> Change(Func<GameSettings, GameSettings> edit)
	{
		var next = edit(current);
		var size = rules.Settings.TextSizes.FirstOrDefault(t => t.Id == next.TextSize);
		if (size is null) return Result<GameSettings>.Fail("Esse tamanho de texto não existe.");
		if (rules.Settings.Controls.All(c => c.Id != next.Controls)) return Result<GameSettings>.Fail("Esse controle não existe.");

		var speed = rules.Settings.VoiceSpeeds.FirstOrDefault(v => v.Id == next.VoiceSpeed);
		if (speed is null) return Result<GameSettings>.Fail("Essa velocidade de voz não existe.");

		var saved = new SettingsSave(next.TextSize, next.Voice, next.ReduceMotion, next.DyslexiaFont, next.CalmMode, next.Controls, next.VoiceId, next.VoiceSpeed);
		var written = session.Commit(d => d with { Settings = saved });
		if (!written.IsOk) return Result<GameSettings>.Fail(written.Error!);

		current = next with { TextScale = size.Scale, VoiceRate = speed.Rate };
		Changed?.Invoke(current);
		return Result<GameSettings>.Ok(current);
	}

	GameSettings Resolve(SettingsSave? saved)
	{
		var defaults = rules.Settings.Defaults;
		var size = rules.Settings.TextSizes.FirstOrDefault(t => t.Id == saved?.TextSize)
			?? rules.Settings.TextSizes.First(t => t.Id == defaults.TextSize);
		var controls = rules.Settings.Controls.FirstOrDefault(c => c.Id == saved?.Controls)?.Id ?? defaults.Controls;
		var speed = rules.Settings.VoiceSpeeds.FirstOrDefault(v => v.Id == saved?.VoiceSpeed)
			?? rules.Settings.VoiceSpeeds.First(v => v.Id == defaults.VoiceSpeed);
		return new GameSettings(
			size.Id,
			size.Scale,
			saved?.Voice ?? defaults.Voice,
			saved?.ReduceMotion ?? defaults.ReduceMotion,
			saved?.DyslexiaFont ?? defaults.DyslexiaFont,
			saved?.CalmMode ?? defaults.CalmMode,
			controls,
			saved?.VoiceId,
			speed.Id,
			speed.Rate);
	}
}
