using System.Numerics;

namespace ArquipelagoDoCeu.World;

// Move o Personagem pelo chão: teclado (direção) ou toque/clique (caminho). O teclado sempre
// vence e cancela o caminho; quem chama só informa a direção e o tempo.
public sealed class MovementController
{
	const float ArrivalEpsilon = 0.0001f;

	readonly HexGrid grid;
	readonly float speed;
	readonly float radius;
	readonly Queue<Vector2> waypoints = new();

	public MovementController(HexGrid grid, Vector2 start, float speed, float radius = 0.2f)
	{
		this.grid = grid;
		this.speed = speed;
		this.radius = radius;
		Position = start;
	}

	public Vector2 Position { get; private set; }
	public Vector2 Facing { get; private set; } = new(0f, 1f);
	public bool IsMoving { get; private set; }
	public bool HasDestination => waypoints.Count > 0;

	// A câmera é isométrica a 45 graus: "para cima" na tela é o fundo do mapa na diagonal.
	// A entrada tem x para a direita e y para cima; a saída é (x, z) no mundo.
	public static Vector2 FromScreen(Vector2 input)
	{
		if (input == Vector2.Zero) return Vector2.Zero;
		var world = new Vector2(input.X - input.Y, -input.X - input.Y);
		return Vector2.Normalize(world);
	}

	// Vai até o ponto; se ele é obstáculo ou está fora da ilha, vai ao hexágono livre mais perto.
	public bool GoTo(Vector2 target)
	{
		var targetCell = HexGrid.FromWorld(target);
		var destination = grid.IsWalkable(targetCell) ? targetCell : grid.NearestWalkable(targetCell, HexGrid.FromWorld(Position));
		if (destination is null) return false;

		var path = grid.FindPath(HexGrid.FromWorld(Position), destination.Value);
		if (path is null) return false;

		waypoints.Clear();
		foreach (var cell in path.Skip(1)) waypoints.Enqueue(HexGrid.ToWorld(cell));
		// O ponto exato só vale se o corpo cabe ali; senão fica o centro do hexágono.
		var exact = targetCell == destination.Value && grid.IsFree(target, radius);
		if (exact && waypoints.Count > 0)
		{
			var rebuilt = waypoints.ToArray();
			rebuilt[^1] = target;
			waypoints.Clear();
			foreach (var point in rebuilt) waypoints.Enqueue(point);
		}
		else if (exact) waypoints.Enqueue(target);
		return true;
	}

	public void Stop()
	{
		waypoints.Clear();
		IsMoving = false;
	}

	public void Tick(Vector2 steer, float dt, float speedScale = 1f)
	{
		dt *= MathF.Max(0f, speedScale);
		if (dt <= 0f) { IsMoving = false; return; }
		if (steer != Vector2.Zero)
		{
			waypoints.Clear();
			var direction = steer.Length() > 1f ? Vector2.Normalize(steer) : steer;
			Facing = Vector2.Normalize(direction);
			var moved = Position;
			// Passos curtos impedem atravessar um obstaculo quando a corrida ou um frame longo aumenta a distancia.
			var displacement = direction * speed * dt;
			var steps = Math.Max(1, (int)MathF.Ceiling(displacement.Length() / MathF.Max(.05f, radius * .5f)));
			for (var i = 0; i < steps; i++) moved = grid.Move(moved, displacement / steps, radius);
			IsMoving = moved != Position;
			Position = moved;
			return;
		}
		FollowPath(dt);
	}

	void FollowPath(float dt)
	{
		var before = Position;
		var remaining = speed * dt;
		while (remaining > ArrivalEpsilon && waypoints.Count > 0)
		{
			var toNext = waypoints.Peek() - Position;
			var distance = toNext.Length();
			if (distance > 0f) Facing = toNext / distance;
			if (distance <= remaining)
			{
				Position = waypoints.Dequeue();
				remaining -= distance;
			}
			else
			{
				Position += toNext / distance * remaining;
				remaining = 0f;
			}
		}
		IsMoving = Position != before;
	}
}
