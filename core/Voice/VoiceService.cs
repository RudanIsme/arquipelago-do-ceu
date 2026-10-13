using ArquipelagoDoCeu.Settings;

namespace ArquipelagoDoCeu.Voice;

public sealed record SystemVoice(string Id, string Name, string Language);

// Fronteira com a voz do sistema (no jogo, o TTS do Godot). Com interrupt falso a fala entra na fila.
public interface ISpeechEngine
{
	IReadOnlyList<SystemVoice> Voices();

	void Speak(string text, string voiceId, float rate, bool interrupt);

	void Stop();
}

// Fala frases em pt-BR respeitando as configurações. Nunca falha para quem chama: sem voz pt-BR,
// com a voz desligada ou com texto vazio, Say apenas devolve falso e o jogo segue só com texto.
public sealed class VoiceService
{
	const string Language = "pt-BR";

	readonly ISpeechEngine engine;
	readonly SettingsService settings;

	public VoiceService(ISpeechEngine engine, SettingsService settings)
	{
		this.engine = engine;
		this.settings = settings;
		settings.Changed += s =>
		{
			if (!s.Voice) engine.Stop();
		};
	}

	public IReadOnlyList<SystemVoice> PortugueseVoices() =>
		engine.Voices().Where(v => v.Language.Replace('_', '-').Equals(Language, StringComparison.OrdinalIgnoreCase)).ToList();

	// A voz escolhida, se ainda existe no aparelho; senão a primeira pt-BR.
	public SystemVoice? Chosen()
	{
		var voices = PortugueseVoices();
		return voices.FirstOrDefault(v => v.Id == settings.Current.VoiceId) ?? voices.FirstOrDefault();
	}

	// Só há aviso quando a criança quer voz e o aparelho não tem nenhuma pt-BR.
	public string? MissingVoiceNotice =>
		settings.Current.Voice && Chosen() is null
			? "Este computador não tem uma voz em português do Brasil. O jogo continua só com texto."
			: null;

	public bool Say(string text, bool interrupt = false)
	{
		if (!settings.Current.Voice || string.IsNullOrWhiteSpace(text)) return false;
		if (Chosen() is not { } voice) return false;
		engine.Speak(text, voice.Id, settings.Current.VoiceRate, interrupt);
		return true;
	}

	public void Stop() => engine.Stop();
}
