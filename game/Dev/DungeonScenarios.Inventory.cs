using System.Linq;
using ArquipelagoDoCeu.Generation;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// Cenário `inventario` do piloto: o dock, o selo "novo", o guia, a comparação, o bloqueio por Maestria e a aba Cosméticos.
static partial class DungeonScenarios
{
	public static async Task Inventory(DevDriver d)
	{
		await EnsureProfile(d);
		var port = d.Port;
		var profile = port.ProfileId;
		var dock = Named<Button>(d, "InventoryButton");
		d.Expect(dock is not null && dock.Text.Contains("novos"), "o botão do Inventário mostra o selo novo, em texto");
		d.Expect(d.HasVisible("Matemático"), "o dock mostra o nome do Papel");
		port.Progress.Mastery.Record(profile, new ConceptKey("matematica", "contagem"), 1);
		await d.Shot("01-lobby-com-dock");

		dock!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.8);
		d.Expect(Named<Control>(d, "InventoryScreen") is not null, "o botão do Inventário abre o Inventário");
		d.Expect(Named<Control>(d, "InventoryGuide") is not null, "o guia abre na primeira vez");
		await d.Shot("02-guia");
		await d.Press("Pular");
		d.Expect(Named<Control>(d, "InventoryGuide") is null, "Pular fecha o guia");

		Named<Button>(d, "Gear_scout")!.GrabFocus();
		await d.Wait(0.4);
		d.Expect(d.HasVisible("▲ Velocidade"), "a comparação mostra só o que muda, com ▲ e números");
		d.Expect(!d.HasVisible("Alcance:"), "atributo igual não aparece");
		d.Expect(port.Gear.Get(profile, "matematica").In("armor") is null, "a prévia não muda nada de verdade");
		await d.Shot("03-comparacao");

		Named<Button>(d, "Gear_guardian")!.GrabFocus();
		await d.Wait(0.4);
		d.Expect(d.HasVisible("Bloqueado: precisa de Maestria 3"), "o equipamento que pede mais Maestria mostra o número");
		d.Expect(Named<Button>(d, "EquipButton")!.Disabled, "o botão Equipar não funciona no bloqueado");
		await d.Shot("04-bloqueado");

