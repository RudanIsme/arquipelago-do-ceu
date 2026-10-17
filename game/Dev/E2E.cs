using System;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Roteiro de ponta a ponta para o executável do build (CI): cria o Perfil, treina e confere que o save não mudou.
// Uso: <jogo> --headless -- --driver=e2e-1 --save=<arquivo>, e depois e2e-2 reabre e confere.
static class E2E
{
	public static async Task First(DevDriver d)
	{
		var save = d.Arg("save");
		d.Expect(save.Length != 0, "o roteiro recebeu --save=<arquivo>");

		await d.Press("Criar");
		await d.Type("Luna");
		await d.Press("Criar");
		d.Expect(d.Player is { Enabled: true } && d.Button("Perfil") is not null, "o Perfil foi criado e o Lobby abriu");
		d.Expect(File.Exists(save), "o Perfil foi gravado no save");
		var before = File.ReadAllBytes(save);

		var start = d.Player!.GroundPosition;
		// Sem mouse: o teste anda e escolhe a criatura só com ações, que funcionam também sem janela (--headless).
		await d.Hold(Controls.Down, 1.5);
		d.Expect(System.Numerics.Vector2.Distance(start, d.Player.GroundPosition) > 0.5f, "o Personagem andou");

		await d.Tap(Controls.Target);
		d.Expect(d.Button("Começar") is not null, "a escolha do treino abriu");
		await d.Press("Começar");
		d.Expect(d.Button("Atacar") is not null, "o treino de Matemática começou");
		await d.PressNumberKey();
		await d.PressKey(Key.Escape);
		await d.Press("Sair do treino");

		await d.Tap(Controls.Target);
		await d.Press("Escriba");
		await d.Press("Começar");
		d.Expect(d.Button("Conferir") is not null, "o treino de Português começou");
		await d.TypeName();
		await d.PressKey(Key.Enter);
		d.Expect(d.HasVisible("libertada") || d.HasVisible("certa"), "o nome certo libertou a criatura");
		await d.Press("Sair do treino");

		d.Expect(File.ReadAllBytes(save).AsSpan().SequenceEqual(before), "treinar nos dois Papéis não mudou o save");
		File.WriteAllBytes(save + ".snapshot", File.ReadAllBytes(save));
	}

	public static async Task Reopen(DevDriver d)
	{
		var save = d.Arg("save");
		await d.Wait(0.5);
		d.Expect(d.Player is { Enabled: true } && d.Button("Perfil") is not null, "o Perfil foi aberto sozinho ao reabrir o jogo");
		d.Expect(d.HasVisible("Luna"), "o apelido do Perfil continua o mesmo");
		d.Expect(File.Exists(save + ".snapshot") && File.ReadAllBytes(save).AsSpan().SequenceEqual(File.ReadAllBytes(save + ".snapshot")), "o progresso continua idêntico depois de fechar e reabrir");
	}
}
