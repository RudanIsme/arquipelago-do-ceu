using System.Collections.Generic;
using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.World;
using Godot;
using Vector2 = System.Numerics.Vector2;
using Vector3 = Godot.Vector3;

namespace ArquipelagoDoCeu;

// O Personagem do Perfil no Lobby: liga as ações de entrada, o clique/toque e a câmera ao
// MovementController (regras em core/) e põe o resultado na cena.
public partial class Player : Node3D
{
	const float Speed = 1.1f;
	const float RunSpeed = 3.3f;
	float currentSpeed;
	public float SpeedMultiplier { get; set; } = 1f;
	public void ApplyEquipment(ArquipelagoDoCeu.Equipment.EquipmentLoadout loadout) => view.ApplyEquipment(loadout);
	Vector2 actualFacing = new(0, 1);
	const float TurnSharpness = 22f;

	readonly System.Func<Vector2, float>? groundHeight;
	readonly Camera3D camera;
	readonly MovementController mover;
	readonly CharacterView view = new() { UseSpectrumTest = true };

	public Player(HexGrid grid, Camera3D camera, System.Func<Vector2, float>? groundHeight = null)
	{
		this.groundHeight = groundHeight;
		this.camera = camera;
		mover = new MovementController(grid, HexGrid.ToWorld(new HexCell(0, 0)), Speed);
		Name = "Player";
	}

	// Desligado enquanto uma tela está aberta: digitar o apelido não pode mover o Personagem.
	public bool Enabled
	{
		get => enabled;
		set
		{
			enabled = value;
			if (!value) mover.Stop();
		}
	}

	bool enabled;
	Vector2? faceTarget;

	public bool IsWalkingAnimation => view.IsWalkingAnimation;

	public Vector2 GroundPosition => mover.Position;

	// Põe o Personagem num ponto do chão de uma vez, por exemplo ao entrar numa Sala.
	public void PlaceAt(Vector2 point)
	{
		mover.Place(point);
		Place();
	}

	// Vira o Personagem para um ponto do chão e dispara o ataque; parado, ele fica olhando para lá.
	public void Attack(Vector2 target)
	{
		var way = target - mover.Position;
		if (way.LengthSquared() > 0.0001f) faceTarget = Vector2.Normalize(way);
		view.PlayAttack();
	}

	public bool ReduceMotion
	{
		get => view.ReduceMotion;
		set => view.ReduceMotion = value;
	}

	public override void _Ready()
	{
		AddChild(view);
		Place();
	}

	public void Apply(IEnumerable<ChosenOption> chosen) => view.Apply(chosen);

	public void WalkTo(Godot.Vector2 screenPoint)
	{
		var origin = camera.ProjectRayOrigin(screenPoint);
		var hit = new Godot.Plane(Vector3.Up, 0f).IntersectsRay(origin, camera.ProjectRayNormal(screenPoint));
		if (hit is { } point) mover.GoTo(new Vector2(point.X, point.Z));
	}

	public override void _Process(double delta)
	{
		var steer = Enabled ? MovementController.FromScreen(ToCore(Controls.Direction())) : Vector2.Zero;
		var dt = (float)delta;
		var running = Enabled && Godot.Input.IsActionPressed(Controls.Run);
		var wantsMove = Enabled && (steer != Vector2.Zero || mover.HasDestination);
		currentSpeed = wantsMove ? Mathf.MoveToward(currentSpeed, (running ? RunSpeed : Speed) * Mathf.Clamp(SpeedMultiplier, 1f, 1.12f), 10f * dt) : 0f;
		var before = mover.Position;
		mover.Tick(steer, dt, currentSpeed / Speed);
		var displacement = mover.Position - before;
		var effectiveSpeed = dt > 0f ? displacement.Length() / dt : 0f;
		if (displacement.LengthSquared() > .0000001f) actualFacing = Vector2.Normalize(displacement);
		Place();
		Turn((float)delta);
		view.SetLocomotion(effectiveSpeed, effectiveSpeed > Speed * 1.15f);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Enabled) return;
		switch (@event)
		{
			case InputEventKey { PhysicalKeycode: Key.F8, Pressed: true, Echo: false }:
				view.ToggleCapeMode();
				GetViewport().SetInputAsHandled();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
				WalkTo(click.Position);
				break;
			// Com o toque também chega um clique emulado; os dois levam ao mesmo ponto, então não atrapalha.
			case InputEventScreenTouch { Pressed: true } touch when !Controls.Pinching:
				WalkTo(touch.Position);
				break;
		}
	}

	void Place() => Position = new Vector3(mover.Position.X, groundHeight?.Invoke(mover.Position) ?? 0f, mover.Position.Y);

	// O modelo olha para +Z; o ângulo sai da direção em que anda.
	void Turn(float delta)
	{
		if (mover.IsMoving) faceTarget = null;
		var facing = faceTarget ?? actualFacing;
		var target = Mathf.Atan2(facing.X, facing.Y);
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, target, 1f - Mathf.Exp(-TurnSharpness * delta)), 0f);
	}

	static Vector2 ToCore(Godot.Vector2 value) => new(value.X, value.Y);
}
