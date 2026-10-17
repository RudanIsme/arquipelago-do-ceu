using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// Cenário `andar` do piloto: desenha o Plano de um Nível, Andares lado a lado, e grava uma captura.
//   godot --path game -- --driver=andar --saida=<pasta> [--mundo=1] [--dungeon=matematica] [--nivel=1]
static class FloorShots
{
	public static async Task Run(DevDriver d)
	{
		using var file = FileAccess.Open("res://data/rules.json", FileAccess.ModeFlags.Read);
		var rules = GameRules.Parse(file.GetAsText());
		if (!rules.IsOk) { GD.PushError($"Piloto: {rules.Error}"); return; }

		var world = ulong.TryParse(d.Arg("mundo"), out var w) ? w : 1UL;
		var discipline = d.Arg("dungeon") is { Length: > 0 } name ? name : "matematica";
		var level = int.TryParse(d.Arg("nivel"), out var n) ? n : 1;
		var plan = LevelPlan.Create(rules.Value!.Dungeon, world, discipline, level, 1);

		var layer = new CanvasLayer { Layer = 100 };
		d.GetTree().Root.AddChild(layer);
		layer.AddChild(new ColorRect { Color = Color.FromHtml("#1B1424"), Size = new Vector2(1280, 720) });
		layer.AddChild(Text($"Plano: {discipline}, Nível {level}, mundo {world}, {plan.Floors.Count} Andares", new Vector2(24, 12), 24));
		layer.AddChild(Text(FloorMap.Legend + "   ★ Item de troca", new Vector2(24, 48), 18));

		var cell = 1232f / plan.Floors.Count;
		foreach (var (floorPlan, i) in plan.Floors.Select((f, i) => (f, i)))
		{
			var floor = FloorGenerator.Generate(rules.Value.Dungeon, world, discipline, level, floorPlan, 1);
			var marks = string.Join(", ", new[] { floorPlan.HoldsItem ? "Item" : null, floorPlan.HasMarco ? "Marco" : null, floorPlan.HasEpic ? "épico" : null }.Where(x => x is not null));
			var x = 24 + i * cell;
			layer.AddChild(Text($"Andar {floorPlan.Number}: {floor.Rooms.Count} Salas" + (marks.Length > 0 ? $" ({marks})" : ""), new Vector2(x, 84), 20));
			var map = new FloorMap(floor, new Vector2(cell - 24, 560)) { Position = new Vector2(x, 124) };
			layer.AddChild(map);
		}

		await d.Wait(0.4);
		await d.Shot($"01-plano-{discipline}-nivel{level}");
		layer.QueueFree();
	}

	static Label Text(string text, Vector2 at, int size)
	{
		var label = new Label { Text = text, Position = at };
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}
}

