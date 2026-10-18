using System.Numerics;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class FreeDrawingTests
{
	const string Digits = "0123456789+-";
	const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

	static string Data(string name) => File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", name));

	static readonly TraceTrails Trails = TraceTrails.Parse(Data("trilhas.json"), Data("trilhas.schema.json")).Value!;
	static readonly FreeDrawRules Rules = FreeDrawRules.Default;
	static readonly GlyphRecognizer Recognizer = new(Trails, Rules);

	// Desenha o glifo na posição dada, como se o dedo passasse pelos pontos da trilha.
	static void Draw(FreeDrawing drawing, string glyph, float left, float size = 200)
	{
		foreach (var stroke in Trails.Find(glyph)!.Strokes)
		{
			var points = stroke.Points.Select(p => new Vector2(left + p.X * size * 0.8f, 40 + p.Y * size)).ToList();
			drawing.Down(points[0]);
			foreach (var p in points.Skip(1)) drawing.Move(p);
			drawing.Up();
		}
	}

	[Fact]
	public void Comeca_a_desenhar_ao_encostar_o_dedo()
	{
		var drawing = new FreeDrawing(Rules);
		Assert.False(drawing.IsActive);

		drawing.Down(new Vector2(10, 10));

		Assert.True(drawing.IsActive);
	}

	[Fact]
	public void A_leitura_so_sai_depois_de_levantar_o_dedo_e_esperar()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "3", 0);

		Assert.Null(drawing.Tick(Rules.IdleSeconds - 0.1f));
		Assert.NotNull(drawing.Tick(0.2f));
		Assert.False(drawing.IsActive);
	}

	[Fact]
	public void Com_o_dedo_apoiado_o_relogio_nao_corre()
	{
		var drawing = new FreeDrawing(Rules);
		drawing.Down(new Vector2(10, 10));
		drawing.Move(new Vector2(60, 90));

		Assert.Null(drawing.Tick(5f));
	}

	[Fact]
	public void Um_novo_traco_antes_do_fim_da_espera_continua_o_mesmo_desenho()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "+", 0);
		drawing.Tick(Rules.IdleSeconds - 0.2f);
		Draw(drawing, "3", 250);

		var reading = drawing.Tick(Rules.IdleSeconds + 0.1f);

		Assert.Equal(2, reading!.Symbols.Count);
	}

	[Fact]
	public void Simbolos_lado_a_lado_sao_separados_pela_posicao()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "3", 0);
		Draw(drawing, "+", 200);
		Draw(drawing, "5", 400);

		var text = Recognizer.Read(drawing.Tick(Rules.IdleSeconds + 0.1f)!, Digits);

		Assert.Equal(GuessKind.Sure, text.Kind);
		Assert.Equal("3+5", text.Text);
	}

	[Fact]
	public void Tracos_cruzados_de_um_mais_sao_um_simbolo_so()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "+", 0);

		Assert.Single(drawing.Tick(Rules.IdleSeconds + 0.1f)!.Symbols);
	}

	[Fact]
	public void Letras_com_varios_tracos_ficam_inteiras_e_a_proxima_e_outro_simbolo()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "B", 0);
		Draw(drawing, "O", 220);

		var text = Recognizer.Read(drawing.Tick(Rules.IdleSeconds + 0.1f)!, Letters);

		Assert.Equal("BO", text.Text);
	}

	[Fact]
	public void Um_simbolo_ambiguo_pede_para_refazer()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "3", 0);
		var half = new Vector2[] { new(0.285f, 0.2f), new(0.65f, 0.11f), new(0.47f, 0.9f) }.Select(p => new Vector2(300 + p.X * 200, 40 + p.Y * 200)).ToList();
		drawing.Down(half[0]);
		foreach (var p in half.Skip(1)) drawing.Move(p);
		drawing.Up();

		var text = Recognizer.Read(drawing.Tick(Rules.IdleSeconds + 0.1f)!, Digits);

		Assert.NotEqual(GuessKind.Sure, text.Kind);
	}

	[Fact]
	public void Um_risco_pequeno_sobre_a_letra_e_acento_e_nao_conta()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "A", 0);
		drawing.Down(new Vector2(55, 22));
		drawing.Move(new Vector2(70, 16));
		drawing.Up();

		var text = Recognizer.Read(drawing.Tick(Rules.IdleSeconds + 0.1f)!, Letters);

		Assert.Equal("A", text.Text);
	}

	[Fact]
	public void Apagar_descarta_o_desenho_sem_ler()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "3", 0);

		drawing.Clear();

		Assert.False(drawing.IsActive);
		Assert.Null(drawing.Tick(5f));
	}

	[Fact]
	public void Descartar_o_ultimo_traco_desfaz_um_toque_curto_sem_deixar_o_desenho_ativo()
	{
		var drawing = new FreeDrawing(Rules);
		drawing.Down(new Vector2(5, 5));
		drawing.Up();

		drawing.Discard();

		Assert.False(drawing.IsActive);
	}

	[Fact]
	public void Descartar_o_ultimo_traco_mantem_os_anteriores()
	{
		var drawing = new FreeDrawing(Rules);
		Draw(drawing, "3", 0);
		drawing.Down(new Vector2(5, 5));
		drawing.Up();

		drawing.Discard();

		Assert.True(drawing.IsActive);
		Assert.Equal(Trails.Find("3")!.Strokes.Count, drawing.Strokes.Count);
	}
}
