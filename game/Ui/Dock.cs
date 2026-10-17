using System;
using Godot;

namespace ArquipelagoDoCeu;

// O canto inferior esquerdo: o botão do Inventário (com o selo "novo", em texto) e o nome do Papel de agora.
// O ícone de trocar de Papel só existe em Dungeon híbrida (#139); no MVP cada Dungeon tem um só Papel.
public partial class Dock : HBoxContainer
{
	const int ButtonHeight = 72;
	const int ButtonWidth = 220;
	const int Margin = 12;

	readonly Button inventory;
	readonly Label role = BookStyle.Text("", 20);

	public Dock()
	{
		Name = "Dock";
		inventory = BookStyle.Button("Inventário", () => InventoryRequested?.Invoke(), "InventoryButton", ButtonHeight);
		inventory.CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight);
		inventory.TooltipText = "Abrir o Inventário [I]";
		AddThemeConstantOverride("separation", 8);
		AddChild(inventory);
		role.AutowrapMode = TextServer.AutowrapMode.Off;
		role.VerticalAlignment = VerticalAlignment.Center;
		var rolePanel = BookStyle.Panel(role);
		rolePanel.CustomMinimumSize = new Vector2(0, ButtonHeight);
		AddChild(rolePanel);
		SetAnchorsPreset(LayoutPreset.BottomLeft);
		GrowVertical = GrowDirection.Begin;
		OffsetLeft = Margin;
		OffsetBottom = -Margin;
		OffsetTop = -Margin - ButtonHeight;
	}

	public event Action? InventoryRequested;

	public string InventoryText => inventory.Text;

	public string RoleText => role.Text;

	public void SetRole(string name) => role.Text = name;

	// Com itens ainda não vistos o botão diz quantos são, em texto: o selo nunca é só uma cor.
	public void SetNewCount(int count)
	{
		inventory.Text = count > 0 ? $"Inventário · {(count == 1 ? "1 novo" : $"{count} novos")}" : "Inventário";
	}

	public void Press() => inventory.EmitSignal(BaseButton.SignalName.Pressed);
}
