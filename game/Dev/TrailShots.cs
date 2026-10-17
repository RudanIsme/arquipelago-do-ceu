using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// Cenário `trilhas` do piloto: a folha com todas as trilhas (início numerado, seta e faixa) e um traço de verdade numa sílaba.
//   godot --path game -- --driver=trilhas --saida=<pasta>
static class TrailShots
{
	const float SheetScale = 0.34f;
	const int Columns = 8;

	public static async Task Run(DevDriver d)
	{
		var trails = TrailSource.Shared;
		d.Expect(trails.Glyphs.Count() == 38, "as trilhas de 0 a 9, + e -, e de A a Z carregaram");
		var rules = new TracingRules(16, 30, 0.8f, 0.35f);

		var layer = new CanvasLayer { Layer = 100 };
		d.GetTree().Root.AddChild(layer);
		layer.AddChild(new ColorRect { Color = Color.FromHtml("#EEDDB8"), Size = new Vector2(1280, 720) });
		foreach (var (glyph, i) in trails.Glyphs.Select((g, i) => (g, i)))
		{
			var canvas = new TraceCanvas(rules) { CustomMinimumSize = TracePad.CanvasSize, Size = TracePad.CanvasSize, Scale = Vector2.One * SheetScale };
			canvas.Position = new Vector2(12 + i % Columns * 158, 12 + i / Columns * 112);
			layer.AddChild(canvas);
			canvas.SetGuide(trails.Guide(glyph, new System.Numerics.Vector2(TracePad.CanvasSize.X, TracePad.CanvasSize.Y), rules).Value!);
			canvas.AutoFill(0.4f);
		}
		await d.Wait(0.5);
		await d.Shot("01-folha-de-trilhas");
		layer.QueueFree();

		await Syllable(d, rules);
	}

	// Uma sílaba é uma trilha por letra: o traço certo de cada uma completa o pedido.
	static async Task Syllable(DevDriver d, TracingRules rules)
	{
		var layer = new CanvasLayer { Layer = 100 };
		d.GetTree().Root.AddChild(layer);
		layer.AddChild(new ColorRect { Color = Color.FromHtml("#EEDDB8"), Size = new Vector2(1280, 720) });
		var pad = new TracePad(rules) { Position = new Vector2(440, 40) };
		layer.AddChild(pad);
		var completed = false;
		pad.Completed += () => completed = true;
		await pad.Begin("ba", "Desenhe a sílaba BA");
		await d.Wait(0.3);
		await d.Shot("02-silaba-ba-primeira-letra");
		await d.TraceGuide();
		await d.Wait(0.9);
		await d.Shot("03-silaba-ba-segunda-letra");
		await d.TraceGuide();
		await d.Wait(1.0);
		d.Expect(completed, "traçar a trilha de cada letra completa a sílaba");
		layer.QueueFree();
	}
}
