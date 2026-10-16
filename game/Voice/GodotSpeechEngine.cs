using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// Voz do sistema pelo TTS embutido do Godot. Sem suporte a TTS no aparelho (ou em modo headless),
// a lista de vozes vem vazia e o jogo segue só com texto.
//
// O jogo nunca interrompe uma fala do sistema: parar ou trocar uma fala em andamento (TtsStop, ou `interrupt` do Godot)
// travou ou derrubou o jogo no Windows, de vez em quando, mesmo numa thread à parte. Em vez disso o jogo guarda o que
// ainda não foi dito, joga fora o que ficou velho quando chega um aviso novo e só manda falar quando a fala anterior acaba.
public sealed class GodotSpeechEngine : ISpeechEngine
{
	const ulong EmptyRetryMilliseconds = 60_000;
	// Uma fala que não termina nunca (voz presa) não pode calar o jogo para sempre.
	const ulong StuckMilliseconds = 30_000;

	sealed record Utterance(string Text, string VoiceId, float Rate);

	IReadOnlyList<SystemVoice>? cache;
	ulong? lastEmptyQuery;
	readonly Queue<Utterance> pending = new();
	ulong waitingSince;
	bool ticking;

	// Listar as vozes consulta o sistema operacional, o que é lento (cerca de 200 ms) e, repetido a cada fala,
	// pesa à toa. A lista com vozes fica guardada; a vazia vale por um minuto, para uma voz instalada com o jogo
	// aberto ainda ser encontrada.
	public IReadOnlyList<SystemVoice> Voices()
	{
		if (OS.IsDebugBuild() && OS.GetCmdlineUserArgs().Contains("--sem-voz")) return [];
		if (cache is not null) return cache;
		if (lastEmptyQuery is { } last && Time.GetTicksMsec() - last < EmptyRetryMilliseconds) return [];
		if (!DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech)) return NoVoices();

		var voices = DisplayServer.TtsGetVoices()
			.Select(v => new SystemVoice(v["id"].AsString(), v["name"].AsString(), v["language"].AsString()))
			.ToList();
		if (voices.Count > 0) cache = voices;
		else lastEmptyQuery = Time.GetTicksMsec();
		return voices;
	}

	IReadOnlyList<SystemVoice> NoVoices()
	{
		lastEmptyQuery = Time.GetTicksMsec();
		return [];
	}

	// `interrupt` joga fora o que ainda esperava a vez; a fala em andamento termina sozinha.
	public void Speak(string text, string voiceId, float rate, bool interrupt)
	{
		if (interrupt) pending.Clear();
		pending.Enqueue(new Utterance(text, voiceId, rate));
		if (waitingSince == 0) waitingSince = Time.GetTicksMsec();
		StartTicking();
	}

	// Parar é esquecer o que falta dizer.
	public void Stop() => pending.Clear();

	// Sem nada a dizer e sem fala em andamento. Fechar o jogo no meio de uma fala travou o encerramento do TTS, então quem
	// fecha o jogo espera isto ficar verdadeiro, deixando o jogo rodar quadros (é assim que o TTS percebe que a fala acabou).
	public bool Silent => pending.Count == 0 && !(DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech) && DisplayServer.TtsIsSpeaking());

	void StartTicking()
	{
		if (ticking || Engine.GetMainLoop() is not SceneTree tree) return;
		ticking = true;
		tree.ProcessFrame += Tick;
	}

	void Tick()
	{
		if (pending.Count == 0)
		{
			waitingSince = 0;
			return;
		}

		var speaking = DisplayServer.TtsIsSpeaking();
		if (speaking && Time.GetTicksMsec() - waitingSince < StuckMilliseconds) return;
		if (speaking) DisplayServer.TtsStop();

		var next = pending.Dequeue();
		DisplayServer.TtsSpeak(next.Text, next.VoiceId, rate: next.Rate, interrupt: false);
		waitingSince = pending.Count > 0 ? Time.GetTicksMsec() : 0;
	}
}
