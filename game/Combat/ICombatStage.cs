using Godot;

namespace ArquipelagoDoCeu;

// O palco de uma luta: a câmera que olha a cena e o ponto do chão onde a criatura está. O Lobby e as Salas da Dungeon
// são palcos, e o combate não precisa saber qual deles é.
public interface ICombatStage
{
	Camera3D Camera { get; }

	(Vector3 Ground, float Top) CreatureSpot { get; }
}
