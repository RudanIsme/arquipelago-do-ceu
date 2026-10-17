using System;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Voice;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// A Bolha de névoa na cena: avisa pela faixa, pelo texto e pela voz, a leva até a criança e diz se ela acertou.
// Quem tira a Energia é a luta; aqui só se mostra e se conta o acerto.
public sealed class BubbleDirector
{
	// Um quadro longo (janela parada) não vale como tempo de jogo.
	const float MaxFrameSeconds = 0.1f;

	readonly AttackRhythm rhythm;
	readonly BubbleRules rules;
	readonly ICombatStage stage;
	readonly TrainingDummy dummy;
	readonly Player player;
	readonly VoiceService voice;

	public BubbleDirector(BubbleRules rules, AttackRhythm rhythm, ICombatStage stage, TrainingDummy dummy, Player player, VoiceService voice)
	{
		this.rules = rules;
		this.rhythm = rhythm;
		this.stage = stage;
		this.dummy = dummy;
		this.player = player;
		this.voice = voice;
	}

	public void Reset() => rhythm.Bubble.Reset();

	// Apaga a bolha e o aviso da cena.
	public void Clear()
	{
		rhythm.Bubble.Reset();
		dummy.HideWarning();
		dummy.HideBubble();
	}

	// Avança o ritmo da criatura; devolve verdadeiro no quadro em que a bolha atinge a criança.
	// `processDelta` já vem na escala do jogo; o ritmo precisa do tempo de relógio, então desfaz a escala.
	// Parada (Modo calmo, Puzzle), a criatura só reage a erros.
	public bool Tick(double processDelta, bool stopped)
	{
		var scale = (float)Engine.TimeScale;
		var realSeconds = scale > 0f ? Math.Min((float)processDelta / scale, MaxFrameSeconds) : 0f;
		var bubble = rhythm.Bubble;
		if (stopped)
		{
			if (bubble.Phase != BubblePhase.Waiting) Clear();
			rhythm.Tick(realSeconds, Vector2.Zero, Vector2.Zero, stopped: true);
			return false;
		}

		var (ground, _) = stage.CreatureSpot;
		var origin = new Vector2(ground.X, ground.Z);
		var radius = rules.Radius;
		switch (rhythm.Tick(realSeconds, origin, player.GroundPosition, stopped: false))
		{
			case BubbleEvent.Warned:
				voice.Say("Cuidado! Bolha de névoa vindo. Saia do caminho.", interrupt: false);
				break;
			case BubbleEvent.Launched:
				dummy.HideWarning();
				break;
			case BubbleEvent.Hit:
				dummy.HideBubble();
				dummy.Hurt();
				return true;
			case BubbleEvent.Missed:
				dummy.HideBubble();
				break;
		}

		if (bubble.Phase == BubblePhase.Warning)
		{
			var way = bubble.DirectionFrom(origin);
			dummy.ShowWarning(new Vector3(origin.X, 0f, origin.Y), new Vector3(way.X, 0f, way.Y), rules.Range, radius * 2f, bubble.WarningProgress);
		}
		else if (bubble.Phase == BubblePhase.Flying) dummy.MoveBubble(new Vector3(bubble.Position.X, 0f, bubble.Position.Y), radius);
		return false;
	}
}
