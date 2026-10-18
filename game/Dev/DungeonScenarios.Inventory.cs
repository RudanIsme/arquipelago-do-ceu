using System.Linq;
using ArquipelagoDoCeu.Generation;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Equipment;
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
		await GrantFloors(d, (1, 1), (1, 2), (2, 3), (3, 1));
		port.RefreshDock();
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
		d.Expect(Named<Button>(d, "Slot_armor")!.Text.Contains("Explorador"), "a caixinha do espaço mostra o equipamento");

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
		await d.Wait(0.4);
		d.Expect(d.HasVisible("Só muda a aparência."), "tocar num Cosmético só mostra a prévia e o painel");
		Named<Button>(d, "WearButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.5);
		d.Expect(Named<Button>(d, "WearButton")!.Text == "Tirar", "o Cosmético vestido vira Tirar");
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

	// Cenário `modais-tempo`: quanto leva para o Inventário e o Mercador aparecerem (a abertura e os quadros até a tela desenhar).
	// A primeira abertura depois de entrar no Lobby é a que mais pesava.
	public static async Task ModalTimes(DevDriver d)
	{
		await EnsureProfile(d);
		await d.Wait(1.0);
		var dock = Named<Button>(d, "InventoryButton")!;
		await Measure(d, "Inventário (1ª vez)", () => dock.EmitSignal(BaseButton.SignalName.Pressed), "InventoryScreen");
		d.Expect(Named<PortraitService>(d, "PortraitService") is { Sleeping: false }, "com o Inventário aberto o retrato renderiza");
		await d.Press("Fechar");
		d.Expect(Named<PortraitService>(d, "PortraitService") is { Sleeping: true }, "fechar o modal desliga a renderização do retrato");
		await Measure(d, "Inventário (2ª vez)", () => dock.EmitSignal(BaseButton.SignalName.Pressed), "InventoryScreen");
		await d.Press("Fechar");
		await WalkTo(d, 0, 11);
		await Measure(d, "Mercador (1ª vez)", () => Input.ParseInputEvent(new InputEventAction { Action = Controls.Interact, Pressed = true }), "MerchantScreen");
		await d.Press("Fechar");
	}

	static async Task Measure(DevDriver d, string label, System.Action open, string screenName)
	{
		var start = Time.GetTicksUsec();
		open();
		var opened = Time.GetTicksUsec();
		for (var i = 0; i < 400 && Named<Control>(d, screenName) is null; i++) await d.ToSignal(d.GetTree(), SceneTree.SignalName.ProcessFrame);
		var shown = Time.GetTicksUsec();
		await d.ToSignal(d.GetTree(), SceneTree.SignalName.ProcessFrame);
		await d.ToSignal(d.GetTree(), SceneTree.SignalName.ProcessFrame);
		var drawn = Time.GetTicksUsec();
		GD.Print($"MODAL {label}: abrir {(opened - start) / 1000.0:0} ms, apareceu {(shown - start) / 1000.0:0} ms, 2 quadros depois {(drawn - start) / 1000.0:0} ms");
		d.Expect(Named<Control>(d, screenName) is not null, $"{label} abriu");
	}

	static T? Named<T>(DevDriver d, string name) where T : Node =>
		d.GetTree().Root.FindChildren(name, "", true, false).OfType<T>().FirstOrDefault(n => n.IsInsideTree() && !n.IsQueuedForDeletion() && (n is not CanvasItem item || item.IsVisibleInTree()));

	// Completa Andares da Torre dos Números pela mesma saída da Dungeon: cada um concede o Cosmético e o equipamento dos dados.
	static async Task GrantFloors(DevDriver d, params (int Level, int Floor)[] floors)
	{
		using var cosmetics = FileAccess.Open("res://data/cosmeticos.json", FileAccess.ModeFlags.Read);
		using var rewards = FileAccess.Open("res://data/recompensas.json", FileAccess.ModeFlags.Read);
		using var equipment = FileAccess.Open("res://data/equipamentos.json", FileAccess.ModeFlags.Read);
		var gear = GearCatalog.Parse(equipment.GetAsText(), GearEffects.Default).Value!;
		var catalog = CosmeticCatalog.Parse(cosmetics.GetAsText()).Value!;
		var parsed = RewardCatalog.Parse(rewards.GetAsText(), RewardTriggers.Default, catalog, gear).Value!;
		foreach (var (level, floor) in floors)
			d.Expect(d.Port.Rewards.Grant(d.Port.ProfileId, new RewardContext("floor-complete", "matematica", level, floor), parsed).IsOk, $"o Andar {floor} do Nível {level} rende o prêmio");
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
