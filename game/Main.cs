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
		var loaded = new SaveStore(ProjectSettings.GlobalizePath(SavePath)).Load();
		if (!loaded.IsOk) return loaded.Error!;

		return loaded.Value!.Notice switch
		{
			SaveNotice.Recovered =>
				"Não foi possível ler o progresso anterior. Ele foi guardado numa cópia e o jogo começou do zero.",
			SaveNotice.NewerVersion =>
				"Este progresso é de uma versão mais nova do jogo. Ele foi guardado numa cópia e o jogo começou do zero.",
			_ => "",
		};
	}
}
