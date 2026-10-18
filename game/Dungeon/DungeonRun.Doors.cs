using ArquipelagoDoCeu.Dungeon;

namespace ArquipelagoDoCeu;

// O que a Expedição faz com as portas físicas e o mapa: a criança cruza o corredor a pé, e o mapa em tela cheia abre fora de combate.
public partial class DungeonRun
{
	// O mapa em tela cheia pausa o jogo, por isso só abre fora de combate: a criança precisa responder ao pedido.
	void OpenMap()
	{
		if (left || !enabled || hud.InCombat() || ctx.ShowOverlay is not { } show) return;
		show(new MapScreen(expedition, $"{ctx.TowerName} · Nível {ctx.Level} · Andar {expedition.Floor.Number}{narration.OfTotal()}", IsFloorDone(), player.ReduceMotion,
			room => Say(narration.RoomSpeech(room)), () => Say(narration.MapSpeech(IsFloorDone()))));
	}

	// A criança atravessou o corredor e pisou numa Sala nova ou já conhecida: é nela que a Expedição passa a estar.
	void Arrive(int room)
	{
		var from = expedition.CurrentRoom;
		if (expedition.MoveTo(room) == Passage.Open) EnterRoom(from);
	}
}
