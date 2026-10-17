using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Trade;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class MerchantTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-merchant-" + Guid.NewGuid().ToString("N"));

	static string Data(string name) => File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", name));

	static readonly GearCatalog Gear = GearCatalog.Parse(Data("equipamentos.json"), GearEffects.Default).Value!;

	static readonly CosmeticCatalog Cosmetics = CosmeticCatalog.Parse(Data("cosmeticos.json")).Value!;

	static readonly MerchantCatalog Catalog = MerchantCatalog.Parse(Data("mercador.json"), Cosmetics, Gear).Value!;

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	(MerchantService Merchant, ProgressStore Store, SaveSession Session) Start(int coins = 100, IReadOnlyList<string>? equipment = null)
	{
		var progress = new ProgressSave(Coins: coins, Equipment: equipment ?? []);
		var session = new SaveSession(new SaveStore(Path.Combine(folder, "save.json")), new SaveData(1, [new ProfileSave("p", "Luna", 1, Progress: progress)]));
		var store = new ProgressStore(session);
		return (new MerchantService(store, Catalog), store, session);
	}

	static int Coins(ProgressStore store) => store.Of("p")!.Coins ?? 0;

	[Fact]
	public void O_estoque_comeca_com_os_primeiros_Cosmeticos_do_catalogo_na_ordem_dos_dados()
	{
		var (merchant, _, _) = Start();

		var stock = merchant.Stock("p");

		Assert.Equal(Catalog.StockSize, stock.Count);
		Assert.Equal(Catalog.Cosmetics.Take(Catalog.StockSize).Select(o => o.Id), stock.Select(o => o.Id));
	}

	[Fact]
	public void Comprar_gasta_moedas_e_entrega_o_Cosmetico()
	{
		var (merchant, store, _) = Start(coins: 100);
		var offer = merchant.Stock("p")[0];

		var bought = merchant.Buy("p", offer.Id);

		Assert.True(bought.IsOk);
		Assert.Equal(100 - offer.Price, Coins(store));
		Assert.Contains(offer.Id, store.Of("p")!.Cosmetics!);
	}

	[Fact]
	public void O_saldo_nunca_fica_negativo_e_sem_moedas_nao_se_compra()
	{
		var (merchant, store, _) = Start(coins: 0);

		var bought = merchant.Buy("p", merchant.Stock("p")[0].Id);

		Assert.False(bought.IsOk);
		Assert.Equal(0, Coins(store));
		Assert.Null(store.Of("p")!.Cosmetics);
	}

	[Fact]
	public void So_se_compra_o_que_esta_no_estoque_e_uma_vez_so()
	{
		var (merchant, store, _) = Start(coins: 1000);
		var outside = Catalog.Cosmetics.Select(o => o.Id).Except(merchant.Stock("p").Select(o => o.Id)).First();
		var inStock = merchant.Stock("p")[0].Id;

		Assert.False(merchant.Buy("p", outside).IsOk);
		Assert.True(merchant.Buy("p", inStock).IsOk);
		var coinsAfter = Coins(store);
		Assert.False(merchant.Buy("p", inStock).IsOk);
		Assert.Equal(coinsAfter, Coins(store));
	}

	[Fact]
	public void Vender_em_lote_paga_a_soma_tira_do_inventario_e_manda_para_recomprar()
	{
		var (merchant, store, _) = Start(coins: 0, equipment: ["scout", "open", "lantern"]);

		var sold = merchant.Sell("p", ["scout", "open"]);

		Assert.True(sold.IsOk);
		Assert.Equal(Catalog.SellPrice("scout") + Catalog.SellPrice("open"), sold.Value);
		Assert.Equal(sold.Value, Coins(store));
		Assert.Equal(["lantern"], store.Of("p")!.Equipment!);
		Assert.Equal(["scout", "open"], merchant.Buybacks("p").Select(b => b.Item));
	}

	[Fact]
	public void O_equipado_nao_vende_e_nada_muda_se_um_do_lote_nao_pode()
	{
		var (merchant, store, session) = Start(coins: 5, equipment: ["scout", "open"]);
		new GearService(session, Gear).Equip("p", "matematica", "scout", 1);

		var sold = merchant.Sell("p", ["open", "scout"]);

		Assert.False(sold.IsOk);
		Assert.Equal(5, Coins(store));
		Assert.Equal(["scout", "open"], store.Of("p")!.Equipment!);
	}

	[Fact]
	public void O_que_nao_e_da_crianca_ou_a_lista_vazia_nao_vende()
	{
		var (merchant, _, _) = Start(equipment: ["scout"]);

		Assert.False(merchant.Sell("p", ["guardian"]).IsOk);
		Assert.False(merchant.Sell("p", []).IsOk);
	}

	[Fact]
	public void Recomprar_custa_o_mesmo_preco_da_venda_sem_prazo()
	{
		var (merchant, store, _) = Start(coins: 0, equipment: ["scout"]);
		var earned = merchant.Sell("p", ["scout"]).Value;

		var rebought = merchant.Rebuy("p", "scout");

		Assert.True(rebought.IsOk);
		Assert.Equal(0, Coins(store));
		Assert.Equal(["scout"], store.Of("p")!.Equipment!);
		Assert.Empty(merchant.Buybacks("p"));
		Assert.True(earned > 0);
	}

	[Fact]
	public void Recomprar_sem_moedas_suficientes_falha_e_o_item_continua_na_linha()
	{
		var (merchant, store, _) = Start(coins: 0, equipment: ["scout"]);
		merchant.Sell("p", ["scout"]);
		store.Update("p", p => p with { Coins = 0 });

		Assert.False(merchant.Rebuy("p", "scout").IsOk);
		Assert.Single(merchant.Buybacks("p"));
	}

	[Fact]
	public void Rotacionar_pede_uma_carga_e_a_carga_nao_acumula_alem_de_uma()
	{
		var (merchant, _, _) = Start();
		Assert.False(merchant.HasCharge("p"));
		Assert.False(merchant.Rotate("p").IsOk);

		merchant.AddCharge("p");
		merchant.AddCharge("p");
		Assert.True(merchant.HasCharge("p"));
		Assert.True(merchant.Rotate("p").IsOk);

		Assert.False(merchant.HasCharge("p"));
		Assert.False(merchant.Rotate("p").IsOk);
	}

	[Fact]
	public void O_estoque_nao_muda_sozinho_nem_ao_comprar_ou_receber_carga()
	{
		var (merchant, _, _) = Start(coins: 1000);
		var before = merchant.Stock("p").Select(o => o.Id).ToList();

		merchant.AddCharge("p");
		merchant.Buy("p", before[0]);

		Assert.Equal(before, merchant.Stock("p").Select(o => o.Id));
	}

	[Fact]
	public void A_rotacao_percorre_o_catalogo_inteiro_antes_de_repetir()
	{
		var (merchant, _, _) = Start();
		var seen = merchant.Stock("p").Select(o => o.Id).ToList();
		var passes = (int)Math.Ceiling(Catalog.Cosmetics.Count / (double)Catalog.StockSize) - 1;

		for (var i = 0; i < passes; i++)
		{
			merchant.AddCharge("p");
			merchant.Rotate("p");
			seen.AddRange(merchant.Stock("p").Select(o => o.Id));
		}

		Assert.Equal(Catalog.Cosmetics.Select(o => o.Id).Order(), seen.Distinct().Order());
	}

	[Fact]
	public void O_item_que_saiu_volta_a_aparecer_depois_de_percorrer_o_catalogo()
	{
		var (merchant, _, _) = Start();
		var first = merchant.Stock("p")[0].Id;
		var comesBack = false;

		for (var i = 0; i < Catalog.Cosmetics.Count; i++)
		{
			merchant.AddCharge("p");
			merchant.Rotate("p");
			comesBack |= merchant.Stock("p").Any(o => o.Id == first);
		}

		Assert.True(comesBack);
	}

	[Fact]
	public void A_rotacao_nao_oferece_o_que_a_crianca_ja_tem()
	{
		var (merchant, store, _) = Start();
		var owned = Catalog.Cosmetics[Catalog.StockSize].Id;
		store.Update("p", p => p with { Cosmetics = [owned] });

		merchant.AddCharge("p");
		merchant.Rotate("p");

		Assert.DoesNotContain(merchant.Stock("p"), o => o.Id == owned);
	}

	[Fact]
	public void Comprar_guarda_o_estoque_mesmo_se_o_item_comprado_sair_da_lista_de_novos()
	{
		var (merchant, _, _) = Start(coins: 1000);
		var before = merchant.Stock("p").Select(o => o.Id).ToList();

		merchant.Buy("p", before[1]);
		merchant.AddCharge("p");

		Assert.Equal(before, merchant.Stock("p").Select(o => o.Id));
		merchant.Rotate("p");
		Assert.DoesNotContain(merchant.Stock("p"), o => o.Id == before[1]);
	}

	[Fact]
	public void Recomprar_o_que_a_crianca_ja_tem_de_novo_nao_duplica_o_equipamento()
	{
		var (merchant, store, _) = Start(coins: 100, equipment: ["scout"]);
		merchant.Sell("p", ["scout"]);
		store.Update("p", p => p with { Equipment = ["scout"] });

		merchant.Rebuy("p", "scout");

		Assert.Equal(["scout"], store.Of("p")!.Equipment!);
	}

	[Fact]
	public void Rotacionar_sem_nada_novo_para_mostrar_nao_gasta_a_carga()
	{
		var (merchant, store, _) = Start();
		store.Update("p", p => p with { Cosmetics = Catalog.Cosmetics.Select(o => o.Id).ToList() });
		merchant.AddCharge("p");

		Assert.False(merchant.Rotate("p").IsOk);
		Assert.True(merchant.HasCharge("p"));
	}

	[Fact]
	public void O_estado_do_Mercador_sobrevive_a_salvar_e_carregar()
	{
		var (merchant, _, _) = Start(coins: 50, equipment: ["scout"]);
		merchant.Sell("p", ["scout"]);
		merchant.AddCharge("p");
		merchant.Rotate("p");
		merchant.AddCharge("p");
		var stock = merchant.Stock("p").Select(o => o.Id).ToList();

		var store = new SaveStore(Path.Combine(folder, "save.json"));
		var again = new MerchantService(new ProgressStore(new SaveSession(store, store.Load().Value!.Data)), Catalog);

		Assert.Equal(stock, again.Stock("p").Select(o => o.Id));
		Assert.True(again.HasCharge("p"));
		Assert.Equal(["scout"], again.Buybacks("p").Select(b => b.Item));
	}

	[Fact]
	public void O_catalogo_do_Mercador_recusa_dados_quebrados()
	{
		Assert.False(MerchantCatalog.Parse("""{"stockSize":2,"cosmetics":[{"id":"nada","price":3}],"gear":[]}""", Cosmetics, Gear).IsOk);
		Assert.False(MerchantCatalog.Parse("""{"stockSize":2,"cosmetics":[{"id":"mercador-laco","price":0}],"gear":[]}""", Cosmetics, Gear).IsOk);
		Assert.False(MerchantCatalog.Parse("""{"stockSize":2,"cosmetics":[{"id":"mercador-laco","price":3}],"gear":[{"id":"nada","price":3}]}""", Cosmetics, Gear).IsOk);
		Assert.False(MerchantCatalog.Parse("""{"stockSize":0,"cosmetics":[{"id":"mercador-laco","price":3}],"gear":[]}""", Cosmetics, Gear).IsOk);
		Assert.False(MerchantCatalog.Parse("{", Cosmetics, Gear).IsOk);
	}

	[Fact]
	public void Todo_equipamento_do_jogo_tem_preco_de_venda()
	{
		Assert.All(Gear.Items, i => Assert.True(Catalog.SellPrice(i.Id) > 0, i.Id));
	}

	[Fact]
	public void Perfil_inexistente_vira_falha_e_nao_excecao()
	{
		var (merchant, _, _) = Start();

		Assert.False(merchant.Buy("nada", "x").IsOk);
		Assert.False(merchant.Sell("nada", ["scout"]).IsOk);
		Assert.False(merchant.AddCharge("nada").IsOk);
		Assert.Empty(merchant.Stock("nada"));
	}

	[Fact]
	public void O_valor_de_uma_moeda_do_jogo_em_centavos_vem_dos_dados_e_e_validado()
	{
		Assert.Equal(100, Catalog.CentsPerCoin);
		Assert.False(MerchantCatalog.Parse("""{"stockSize":2,"centsPerCoin":3,"cosmetics":[{"id":"mercador-laco","price":3}],"gear":[]}""", Cosmetics, Gear).IsOk);
	}

	[Fact]
	public void Nada_do_Mercador_conta_para_a_aprendizagem_nem_para_a_repeticao_espacada()
	{
		var tradeFolder = Path.Combine(MissionFiles.RepositoryRoot(), "game", "Trade");
		var forbidden = new[] { "Mastery", "BattleEnded", "Conclude(", "MissionHistory", "RewardService", "Occurrences" };

		var touching = Directory.EnumerateFiles(tradeFolder, "*.cs").Where(f => forbidden.Any(token => File.ReadAllText(f).Contains(token))).Select(Path.GetFileName);

		Assert.Empty(touching);
	}
}
