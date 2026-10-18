using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// O retrato do Personagem dentro de uma tela: só mostra a imagem do PortraitService, que é único e dorme quando nenhuma tela o usa.
public partial class CharacterPortrait : VBoxContainer
{
	readonly PortraitService service;
	readonly TextureRect picture = new() { Name = "PortraitPicture", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(120, 200) };
	EquipmentLoadout? last;

	public CharacterPortrait(PortraitService service)
	{
		Name = "CharacterPortrait";
		this.service = service;
	}

	public override void _EnterTree()
	{
		service.Built += ShowTexture;
		service.Acquire();
	}

	public override void _Ready()
	{
		AddChild(picture);
		ShowTexture();
		if (last is not null) service.Show(last);
	}

	public override void _ExitTree()
	{
		service.Built -= ShowTexture;
		service.Release();
	}

	public void Show(EquipmentLoadout loadout)
	{
		last = loadout;
		service.Show(loadout);
	}

	void ShowTexture() => picture.Texture = service.Texture;

}
