using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Sai do jogo depois de a fala em andamento terminar (no máximo alguns segundos).
public static class GameExit
{
	public static async Task QuitAsync(SceneTree tree, GodotSpeechEngine? speech, int code)
	{
		speech?.Stop();
		var start = Time.GetTicksMsec();
		while (speech is { Silent: false } && Time.GetTicksMsec() - start < 6000)
			await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		WatchExit(code);
		tree.Quit(code);
	}

	// O encerramento do TTS do sistema às vezes não termina e deixaria um processo preso e invisível.
	// Se o jogo não sair em poucos segundos, o vigia o encerra: nada fica pendente, o save é gravado a cada mudança.
	static void WatchExit(int code) =>
		new System.Threading.Thread(() =>
		{
			System.Threading.Thread.Sleep(8000);
			System.Environment.Exit(code);
		})
		{ IsBackground = true, Name = "Vigia da saída" }.Start();
}
