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
	readonly BubbleAttack bubble;
	readonly BubbleRules rules;
	readonly ICombatStage stage;
	readonly TrainingDummy dummy;
	readonly Player player;
	readonly VoiceService voice;

	public BubbleDirector(BubbleRules rules, ICombatStage stage, TrainingDummy dummy, Player player, VoiceService voice)
	{
		this.rules = rules;
		this.stage = stage;
		this.dummy = dummy;
		this.player = player;
		this.voice = voice;
		bubble = new BubbleAttack(rules);
	}

	public void Reset() => bubble.Reset();

	// Apaga a bolha e o aviso da cena.
	public void Clear()
	{
		bubble.Reset();
		dummy.HideWarning();
		dummy.HideBubble();
	}

	// Avança a bolha; devolve verdadeiro no quadro em que ela atinge a criança. Parada (Modo calmo, Puzzle), o Boneco só reage a erros.
	public bool Tick(float delta, bool stopped)
	{
		if (stopped)
		{
			if (bubble.Phase != BubblePhase.Waiting) Clear();
			return false;
		}

		var (ground, _) = stage.CreatureSpot;
		var origin = new Vector2(ground.X, ground.Z);
		var radius = rules.Radius;
		switch (bubble.Update(delta, origin, player.GroundPosition))
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
