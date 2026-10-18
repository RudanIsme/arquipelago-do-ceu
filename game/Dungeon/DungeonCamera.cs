using System;
using System.Linq;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// A câmera da Dungeon: enquadra a Sala inteira, com as portas e o Personagem, e nos corredores acompanha a criança.
// Sem animação ela muda de uma vez.
public sealed class DungeonCamera
{
	const float MinSize = 12f;
	const float MaxSize = 44f;
	const float CorridorSize = 14f;
	// Folga em volta da Sala para a interface (mapa, placa, dock) não cobrir nada.
	const float Padding = 1.4f;
	const float Sharpness = 4f;

	Vector2 focus;
	float size = 13f;
	bool placed;

	// O chão andou `shift` (ao trocar de Sala); o foco anda junto para a imagem não pular.
	public void Move(Vector2 shift, bool snap)
	{
		focus -= shift;
		if (snap) placed = false;
	}

	public void Update(DungeonWorld world, Vector2 player, int currentRoom, float seconds, bool reduceMotion, Godot.Vector2 viewport)
	{
		var inRoom = world.RoomAt(player) == currentRoom;
		var (targetFocus, targetSize) = inRoom ? (world.CenterOfRoom(), Fit(world, viewport)) : (player, CorridorSize);
		if (!placed || reduceMotion)
		{
			(focus, size, placed) = (targetFocus, targetSize, true);
		}
		else
		{
			var blend = 1f - MathF.Exp(-Sharpness * seconds);
			focus += (targetFocus - focus) * blend;
			size += (targetSize - size) * blend;
		}
		world.Frame(focus, size);
	}

	// O menor zoom em que cabem a Sala e as portas na tela, olhando como a câmera olha (isométrica).
	static float Fit(DungeonWorld world, Godot.Vector2 viewport)
	{
		var right = new Godot.Vector3(1f, 0f, -1f).Normalized();
		var up = new Godot.Vector3(-1f, 0f, -1f).Normalized();
		var points = world.Bounds().Select(p => new Godot.Vector3(p.X, 0f, p.Y)).ToList();
		var width = points.Max(p => p.Dot(right)) - points.Min(p => p.Dot(right)) + 3f;
		// A câmera olha de cima a cerca de 35 graus: o que está no chão se acha na tela com a altura encolhida.
		var depth = (points.Max(p => p.Dot(up)) - points.Min(p => p.Dot(up)) + 3f) * 0.6f + 3f;
		var aspect = viewport.Y > 0f ? viewport.X / viewport.Y : 16f / 9f;
		return Math.Clamp(Math.Max(depth, width / aspect) * Padding, MinSize, MaxSize);
	}
}
