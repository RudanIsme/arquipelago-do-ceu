using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// A escala de tempo do jogo: lenta enquanto a criança desenha a resposta e, no Modo calmo, lenta a luta toda.
// O Personagem não sente a câmera lenta (anda no tempo normal). O adaptador do Godot só copia Scale para Engine.TimeScale.
public sealed class CombatTime
{
	readonly CombatRules rules;
	bool drawing;
	bool calm;

	public CombatTime(CombatRules rules) => this.rules = rules;

	public float Scale => drawing || calm ? rules.SlowTimeScale : 1f;

	public void Begin() => drawing = true;

	public void End() => drawing = false;

	// Liga e desliga o Modo calmo para a luta que começa ou acaba.
	public void Calm(bool on) => calm = on;
}
