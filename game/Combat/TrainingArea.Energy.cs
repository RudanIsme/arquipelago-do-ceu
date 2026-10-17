using System;

namespace ArquipelagoDoCeu;

// A Energia da luta: o bônus do equipamento e a barra na tela.
public partial class TrainingArea
{
	int extraEnergy;

	// A Energia extra do equipamento do Papel escolhido, antes de ele valer para o resto do jogo.
	public Func<string, int>? EnergyBonusFor { get; set; }

	public void SetEquipmentEnergy(int bonus)
	{
		extraEnergy = Math.Clamp(bonus, 0, 2);
		session?.Energy.SetMaximum(rules.MaxEnergy + extraEnergy);
		UpdateEnergy();
	}

	internal void UpdateEnergy()
	{
		if (session is not null) energyView?.Set(session.Energy.Current, session.Energy.Max);
	}
}
