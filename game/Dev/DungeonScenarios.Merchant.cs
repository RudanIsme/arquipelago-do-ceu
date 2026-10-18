using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Cenário `mercador` do piloto: o Mercador do Lobby compra, vende em lote, recompra e rotaciona, sempre com confirmação.
static partial class DungeonScenarios
{
	public static async Task Merchant(DevDriver d)
	{
		await EnsureProfile(d);
		var port = d.Port;
		var profile = port.ProfileId;
		await GrantFloors(d, (1, 1), (1, 2));
		port.Progress.Wallet.Add(profile, 40);
		var start = port.Merchant.Balance(profile);

		await WalkTo(d, 0, 11);
		await d.Tap(Controls.Interact);
		await d.Wait(0.8);
		d.Expect(Named<Control>(d, "MerchantScreen") is not null, "chegar perto do Mercador e interagir abre a tela dele");
		d.Expect(d.HasVisible($"Saldo: {start} moedas"), "o saldo aparece escrito");
		d.Expect(Named<Button>(d, "RotateButton")!.Disabled && HasButtonText(d, "sem carga"), "sem carga o Rotacionar fica desligado e diz isso em texto");
		await d.Shot("01-mercador");

		var offer = port.Merchant.Stock(profile)[0];
		d.Expect(Named<Control>(d, "MerchantDetail") is null, "sem item escolhido o painel de detalhes não existe");
		Named<Button>(d, "Offer_" + offer.Id)!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.5);
		d.Expect(Named<Control>(d, "MerchantDetail") is not null && Named<Control>(d, "ItemPreview") is not null && Named<Control>(d, "PortraitPicture") is null, "escolher uma oferta abre o painel com o item, sem o retrato do Personagem");
		d.Expect(Named<Label>(d, "Summary")!.Text.Contains("moedas") && Named<Button>(d, "ActionButton")!.Text.StartsWith("Comprar"), "o painel mostra o total e o botão Comprar");
		await d.Shot("01b-previa-do-item");
		Named<Button>(d, "ActionButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		d.Expect(Named<Control>(d, "PaymentDialog") is not null && d.Button("Eu pago") is not null && d.Button("Pagar sozinho") is not null, "comprar pergunta como pagar: Eu pago ou Pagar sozinho");
		d.Expect(!port.Rewards.Cosmetics(profile).Contains(offer.Id), "antes de confirmar nada é comprado");
		await d.Shot("02-confirmar-compra");
		await d.Press("Pagar sozinho");
		await d.Wait(0.4);
		d.Expect(port.Rewards.Cosmetics(profile).Contains(offer.Id) && d.HasVisible($"Saldo: {start - offer.Price} moedas"), "confirmar compra o Cosmético e desconta o preço");

		Named<Button>(d, "TabSell")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.3);
		Named<Button>(d, "Sell_scout")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.3);
		Named<Button>(d, "Sell_lantern")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.3);
		d.Expect(HasButtonText(d, "Vender (2)"), "a seleção em lote mostra quantos e quanto");
		await d.Shot("02b-venda-em-lote");
		Named<Button>(d, "ActionButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		d.Expect(Named<Control>(d, "PaymentDialog") is not null, "vender pergunta como pagar");
		await d.Press("Pagar sozinho");
		await d.Wait(0.4);
		Named<Button>(d, "TabRebuy")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.3);
		d.Expect(!port.Inventory.OwnedGear(profile).Contains("scout") && Named<Button>(d, "Rebuy_scout") is not null, "o vendido vai para a aba Recomprar");
		await d.Shot("03-recomprar");

		var before = port.Merchant.Balance(profile);
		Named<Button>(d, "Rebuy_scout")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.3);
		Named<Button>(d, "ActionButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		await d.Press("Eu pago");
		await d.Wait(0.4);
		d.Expect(d.HasVisible("troco"), "na recompra o Eu pago pede o troco da nota");
		await d.Shot("03b-eu-pago");
		Named<Button>(d, "Money_500")!.EmitSignal(BaseButton.SignalName.Pressed);
		Named<Button>(d, "PayCheck")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		d.Expect(!port.Inventory.OwnedGear(profile).Contains("scout") && d.HasVisible("Passou do valor"), "valor errado no Eu pago não custa nada e deixa tentar de novo");
		Named<Button>(d, "PayUndo")!.EmitSignal(BaseButton.SignalName.Pressed);
		Named<Button>(d, "PaySuggest")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.3);
		d.Expect(d.HasVisible("Tente 1 real"), "a dica diz a próxima peça sem custo");
		Named<Button>(d, "Money_100")!.EmitSignal(BaseButton.SignalName.Pressed);
		Named<Button>(d, "PayCheck")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		d.Expect(port.Inventory.OwnedGear(profile).Contains("scout") && port.Merchant.Balance(profile) < before, "recomprar devolve o equipamento por moedas");

		port.Merchant.AddCharge(profile);
		await d.Press("Fechar");
		await d.Tap(Controls.Interact);
		await d.Wait(0.8);
		d.Expect(HasButtonText(d, "1 carga") && !Named<Button>(d, "RotateButton")!.Disabled, "com carga o Rotacionar liga");
		var oldStock = port.Merchant.Stock(profile).Select(o => o.Id).ToList();
		Named<Button>(d, "RotateButton")!.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.4);
		await d.Press("Sim");
		await d.Wait(0.4);
		d.Expect(!oldStock.SequenceEqual(port.Merchant.Stock(profile).Select(o => o.Id)) && Named<Button>(d, "RotateButton")!.Disabled, "Rotacionar troca o estoque e gasta a carga");
		await d.Shot("04-estoque-novo");
		await d.Press("Fechar");
	}
}
