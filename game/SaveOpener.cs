using System.Linq;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// O save que o jogo abriu, com o aviso para o Responsável quando foi preciso recuperá-lo ou o progresso é de uma versão mais nova.
public sealed record OpenedSave(SaveData Data, string? Notice);

// Onde o save fica e como é aberto na partida.
public static class SaveOpener
{
	const string SavePath = "user://save.json";
	// O piloto de desenvolvimento usa um save próprio e descartável, para nunca mexer no progresso nem nas configurações reais.
	const string PilotSavePath = "user://piloto-save.json";

	// Com o piloto de desenvolvimento, o jogo trabalha numa cópia do save: nada do que o piloto clica volta para o real.
	public static string ChoosePath()
	{
		// O teste de ponta a ponta fecha e reabre o jogo no mesmo save, então `--save=` aponta um arquivo que fica de uma execução para a outra.
		if (OS.IsDebugBuild() && OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--save="))?["--save=".Length..] is { Length: > 0 } fixedPath) return fixedPath;
		if (MeshyAssets.Enabled) return "user://meshy-teste-20261019.json";
		if (!OS.IsDebugBuild() || !OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--driver="))) return SavePath;
		DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PilotSavePath));
		if (FileAccess.FileExists(SavePath)) DirAccess.CopyAbsolute(ProjectSettings.GlobalizePath(SavePath), ProjectSettings.GlobalizePath(PilotSavePath));
		return PilotSavePath;
	}

	// Se precisou recuperar ou migrar o save, grava o estado novo para o problema não se repetir.
	public static Result<OpenedSave> Open(SaveStore store)
	{
		var loaded = store.Load();
		if (!loaded.IsOk) return Result<OpenedSave>.Fail(loaded.Error!);

		var outcome = loaded.Value!;
		if (outcome.Notice == SaveNotice.None) return Result<OpenedSave>.Ok(new OpenedSave(outcome.Data, null));

		var written = store.Write(outcome.Data);
		if (!written.IsOk) return Result<OpenedSave>.Fail(written.Error!);

		if (outcome.Notice == SaveNotice.Migrated)
		{
			GD.Print($"Save migrado; o original está em {outcome.CopyPath}");
			return Result<OpenedSave>.Ok(new OpenedSave(written.Value!, null));
		}
		var notice = outcome.Notice == SaveNotice.Recovered
			? "Não foi possível ler o progresso anterior. Ele foi guardado numa cópia e o jogo começou do zero."
			: "Este progresso é de uma versão mais nova do jogo. Ele foi guardado numa cópia e o jogo começou do zero.";
		return Result<OpenedSave>.Ok(new OpenedSave(written.Value!, notice));
	}
}
