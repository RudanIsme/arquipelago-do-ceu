using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// Voz do sistema pelo TTS embutido do Godot. Sem suporte a TTS no aparelho (ou em modo headless),
// a lista de vozes vem vazia e o jogo segue só com texto.
public sealed class GodotSpeechEngine : ISpeechEngine
{
	const ulong EmptyRetryMilliseconds = 60_000;
	const int MaxPending = 8;

	IReadOnlyList<SystemVoice>? cache;
	ulong? lastEmptyQuery;

	// Falar e parar uma fala vão para uma thread só delas. No Windows, interromper uma fala no laço principal do jogo
	// trava os dois lados de vez em quando (o motor de áudio espera o laço, e o laço espera o motor); numa thread à parte
	// o laço continua livre, e uma voz que trave nunca trava a criança. Se a fila encher, o que é velho se perde.
	readonly BlockingCollection<Action> work = new(MaxPending);
	Thread? worker;

	// Listar as vozes consulta o sistema operacional, o que é lento (cerca de 200 ms) e, repetido a cada fala,
	// pesa à toa. A lista com vozes fica guardada; a vazia vale por um minuto, para uma voz instalada com o jogo
	// aberto ainda ser encontrada.
	public IReadOnlyList<SystemVoice> Voices()
	{
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

	public void Speak(string text, string voiceId, float rate, bool interrupt) =>
		Post(() =>
		{
			if (interrupt && DisplayServer.TtsIsSpeaking()) DisplayServer.TtsStop();
			DisplayServer.TtsSpeak(text, voiceId, rate: rate, interrupt: false);
		});

	public void Stop() => Post(DisplayServer.TtsStop);

	void Post(Action action)
	{
		EnsureWorker();
		// Fila cheia: a voz está presa ou muito atrasada; melhor perder a fala do que travar o jogo.
		work.TryAdd(action);
	}

	void EnsureWorker()
	{
		if (worker is not null) return;
		worker = new Thread(() =>
		{
			foreach (var action in work.GetConsumingEnumerable())
			{
				try
				{
					action();
				}
				catch (Exception e)
				{
					GD.PushWarning($"A voz falhou: {e.Message}");
				}
			}
		})
		{ IsBackground = true, Name = "Voz do sistema" };
		worker.Start();
	}
}
