using System;
using System.Linq;
using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// Raiz de composição: liga os adaptadores do Godot às regras de core/ e conduz o fluxo das telas.
public partial class Main : Node3D
{
	const string RulesPath = "res://data/rules.json";
	const string SavePath = "user://save.json";

	CanvasLayer ui = null!;
	Label title = null!;
	Label notice = null!;
	GameRules rules = null!;
	ProfileService profiles = null!;
	ProfileScreen? screen;
	CharacterEditor? editor;
	Button characterButton = null!;
	ProfileSave? current;
	Player player = null!;

	public override void _Ready()
	{
		ui = GetNode<CanvasLayer>("Interface");
		title = GetNode<Label>("Interface/Message");
		notice = GetNode<Label>("Interface/Notice");

		var loadedRules = LoadRules();
		if (!loadedRules.IsOk) { Stop(loadedRules.Error!); return; }
		rules = loadedRules.Value!;
		title.Text = rules.GameName;
		GD.Print($"Regras carregadas: {rules.GameName}");

		var store = new SaveStore(ProjectSettings.GlobalizePath(SavePath));
		var save = OpenSave(store);
		if (!save.IsOk) { Stop(save.Error!); return; }
		profiles = new ProfileService(store, rules, save.Value!, () => Guid.NewGuid().ToString("N"));

		Controls.Register();
		var lobby = GD.Load<PackedScene>("res://Lobby/Lobby.tscn").Instantiate<Lobby>();
		AddChild(lobby);
		player = new Player(lobby.Grid, lobby.Camera) { Visible = false, ReduceMotion = OS.GetCmdlineUserArgs().Contains("--reduzir-animacoes") };
		AddChild(player);
		var hud = new HBoxContainer();
		hud.AddThemeConstantOverride("separation", 8);
		hud.AddChild(UiKit.BigButton("Perfis", ShowProfiles, 160));
		characterButton = UiKit.BigButton("Personagem", ShowEditor, 220);
		characterButton.Visible = false;
		hud.AddChild(characterButton);
		ui.AddChild(hud);
		StartFlow();
		DevDriver.AttachIfRequested(this);

		GD.Print("Cena inicial pronta.");
	}

	void Stop(string text)
	{
		title.Text = text;
		GD.PushError(text);
	}

	static Result<GameRules> LoadRules()
	{
		using var file = FileAccess.Open(RulesPath, FileAccess.ModeFlags.Read);
		if (file is null)
			return Result<GameRules>.Fail($"Não foi possível abrir {RulesPath}: {FileAccess.GetOpenError()}");

		return GameRules.Parse(file.GetAsText());
	}

	// Abre o save; se precisou recuperá-lo ou migrá-lo, grava o estado novo para o problema não se repetir
	// e avisa o Responsável na tela.
	Result<SaveData> OpenSave(SaveStore store)
	{
		var loaded = store.Load();
		if (!loaded.IsOk) return Result<SaveData>.Fail(loaded.Error!);

		var outcome = loaded.Value!;
		if (outcome.Notice == SaveNotice.None) return Result<SaveData>.Ok(outcome.Data);

		var written = store.Write(outcome.Data);
		if (!written.IsOk) return written;

		if (outcome.Notice == SaveNotice.Migrated)
			GD.Print($"Save migrado; o original está em {outcome.CopyPath}");
		else
		{
			notice.Text = outcome.Notice == SaveNotice.Recovered
				? "Não foi possível ler o progresso anterior. Ele foi guardado numa cópia e o jogo começou do zero."
				: "Este progresso é de uma versão mais nova do jogo. Ele foi guardado numa cópia e o jogo começou do zero.";
			GD.PushWarning(notice.Text);
		}
		return written;
	}

	// Um só Perfil entra direto; zero ou vários passam pela tela de Perfis.
	void StartFlow()
	{
		if (profiles.Profiles.Count == 1) Enter(profiles.Profiles[0]);
		else ShowProfiles();
	}

	void ShowProfiles()
	{
		if (screen is not null) return;
		screen = new ProfileScreen(profiles);
		screen.Chosen += Enter;
		ui.AddChild(screen);
		UpdatePlayerControl();
	}

	void Enter(ProfileSave profile)
	{
		screen?.QueueFree();
		screen = null;
		editor?.QueueFree();
		editor = null;
		current = profile;
		characterButton.Visible = true;
		title.Text = $"{rules.GameName}  ·  {profile.Nickname}";
		player.Visible = true;
		ApplyAppearance();
		UpdatePlayerControl();
		GD.Print($"Perfil ativo: {profile.Nickname}");
	}

	void ShowEditor()
	{
		if (editor is not null || screen is not null || current is null) return;
		editor = new CharacterEditor(profiles, current.Id);
		editor.Changed += ApplyAppearance;
		editor.Done += () =>
		{
			editor?.QueueFree();
			editor = null;
			UpdatePlayerControl();
		};
		ui.AddChild(editor);
		UpdatePlayerControl();
	}

	// O Personagem do Lobby usa sempre a aparência gravada do Perfil ativo.
	void ApplyAppearance()
	{
		if (current is null) return;
		player.Apply(profiles.AppearanceOf(profiles.Find(current.Id).Value!));
	}

	// Com uma tela aberta, teclado e toque são da tela, não do Personagem.
	void UpdatePlayerControl() => player.Enabled = current is not null && screen is null && editor is null;
}
