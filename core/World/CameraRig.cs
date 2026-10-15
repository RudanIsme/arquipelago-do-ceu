using System.Numerics;
using ArquipelagoDoCeu.Rules;

namespace ArquipelagoDoCeu.World;

// O zoom e o acompanhamento da câmera do Lobby, sem Godot: o adaptador só copia Size e Focus para a câmera.
// Com a redução de animações a câmera não desliza nem aproxima aos poucos: muda de uma vez, e só quando preciso.
public sealed class CameraRig
{
	readonly CameraRules rules;
	float targetSize;

	public CameraRig(CameraRules rules)
	{
		this.rules = rules;
		Size = targetSize = rules.Size;
	}

	// Quanto do mundo a câmera mostra na altura da tela: menor é mais perto.
	public float Size { get; private set; }

	// O ponto do chão para onde a câmera olha.
	public Vector2 Focus { get; private set; }

	// Passos positivos aproximam, negativos afastam; sempre dentro dos limites dos dados.
	public void ZoomSteps(float steps) => targetSize = Math.Clamp(targetSize - steps * rules.ZoomStep, rules.MinSize, rules.MaxSize);

	// Gesto de pinça: fator maior que 1 aproxima.
	public void Pinch(float factor)
	{
		if (factor > 0.01f) targetSize = Math.Clamp(targetSize / factor, rules.MinSize, rules.MaxSize);
	}

	public void ZoomFactor(float factor) => Size = targetSize = Math.Clamp(Size * factor, rules.MinSize, rules.MaxSize);

	public void Pan(Vector2 offset)
	{
		Focus += offset;
		if (Focus.Length() > rules.MaxPan) Focus = Vector2.Normalize(Focus) * rules.MaxPan;
	}

	public void ResetZoom() => targetSize = rules.Size;

	public void Update(Vector2 player, float seconds, bool reduceMotion)
	{
		Size = reduceMotion ? targetSize : Size + (targetSize - Size) * (1f - MathF.Exp(-rules.FollowSharpness * 2f * seconds));

		var away = player - Focus;
		var distance = away.Length();
		if (distance > rules.DeadZone)
		{
			var excess = away * ((distance - rules.DeadZone) / distance);
			Focus += reduceMotion ? excess : excess * (1f - MathF.Exp(-rules.FollowSharpness * seconds));
		}
		if (Focus.Length() > rules.MaxPan) Focus = Vector2.Normalize(Focus) * rules.MaxPan;
	}
}
