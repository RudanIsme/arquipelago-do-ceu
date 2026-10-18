using System;
using Godot;

namespace ArquipelagoDoCeu;

// O canto inferior esquerdo: o botão do Inventário (com o selo "novo", em texto) e o nome do Papel de agora.
// O botão do Papel troca entre o Matemático e o Escriba no Lobby; dentro de uma Dungeon fica desligado, porque cada torre tem um Papel só.
public partial class Dock : HBoxContainer
{
	const int ButtonHeight = 72;
	const int ButtonWidth = 220;
	const int Margin = 12;

	readonly Button inventory;
	readonly Button role;
	string roleName = "";

	public Dock()
	{
		Name = "Dock";
		inventory = BookStyle.Button("Inventário", () => InventoryRequested?.Invoke(), "InventoryButton", ButtonHeight);
		inventory.CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight);
		inventory.TooltipText = "Abrir o Inventário [I]";
		AddThemeConstantOverride("separation", 8);
		AddChild(inventory);
		role = BookStyle.Button("", () => RoleSwitchRequested?.Invoke(), "RoleButton", ButtonHeight);
		role.Icon = Icons.For("swap", 28);
		role.IconAlignment = HorizontalAlignment.Right;
		role.AddThemeFontSizeOverride("font_size", 20);
		role.TooltipText = "Trocar de Papel";
		AddChild(role);
		SetAnchorsPreset(LayoutPreset.BottomLeft);
		GrowVertical = GrowDirection.Begin;
		OffsetLeft = Margin;
		OffsetBottom = -Margin;
		OffsetTop = -Margin - ButtonHeight;
	}

	public event Action? InventoryRequested;

	public string InventoryText => inventory.Text;

	public event Action? RoleSwitchRequested;

	public string RoleText => roleName;

	public void SetRole(string name)
	{
		roleName = name;
		role.Text = name;
	}

	// Dentro de uma Dungeon o Papel é o da torre: o botão só mostra o nome.
	public void LockRole()
	{
		role.Disabled = true;
		role.Icon = null;
		role.TooltipText = "Esta torre tem um Papel só";
	}

	// Com itens ainda não vistos o botão diz quantos são, em texto: o selo nunca é só uma cor.
	public void SetNewCount(int count)
	{
		inventory.Text = count > 0 ? $"Inventário · {(count == 1 ? "1 novo" : $"{count} novos")}" : "Inventário";
	}

	public void Press() => inventory.EmitSignal(BaseButton.SignalName.Pressed);
}
