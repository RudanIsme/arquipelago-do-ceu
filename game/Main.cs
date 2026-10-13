using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// Raiz de composição: liga os adaptadores do Godot às regras de core/.
public partial class Main : Node3D
{
	const string RulesPath = "res://data/rules.json";
	const string SavePath = "user://save.json";

	public override void _Ready()
	{
		var rules = LoadRules();
		var text = rules.IsOk ? rules.Value!.GameName : rules.Error!;

		GetNode<Label>("Interface/Message").Text = text;
		if (rules.IsOk) GD.Print($"Regras carregadas: {text}");
		else GD.PushError(text);

		var notice = LoadSave();
		GetNode<Label>("Interface/Notice").Text = notice;
		if (notice != "") GD.PushWarning(notice);

		AddChild(GD.Load<PackedScene>("res://Lobby/Lobby.tscn").Instantiate());

		GD.Print("Cena inicial pronta.");
	}

	static Result<GameRules> LoadRules()
	{
		using var file = FileAccess.Open(RulesPath, FileAccess.ModeFlags.Read);
		if (file is null)
			return Result<GameRules>.Fail($"Não foi possível abrir {RulesPath}: {FileAccess.GetOpenError()}");

		return GameRules.Parse(file.GetAsText());
	}

	// Devolve o aviso para o Responsável, ou "" quando não há nada a avisar.
	static string LoadSave()
	{
		var store = new SaveStore(ProjectSettings.GlobalizePath(SavePath));
		var loaded = store.Load();
		if (!loaded.IsOk) return loaded.Error!;

		var outcome = loaded.Value!;
		if (outcome.Notice == SaveNotice.None) return "";

		// Sem gravar, o save problemático seguiria no disco e geraria uma cópia nova a cada abertura.
		var written = store.Write(outcome.Data);
		if (!written.IsOk) return written.Error!;

		if (outcome.Notice == SaveNotice.Migrated)
		{
			GD.Print($"Save migrado; o original está em {outcome.CopyPath}");
			return "";
		}

		return outcome.Notice == SaveNotice.Recovered
			? "Não foi possível ler o progresso anterior. Ele foi guardado numa cópia e o jogo começou do zero."
			: "Este progresso é de uma versão mais nova do jogo. Ele foi guardado numa cópia e o jogo começou do zero.";
	}
}
