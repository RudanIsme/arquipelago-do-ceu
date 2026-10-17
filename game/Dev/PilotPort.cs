using System;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A porta do piloto de depuração: o que o roteiro enxerga do jogo e as vitórias e derrotas que ele força.
// As classes de produção não têm ganchos próprios; o piloto usa as mesmas saídas que a luta de verdade usa.
public sealed class PilotPort
{
	readonly Func<string> profileId;
	readonly Func<int> level;
	readonly TrainingArea training;

	public PilotPort(ProgressServices progress, RewardService rewards, GearService gear, InventoryService inventory, MasterChallenge master, TrainingArea training, Func<string> profileId, Func<int> level)
	{
		Progress = progress;
		Rewards = rewards;
		Gear = gear;
		Inventory = inventory;
		Master = master;
		this.training = training;
		this.profileId = profileId;
		this.level = level;
	}

	public ProgressServices Progress { get; }
	public RewardService Rewards { get; }
	public GearService Gear { get; }
	public InventoryService Inventory { get; }
	public MasterChallenge Master { get; }
	public string ProfileId => profileId();
	public int Level => level();

	// Vence a Sala sem resolver o Desafio, pela mesma saída da vitória de verdade.
	public static void WinRoom(DungeonRun run)
	{
		if (!OS.IsDebugBuild()) return;
		var mission = run.Combat.CurrentMission;
		run.Combat.EndFight();
		if (mission is not null) run.OnSolved(mission);
		run.OnWon();
	}

	// Vence o Desafio da vez do Desafio mestre sem resolvê-lo.
	public void WinMasterRound()
	{
		if (!OS.IsDebugBuild() || !Master.Active) return;
		training.EndFight();
		Master.OnRoundWon();
	}

	// Zera a Energia da luta do Desafio mestre, pela mesma saída da derrota de verdade.
	public void LoseMaster()
	{
		if (!OS.IsDebugBuild() || !Master.Active) return;
		training.EndFight();
		Master.OnLost();
	}
}
