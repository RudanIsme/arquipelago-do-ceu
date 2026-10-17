using ArquipelagoDoCeu.Combat;
using Godot;

namespace ArquipelagoDoCeu;

// Como o Boneco e o Personagem reagem à resposta de cada Mecânica: raio, escudo, mensagem e o desfecho da luta.
// A luta em si (Energia, travessura, vitória) continua na TrainingArea; aqui só se escolhe o que mostrar e dizer.
public sealed class AnswerReactions
{
	readonly TrainingArea area;
	readonly TrainingDummy dummy;
	readonly Player player;

	public AnswerReactions(TrainingArea area, TrainingDummy dummy, Player player)
	{
		this.area = area;
		this.dummy = dummy;
		this.player = player;
	}

	Vector3 RayOrigin => player.GlobalPosition + Vector3.Up * 1f;

	// Matemático
	public void Attacked(AttackResult result)
	{
		if (area.Session is not { } session) return;
		var from = RayOrigin;
		area.AttackAnimation();
		switch (result.Outcome)
		{
			case HitOutcome.Dissolved:
				dummy.Ray(from, perfect: true);
				dummy.Dissolved();
				session.Hit(perfect: true);
				area.UpdateEnergy();
				area.Say("Golpe perfeito!", $"Golpe perfeito! {result.Equation} dissolveu a névoa. Você recuperou Energia.");
				area.Won();
				break;
			case HitOutcome.Reduced:
				dummy.Ray(from, perfect: false);
				dummy.Reduced(result.Remaining);
				session.Hit(perfect: false);
				area.UpdateEnergy();
				area.Say($"Acertou! Faltam {result.Remaining}.", $"Acertou! {result.Equation}. Faltam {result.Remaining}.");
				break;
			default:
				dummy.Overshoot();
				area.Mistake("Passou do valor!", $"Passou do valor: {result.Equation} é mais que {result.Remaining}.");
				break;
		}
	}

	// Escriba: sílabas, frase ou lacuna
	public void NameChecked(NameVerdict verdict)
	{
		if (area.Session is not { Name: { } name } session) return;
		var from = RayOrigin;
		var strikes = verdict.NewStrikes;
		if (strikes > 0)
		{
			area.AttackAnimation();
			dummy.Ray(from, perfect: verdict.Freed);
			dummy.Reduced(name.Remaining);
		}
		var piece = name.Kind == NameKind.Syllables ? "sílaba" : "palavra";
		var struck = strikes switch { 0 => "", 1 => $"1 {piece} certa, 1 golpe! ", _ => $"{strikes} {piece}s certas, {strikes} golpes! " };

		if (verdict.Freed)
		{
			dummy.Dissolved();
			session.Hit(perfect: false);
			area.UpdateEnergy();
			var done = name.Kind switch
			{
				NameKind.Sentence => $"Muito bem! A frase ficou assim: {name.Completed}",
				NameKind.Gap => $"Muito bem! A frase completa é: {name.Completed}",
				_ => $"Muito bem! O nome é {name.Word.ToUpperInvariant()}. A criatura foi libertada.",
			};
			area.Say(name.Kind == NameKind.Syllables ? "Nome certo!" : "Frase certa!", $"{struck}{done}");
			area.Won();
		}
		else if (verdict.Mistaken)
		{
			dummy.Overshoot();
			area.Mistake(name.Kind switch { NameKind.Sentence => "Fora de ordem!", NameKind.Gap => "Palavra errada!", _ => "Sílaba errada!" },
				$"{struck}{name.Kind switch { NameKind.Sentence => "Uma palavra está fora do lugar.", NameKind.Gap => "Essa palavra não completa a frase.", _ => "Uma sílaba está errada." }}");
		}
		else area.Tell(strikes > 0 ? $"{struck}Faltam {name.Remaining}." : $"Ainda faltam {piece}s. Escolha mais uma.");
	}

	// Pagar e dar troco: o valor montado bate com o pedido ou não; errar é erro e mostra se passou ou faltou.
	public void ChangeChecked(ChangeResult result)
	{
		if (area.Session is not { Change: { } change } session) return;
		var from = RayOrigin;
		area.AttackAnimation();
		if (result.Correct)
		{
			dummy.Ray(from, perfect: true);
			dummy.Dissolved();
			session.Hit(perfect: true);
			area.UpdateEnergy();
			var done = change.Task == MoneyTask.Pay ? $"Você pagou {MoneyCatalog.Format(result.Target)}." : $"O troco é {MoneyCatalog.Format(result.Target)}.";
			area.Say("Valor certo!", $"Muito bem! {done} A criatura foi acalmada.");
			area.Won();
			return;
		}
		dummy.Overshoot();
		var lead = result.Total > result.Target ? "Passou do valor." : "Ainda falta dinheiro.";
		area.Mistake(result.Total > result.Target ? "Passou do valor!" : "Faltou dinheiro!", $"{lead} Você montou {MoneyCatalog.Format(result.Total)}.");
	}

	// O Escriba do 2º ano: a palavra inteira, com a grafia exata. Acertar liberta a criatura; errar é erro.
	public void WordChecked(WordResult result)
	{
		if (area.Session is not { Word: { } word } session) return;
		var from = RayOrigin;
		area.AttackAnimation();
		if (result == WordResult.Freed)
		{
			dummy.Ray(from, perfect: true);
			dummy.Dissolved();
			session.Hit(perfect: false);
			area.UpdateEnergy();
			area.Say("Nome certo!", $"Muito bem! Você escreveu {word.Word.ToUpperInvariant()}. A criatura foi libertada.");
			area.Won();
			return;
		}
		dummy.Overshoot();
		area.Mistake("Grafia errada!", $"Você escreveu {word.Typed.ToUpperInvariant()}, mas não é assim que se escreve.");
	}
}
