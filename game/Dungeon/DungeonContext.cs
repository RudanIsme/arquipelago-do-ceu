using System;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;


// Tudo o que a Dungeon precisa de fora, reunido num lugar só para a Main não repassar uma lista de parâmetros.
public sealed record DungeonContext(
	GameRules Rules,
	string ProfileId,
	string Discipline,
	string TowerName,
	int Level,
	VoiceService Voice,
	SettingsService Settings,
	Control UiRoot,
	ProgressServices Progress,
	FallbackFloors Fallback,
	RewardCatalog Rewards,
	RewardService Granter,
	string Nickname,
	string RoleName,
	TowerCatalog Towers,
	int ExtraEnergy,
	float ExtraReach,
	Action<Player> ConfigurePlayer,
	IRandomSource Random,
	int StartFloor = 1,
	bool AtFastTravel = false,
	Action<OverlayScreen>? ShowOverlay = null,
	bool Shortcut = false,
	bool Free = false,
	ConceptNames? Concepts = null,
	Action? OpenInventory = null,
	Func<int>? NewInventoryItems = null)
{
	// Muda quando o gerador muda de jeito, para os Andares antigos não valerem para os novos (ADR 0003).
	public const int GeneratorVersion = 1;

	public LevelKey LevelKey => new(Discipline, Level);

	public FloorKey FloorKeyOf(int number) => new(Discipline, Level, number);

	// Em build de depuração, `--seed-do-mundo=N` fixa a seed para o piloto repetir sempre os mesmos Andares.
	public ulong WorldSeed =>
		OS.IsDebugBuild() && OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--seed-do-mundo="))?["--seed-do-mundo=".Length..] is { } text && ulong.TryParse(text, out var forced)
			? forced
			: Seed.ForProfile(ProfileId);

	public Tower Tower => Towers.Find(Discipline)!;

	public string TowerNameOf(string discipline) => Towers.Find(discipline)?.Name ?? discipline;

	// A outra torre: é dela o Item de troca que abre o Marco desta.
	public string OtherTower => Towers.Exchange(Tower)?.Id ?? Discipline;

	public bool HasTradeItem() => Progress.Levels.HasItem(ProfileId, new LevelKey(OtherTower, Level));
}
