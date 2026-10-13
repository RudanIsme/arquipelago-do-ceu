using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Ferramenta de desenvolvimento: percorre uma tela como uma criança faria (clica e digita) e grava
// capturas, para conferir fluxos de interface sem teste manual. Só liga com argumentos de usuário:
//   godot --path game -- --driver=criar --saida=<pasta>
public partial class DevDriver : Node
{
	string scenario = "";
	string outputDir = "";

	public static void AttachIfRequested(Node parent)
	{
		var args = OS.GetCmdlineUserArgs();
		var scenario = args.FirstOrDefault(a => a.StartsWith("--driver="))?["--driver=".Length..];
		if (scenario is null) return;
		var output = args.FirstOrDefault(a => a.StartsWith("--saida="))?["--saida=".Length..] ?? ".";
		parent.AddChild(new DevDriver { scenario = scenario, outputDir = output });
	}

	public override async void _Ready()
	{
		await Wait(0.6);
		await Scenarios.Run(this, scenario);
		GetTree().Quit();
	}

	public async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	public async Task Shot(string name)
	{
		await Wait(0.25);
		GetViewport().GetTexture().GetImage().SavePng($"{outputDir}/{name}.png");
	}

	public Button? Button(string text) =>
		GetTree().Root.FindChildren("*", "Button", true, false).Cast<Button>().FirstOrDefault(b => b.Text == text && b.IsVisibleInTree());

	public async Task Press(string text)
	{
		var button = Button(text);
		if (button is null) GD.PushError($"Piloto: botão '{text}' não encontrado.");
		else button.EmitSignal(BaseButton.SignalName.Pressed);
		await Wait(0.3);
	}

	public async Task Type(string text)
	{
		var input = GetTree().Root.FindChildren("*", "LineEdit", true, false).Cast<LineEdit>().FirstOrDefault(l => l.IsVisibleInTree());
		if (input is null) GD.PushError("Piloto: campo de texto não encontrado.");
		else input.Text = text;
		await Wait(0.2);
	}
}

static class Scenarios
{
	public static async Task Run(DevDriver d, string name)
	{
		switch (name)
		{
			case "criar":
				await d.Shot("01-criar");
				await d.Press("Criar");
				await d.Shot("02-apelido-vazio");
				await d.Type("Lu@na");
				await d.Press("Criar");
				await d.Shot("03-apelido-invalido");
				await d.Type("Luna");
				await d.Press("Criar");
				await d.Shot("04-lobby");
				break;
			case "escolher":
				await d.Shot("01-lista");
				await d.Press("Editar");
				await d.Shot("02-editar");
				await d.Type("Luninha");
				await d.Press("Salvar");
				await d.Shot("03-lista-depois");
				await d.Press("Theo");
				await d.Shot("04-lobby");
				break;
			default:
				GD.PushError($"Piloto: cenário '{name}' não existe.");
				break;
		}
	}
}
