using Godot;

namespace ArquipelagoDoCeu;

// Álbum: criaturas e Cosméticos. Ainda não há nada para guardar, então as duas abas só mostram
// uma mensagem amigável.
public partial class AlbumScreen : OverlayScreen
{
	string tab = "criaturas";

	public override void _Ready()
	{
		base._Ready();
		Show(tab);
	}

	void Show(string current)
	{
		tab = current;
		var box = UiKit.Card(this, "Álbum");

		var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		tabs.AddThemeConstantOverride("separation", 12);
		var group = new ButtonGroup();
		foreach (var (id, name) in new[] { ("criaturas", "Criaturas"), ("cosmeticos", "Cosméticos") })
		{
			var tabId = id;
			var button = UiKit.BigButton(name, () => Show(tabId), 220);
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = id == current;
			tabs.AddChild(button);
		}
		box.AddChild(tabs);

		box.AddChild(UiKit.Hint(current == "criaturas"
			? "Você ainda não encontrou nenhuma criatura. Explore as torres para descobrir!"
			: "Você ainda não ganhou nenhum Cosmético. Aprender e explorar rendem novidades!"));
		box.AddChild(UiKit.BigButton("Fechar", Close));
	}
}

// Portal de viagem rápida: lista os pontos já descobertos; por enquanto não há nenhum.
public partial class PortalScreen : OverlayScreen
{
	public override void _Ready()
	{
		base._Ready();
		var box = UiKit.Card(this, "Portal de viagem");
		box.AddChild(UiKit.Hint("Ainda não há nenhum ponto de viagem. Quando você chegar a um ponto de uma torre, ele aparece aqui."));
		box.AddChild(UiKit.BigButton("Fechar", Close));
	}
}
