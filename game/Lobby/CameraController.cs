using System.Collections.Generic;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Liga a câmera do Lobby ao CameraRig: zoom (roda, pinça, teclas) e acompanhamento suave do Personagem.
// O tempo da câmera é o real, para não parar em câmera lenta, e o texto 3D cresce com o afastamento.
public partial class CameraController : Node
{
	// A câmera olha de cima, de um canto da ilha: este é o afastamento fixo entre o ponto focado e a câmera.
	static readonly Vector3 Offset = new(12f, 12f, 12f);
	public const string LabelGroup = "zoom_labels";

	readonly Camera3D camera;
	readonly Player player;
	readonly CameraRig rig;
	readonly CameraRules rules;
	readonly System.Func<bool> reduceMotion;
	readonly Dictionary<int, Godot.Vector2> touches = new();
	ulong lastTicks;
	float lastSize;
	int lastLabels;

	public CameraController(Camera3D camera, Player player, CameraRules rules, System.Func<bool> reduceMotion)
	{
		this.camera = camera;
		this.player = player;
		this.rules = rules;
		this.reduceMotion = reduceMotion;
		rig = new CameraRig(rules);
	}

	public float Size => rig.Size;

	// Onde está a criatura em luta, se há; a câmera centra a luta.
	public System.Func<Vector2?>? Battle { get; set; }

	public override void _Ready() => lastTicks = Time.GetTicksUsec();

	public override void _Process(double delta)
	{
		var now = Time.GetTicksUsec();
		var seconds = Mathf.Min((now - lastTicks) / 1_000_000f, 0.1f);
		lastTicks = now;

		rig.Battle = Battle?.Invoke();
		rig.Update(player.GroundPosition, seconds, reduceMotion());
		Apply();
	}

	void Apply()
	{
		var focus = new Vector3(rig.Focus.X, 2f, rig.Focus.Y);
		camera.Size = rig.Size;
		camera.LookAtFromPosition(focus + Offset, focus, Vector3.Up);
		// O texto 3D só muda de escala quando o zoom muda, e rótulos novos (popup, criatura) entram na escala certa na hora.
		if (Mathf.IsEqualApprox(rig.Size, lastSize) && GetTree().GetNodeCountInGroup(LabelGroup) == lastLabels) return;
		lastSize = rig.Size;
		lastLabels = GetTree().GetNodeCountInGroup(LabelGroup);
		GetTree().CallGroup(LabelGroup, Node3D.MethodName.SetScale, Vector3.One * (rig.Size / rules.Size));
	}

	public override void _Input(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventScreenTouch touch:
				if (touch.Pressed) touches[touch.Index] = touch.Position;
				else touches.Remove(touch.Index);
				Controls.Pinching = touches.Count >= 2;
				break;
			case InputEventScreenDrag drag when touches.ContainsKey(drag.Index):
				Pinch(drag);
				break;
			case InputEventMagnifyGesture magnify:
				rig.Pinch(magnify.Factor);
				break;
		}
	}

	// Com dois dedos, a distância entre eles antes e depois do movimento dá o fator de zoom.
	void Pinch(InputEventScreenDrag drag)
	{
		if (touches.Count < 2) return;
		var before = Spread();
		touches[drag.Index] = drag.Position;
		var after = Spread();
		if (before > 1f && after > 1f) rig.Pinch(after / before);
	}

	float Spread()
	{
		var points = new List<Godot.Vector2>(touches.Values);
		return points[0].DistanceTo(points[1]);
	}

	void ZoomAt(Godot.Vector2 cursor, float factor)
	{
		if (GetViewport().GuiGetHoveredControl() is not null) return;
		var plane = new Plane(Vector3.Up, 0f);
		var before = plane.IntersectsRay(camera.ProjectRayOrigin(cursor), camera.ProjectRayNormal(cursor));
		rig.ZoomFactor(factor);
		Apply();
		var after = plane.IntersectsRay(camera.ProjectRayOrigin(cursor), camera.ProjectRayNormal(cursor));
		if (before is { } a && after is { } b)
		{
			var offset = a - b;
			var middle = GetViewport().GetVisibleRect().Size * .5f;
			var focus = plane.IntersectsRay(camera.ProjectRayOrigin(middle), camera.ProjectRayNormal(middle));
			if (factor < 1f && focus is { } f) offset += (a - f) * .25f;
			rig.Pan(new Vector2(offset.X, offset.Z));
			Apply();
		}
		GetViewport().SetInputAsHandled();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
				ZoomAt(wheel.Position, 1f / 1.12f);
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
				ZoomAt(wheel.Position, 1.12f);
				break;
			default:
				if (@event.IsActionPressed(Controls.ZoomIn)) rig.ZoomSteps(1f);
				else if (@event.IsActionPressed(Controls.ZoomOut)) rig.ZoomSteps(-1f);
				else if (@event.IsActionPressed(Controls.ZoomReset)) rig.ResetZoom();
				break;
		}
	}
}
