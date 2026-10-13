using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// Voz do sistema pelo TTS embutido do Godot. Sem suporte a TTS no aparelho (ou em modo headless),
// a lista de vozes vem vazia e o jogo segue só com texto.
public sealed class GodotSpeechEngine : ISpeechEngine
{
	IReadOnlyList<SystemVoice>? cache;

	// Listar as vozes consulta o sistema operacional; só guarda a lista quando ela veio com algo,
	// para uma voz instalada com o jogo aberto ainda ser encontrada.
	public IReadOnlyList<SystemVoice> Voices()
	{
		if (cache is not null) return cache;
		if (!DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech)) return [];

		var voices = DisplayServer.TtsGetVoices()
			.Select(v => new SystemVoice(v["id"].AsString(), v["name"].AsString(), v["language"].AsString()))
			.ToList();
		if (voices.Count > 0) cache = voices;
		return voices;
	}

	public void Speak(string text, string voiceId, float rate, bool interrupt) =>
		DisplayServer.TtsSpeak(text, voiceId, rate: rate, interrupt: interrupt);

	public void Stop() => DisplayServer.TtsStop();
}
