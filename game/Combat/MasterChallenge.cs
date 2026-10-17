using System;
using System.Linq;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// O Desafio mestre, no Lobby: Desafios de Matemática e Português do Nível contra o Guardião, com Energia de Expedição.
// Vencer sobe o Nível nas duas torres; perder não pune.
public partial class MasterChallenge : Node
{
	readonly GameRules rules;
	readonly IRandomSource random;
	readonly RewardService granter;
	readonly Func<RewardCatalog> rewards;
	readonly TrainingArea training;
	readonly Control uiRoot;
	readonly ProgressServices progress;
	readonly IReadOnlyList<Mission> missions = MissionSource.Load().Missions;
	readonly Func<int> extraEnergy;
	readonly Action<string> notify;
	readonly Action<ProfileSave> levelRaised;
	IReadOnlyList<TrainingSetup> rounds = [];
	EnergyView? energyView;
	Energy? energy;
	string profileId = "";
	int level;
	int round;

	public MasterChallenge(GameRules rules, TrainingArea training, Control uiRoot, ProgressServices progress, Func<int> extraEnergy,
		Action<string> notify, Action<ProfileSave> levelRaised, IRandomSource random, RewardService granter, Func<RewardCatalog> rewards)
	{
		this.granter = granter;
		this.rewards = rewards;
		this.random = random;
		this.rules = rules;
		this.training = training;
		this.uiRoot = uiRoot;
		this.progress = progress;
		this.extraEnergy = extraEnergy;
		this.notify = notify;
		this.levelRaised = levelRaised;
		// Trocar de Perfil ou entrar numa torre no meio do desafio o encerra, sem punir a criança.
		training.FightAborted += () => { if (Active) Finish("O Desafio mestre foi pausado. Ele continua esperando por você."); };
	}

	public bool Active => energyView is not null;

	public int Round => round;

	public int Rounds => rounds.Count;

	// Começa o Desafio mestre do Nível; devolve o que impediu, se algo impediu.
	public string? Start(string profile, int masterLevel)
	{
		if (Active) return "O Desafio mestre já está em andamento.";
		var setups = DungeonCombat.MasterSetups(missions, masterLevel, rules.Dungeon.MasterChallenges, random.NewStream());
		if (!setups.IsOk) return setups.Error;

		training.End();
		profileId = profile;
		level = masterLevel;
		rounds = setups.Value!;
		round = 0;
		energy = new Energy(rules.MaxEnergy + extraEnergy());
		energyView = new EnergyView("Desistir do desafio");
		energyView.LeaveRequested += Quit;
		uiRoot.AddChild(energyView);
		training.EnergyTop = 154;
		training.Creature = CreatureWords.Guardian;
		BeginRound();
		return null;
	}

	void BeginRound()
	{
		var setup = rounds[round];
		var fight = CombatSession.Start(rules.Combat, energy!, level, setup, missions, random.NewStream());
		if (!fight.IsOk)
		{
			Finish($"Não foi possível montar o Desafio mestre: {fight.Error}");
			return;
		}
		training.BeginFight(fight.Value!, energyView!, OnRoundWon, OnLost);
		notify($"Desafio mestre! Desafio {round + 1} de {rounds.Count}. Chegue perto do Guardião e escolha-o (clique ou F).");
	}

	void OnRoundWon()
	{
		round++;
		if (round < rounds.Count)
		{
			BeginRound();
			return;
		}

		var raised = progress.Levels.RaiseLevel(profileId, TrainingSetup.LastLevel);
		var message = !raised.IsOk
			? "Você venceu o Desafio mestre, mas não foi possível guardar o novo Nível agora. Tente de novo."
			: raised.Value!.Level > level
				? $"Desafio mestre vencido! As duas torres liberaram o Nível {raised.Value.Level}."
				: "Desafio mestre vencido! Você chegou ao último Nível do jogo. Parabéns!";
		if (raised.IsOk) levelRaised(raised.Value!);
		Finish(raised.IsOk ? $"{message}{PrizeText()}" : message);
	}

	// O que o Desafio mestre rende, se os dados mandarem alguma coisa (uma vez por Nível).
	string PrizeText()
	{
		var granted = granter.Grant(profileId, new RewardContext("master-challenge", "", this.level), rewards());
		if (!granted.IsOk || granted.Value is not { Fired: true } outcome) return "";
		var coins = outcome.Coins > 0 ? $" Você ganhou {outcome.Coins} moedas." : "";
		return coins + string.Concat(outcome.Cosmetics.Select(c => $" Você ganhou um Cosmético: {c.Name}."));
	}

	void OnLost() => Finish("A névoa ficou forte demais desta vez. O Desafio mestre continua esperando: tente de novo quando quiser!");

	void Quit()
	{
		training.EndFight();
		Finish("Você parou o Desafio mestre. Ele continua esperando por você.");
	}

	void Finish(string message)
	{
		training.Creature = CreatureWords.Dummy;
		training.EnergyTop = null;
		energyView?.QueueFree();
		energyView = null;
		energy = null;
		notify(message);
	}

	// Só para o piloto de desenvolvimento: zera a Energia da luta, pela mesma saída da derrota de verdade.
	public void DevLose()
	{
		if (!OS.IsDebugBuild() || !Active) return;
		training.EndFight();
		OnLost();
	}

	// Só para o piloto de desenvolvimento: vence o Desafio da vez sem resolvê-lo, pela mesma saída da vitória de verdade.
	public void DevWinRound()
	{
		if (!OS.IsDebugBuild() || !Active) return;
		training.EndFight();
		OnRoundWon();
	}
}
