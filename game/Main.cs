using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// Raiz de composição: liga os adaptadores do Godot às regras de core/.
public partial class Main : Node3D
{
	const string RulesPath = "res://data/rules.json";

	public override void _Ready()
	{
		var rules = LoadRules();
		var text = rules.IsOk ? rules.Value!.GameName : rules.Error!;

		GetNode<Label>("Interface/Message").Text = text;
		if (rules.IsOk) GD.Print($"Regras carregadas: {text}");
		else GD.PushError(text);

		GD.Print("Cena inicial pronta.");
	}

	static Result<GameRules> LoadRules()
	{
		using var file = FileAccess.Open(RulesPath, FileAccess.ModeFlags.Read);
		if (file is null)
			return Result<GameRules>.Fail($"Não foi possível abrir {RulesPath}: {FileAccess.GetOpenError()}");

		return GameRules.Parse(file.GetAsText());
	}
}
