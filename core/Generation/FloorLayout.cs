using System.Numerics;
using ArquipelagoDoCeu.World;

namespace ArquipelagoDoCeu.Generation;

// Uma Sala posta no espaço: a forma dela e o hexágono do mundo onde fica a origem da forma.
public sealed record PlacedRoom(int Room, RoomForm Form, HexCell Origin)
{
	public IEnumerable<HexCell> Cells => Form.Cells.Select(c => c + Origin);

	public bool Contains(HexCell cell) => Form.Cells.Contains(cell - Origin);

	// O hexágono de interesse da Sala, onde ficam a criatura, a escada e a fonte.
	public HexCell Spot => Form.Center + Origin;

	public HexCell SideSpot => Form.Side + Origin;

	public float HeightAt(HexCell cell) => Form.HeightAt(cell - Origin);

	public Vector2 SpotWorld => HexGrid.ToWorld(Spot);
}

// O corredor a pé entre duas Salas ligadas: uma tira de hexágonos que sai da porta de `From` e chega à porta de `To`.
// Cells[0] é a porta do lado de `From` e Cells[^1], a de `To`.
public sealed record Corridor(int From, int To, IReadOnlyList<HexCell> Cells);

// O Andar no espaço (ADR 0008): onde fica cada Sala e por onde a criança anda de uma até a outra.
public sealed record FloorLayout(IReadOnlyList<PlacedRoom> Rooms, IReadOnlyList<Corridor> Corridors)
{
	public PlacedRoom Room(int id) => Rooms.First(r => r.Room == id);

	public Corridor? CorridorOf(int a, int b) => Corridors.FirstOrDefault(c => (c.From == a && c.To == b) || (c.From == b && c.To == a));

	// Os hexágonos do corredor na ordem de quem sai de `from` para `to`.
	public IReadOnlyList<HexCell>? CorridorBetween(int from, int to) =>
		CorridorOf(from, to) is { } corridor ? (corridor.From == from ? corridor.Cells : corridor.Cells.Reverse().ToList()) : null;

	// A Sala que contém o hexágono, ou nenhuma se for corredor ou vazio.
	public PlacedRoom? RoomAt(HexCell cell) => Rooms.FirstOrDefault(r => r.Contains(cell));
}