		Named<Button>(d, "Gear_scout")!.GrabFocus();
		await d.Wait(0.3);
		Named<Button>(d, "EquipButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		d.Expect(port.Gear.Get(profile, "matematica").In("armor") == "scout", "Equipar veste de verdade");
		d.Expect(HasButtonText(d, "Armadura: Explorador"), "a caixinha do espaço mostra o equipamento");

		await GiveCosmetic(d);
		await d.Press("Fechar");
		d.Expect(Named<Control>(d, "InventoryScreen") is null, "Fechar volta ao Lobby");
		dock!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.6);
		d.Expect(Named<Control>(d, "InventoryGuide") is null, "o guia não abre de novo");
		Named<Button>(d, "GuideButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		d.Expect(Named<Control>(d, "InventoryGuide") is not null, "o ? permite rever o guia");
		await d.Press("Pular");

		Named<Button>(d, "TabCosmetics")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.5);
		await d.Shot("05-cosmeticos");
		Named<Button>(d, "Cosmetic_matematica-1-1")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.5);
		d.Expect(HasButtonText(d, "Vestido ✓"), "o Cosmético vestido aparece marcado");
		d.Expect(port.Gear.Get(profile, "matematica").In("armor") == "scout", "vestir um Cosmético não mexe no equipamento");
		await d.Shot("06-cosmetico-vestido");
		await d.Press("Fechar");
		await InventoryInDungeon(d);
	}

	// Na Dungeon o dock também abre o Inventário, mas só os Cosméticos mudam, e na luta ele some.
	static async Task InventoryInDungeon(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var dock = Named<Button>(d, "InventoryButton");
		d.Expect(dock is not null, "a Dungeon tem o dock do Inventário");
		dock!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.8);
		d.Expect(Named<Control>(d, "InventoryScreen") is not null, "o dock da Dungeon abre o Inventário");
		Named<Button>(d, "Gear_lantern")!.GrabFocus();
		await d.Wait(0.3);
		d.Expect(Named<Button>(d, "EquipButton")!.Disabled && d.HasVisible("Troque o equipamento no Lobby."), "na Dungeon o equipamento não muda");
		await d.Shot("07-inventario-na-dungeon");
		await d.Press("Fechar");

		await GoTo(d, run, RoomTypes.Combat);
		run.Player.PlaceAt(new System.Numerics.Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		d.Expect(run.Combat.HasTarget && Named<Button>(d, "InventoryButton") is null, "na luta o dock do Inventário some");
		await d.Tap(Controls.Inventory);
		d.Expect(Named<Control>(d, "InventoryScreen") is null, "a tecla I não abre o Inventário na luta");
		run.Leave("");
	}

	static bool HasButtonText(DevDriver d, string text) =>
		d.GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.IsVisibleInTree() && b.Text.Contains(text, System.StringComparison.Ordinal));

	static T? Named<T>(DevDriver d, string name) where T : Node =>
		d.GetTree().Root.FindChildren(name, "", true, false).OfType<T>().FirstOrDefault(n => n.IsInsideTree() && !n.IsQueuedForDeletion() && (n is not CanvasItem item || item.IsVisibleInTree()));

	// Entrega o primeiro Cosmético da Torre dos Números, pela mesma saída que a Dungeon usa.
	static async Task GiveCosmetic(DevDriver d)
	{
		using var cosmetics = FileAccess.Open("res://data/cosmeticos.json", FileAccess.ModeFlags.Read);
		using var rewards = FileAccess.Open("res://data/recompensas.json", FileAccess.ModeFlags.Read);
		var catalog = CosmeticCatalog.Parse(cosmetics.GetAsText()).Value!;
		var parsed = RewardCatalog.Parse(rewards.GetAsText(), RewardTriggers.Default, catalog).Value!;
		var context = new RewardContext("floor-complete", "matematica", 1, 1);
		d.Expect(d.Port.Rewards.Grant(d.Port.ProfileId, context, parsed).IsOk, "o Andar completo rende um Cosmético");
		await d.Wait(0.2);
	}

	// Cenário `cosmeticos`: veste uma peça de cada tipo e grava o Personagem de frente e de costas, para a revisão da aparência.
	public static async Task Cosmetics(DevDriver d)
	{
		await EnsureProfile(d);
		using var file = FileAccess.Open("res://data/cosmeticos.json", FileAccess.ModeFlags.Read);
		var all = CosmeticCatalog.Parse(file.GetAsText()).Value!.All;
		var ids = all.Select(c => c.Id).ToList();
		var profile = d.Port.ProfileId;
		Named<Button>(d, "InventoryButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.6);
		await d.Press("Pular");
		foreach (var cosmetic in all.GroupBy(c => c.Look!.Kind).Select(g => g.First()))
		{
			foreach (var worn in d.Port.Inventory.Worn(profile)) d.Port.Inventory.Remove(profile, worn);
			d.Port.Inventory.Wear(profile, cosmetic.Id, ids);
			Named<Button>(d, "TabGear")!.EmitSignal(BaseButton.SignalName.Pressed);
			Named<Button>(d, "TabCosmetics")!.EmitSignal(BaseButton.SignalName.Pressed);
			await d.Shot($"{cosmetic.Look!.Kind}-frente");
			Named<Button>(d, "PortraitTurnRight")!.EmitSignal(BaseButton.SignalName.Pressed);
			Named<Button>(d, "PortraitTurnRight")!.EmitSignal(BaseButton.SignalName.Pressed);
			Named<Button>(d, "PortraitTurnRight")!.EmitSignal(BaseButton.SignalName.Pressed);
			Named<Button>(d, "PortraitTurnRight")!.EmitSignal(BaseButton.SignalName.Pressed);
			Named<Button>(d, "PortraitTurnRight")!.EmitSignal(BaseButton.SignalName.Pressed);
			await d.Shot($"{cosmetic.Look.Kind}-costas");
			for (var i = 0; i < 5; i++) Named<Button>(d, "PortraitTurnLeft")!.EmitSignal(BaseButton.SignalName.Pressed);
		}
	}
}
