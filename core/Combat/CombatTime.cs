using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.Combat;

// A escala de tempo do jogo enquanto a criança monta a resposta: desacelera, ou para no Modo calmo,
// e volta ao normal quando o ataque sai. O adaptador do Godot só copia Scale para Engine.TimeScale.
public sealed class CombatTime
{
	readonly CombatRules rules;
	bool assembling;
	bool calm;

	public CombatTime(CombatRules rules) => this.rules = rules;

	public float Scale => !assembling ? 1f : calm ? 0f : rules.SlowTimeScale;

	public void Begin(bool calmMode)
	{
		assembling = true;
		calm = calmMode;
	}

	public void End() => assembling = false;
}
