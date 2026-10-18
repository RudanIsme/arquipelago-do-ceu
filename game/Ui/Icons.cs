using System.Collections.Generic;
using Godot;

namespace ArquipelagoDoCeu;

// Os ícones das molduras (equipamento, Cosmético, moeda, cadeado), desenhados em SVG do mockup e rasterizados na hora, sem arquivos de
// imagem: trocar por arte de verdade é trocar o texto do SVG. Sem ícone para o nome pedido, devolve nulo e a moldura fica só com o texto.
public static class Icons
{
	const string Ink = "#3D2C25";

	static readonly Dictionary<string, string> Shapes = new()
	{
		["helmet"] = $"<path d='M4 16v-4a8 8 0 0 1 16 0v4z' fill='#C9B1E8' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/><path d='M3 16h18v3H3z' fill='#946239' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["armor"] = $"<path d='M3 7l5-3 4 2 4-2 5 3-2 4-3-1v11H8V10l-3 1z' fill='#B9E6E6' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["accessory"] = $"<circle cx='12' cy='14' r='6.5' fill='none' stroke='{Ink}' stroke-width='3'/><path d='M9 6l3-3 3 3-3 3z' fill='#F2C14E' stroke='{Ink}' stroke-width='1.8' stroke-linejoin='round'/>",
		["key"] = $"<circle cx='8' cy='12' r='5' fill='#F2C14E' stroke='{Ink}' stroke-width='2'/><path d='M13 12h9M18 12v4M21 12v3' stroke='{Ink}' stroke-width='2.4' stroke-linecap='round'/>",
		["lock"] = $"<path d='M7 11V8a5 5 0 0 1 10 0v3' fill='none' stroke='{Ink}' stroke-width='2.4'/><rect x='4.5' y='11' width='15' height='10' rx='2' fill='#E1CA9A' stroke='{Ink}' stroke-width='2'/>",
		["star"] = $"<path d='M12 2.5l2.9 6 6.6.8-4.9 4.5 1.3 6.5L12 17l-5.9 3.3 1.3-6.5L2.5 9.3l6.6-.8z' fill='#F2C14E' stroke='{Ink}' stroke-width='1.6' stroke-linejoin='round'/>",
		["coin"] = $"<circle cx='12' cy='12' r='9' fill='#F2C14E' stroke='{Ink}' stroke-width='2'/><circle cx='12' cy='12' r='5' fill='none' stroke='{Ink}' stroke-width='1.6'/>",
		["swap"] = $"<path d='M4 8h13l-3-3M20 16H7l3 3' fill='none' stroke='{Ink}' stroke-width='2.4' stroke-linecap='round' stroke-linejoin='round'/>",
		["bow"] = $"<path d='M12 12 4 7v10zM12 12l8-5v10z' fill='#E98FA3' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/><circle cx='12' cy='12' r='2.4' fill='#F2C14E' stroke='{Ink}' stroke-width='1.6'/>",
		["tie"] = $"<path d='M9 3h6l-1 4 2 12-4 2-4-2 2-12z' fill='#7FA8E8' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["scarf"] = $"<path d='M4 8c4 3 12 3 16 0v4c-4 3-12 3-16 0z' fill='#E98FA3' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/><path d='M15 12l2 8h3l-1-8z' fill='#E98FA3' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["hat"] = $"<path d='M8 15V8a4 4 0 0 1 8 0v7z' fill='#946239' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/><path d='M2 15h20v3H2z' fill='#C79A5A' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["cap"] = $"<path d='M4 16a8 8 0 0 1 16 0z' fill='#7FA8E8' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/><path d='M12 16h10v2.5H12z' fill='#3E6BB0' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["cape"] = $"<path d='M7 3h10l3 17H4z' fill='#C9B1E8' stroke='{Ink}' stroke-width='2' stroke-linejoin='round'/>",
		["backpack"] = $"<rect x='5' y='6' width='14' height='15' rx='4' fill='#C79A5A' stroke='{Ink}' stroke-width='2'/><path d='M8 6V4h8v2M8 14h8' fill='none' stroke='{Ink}' stroke-width='2' stroke-linecap='round'/>",
		["wings"] = $"<path d='M12 5C8 2 3 4 2 9c3-1 5 0 6 2-2 0-4 1-5 3 3 0 6 1 9 5zM12 5c4-3 9-1 10 4-3-1-5 0-6 2 2 0 4 1 5 3-3 0-6 1-9 5z' fill='#FFF6DC' stroke='{Ink}' stroke-width='1.8' stroke-linejoin='round'/>",
	};

	static readonly Dictionary<string, Texture2D?> Cache = [];

	// Equipamento por espaço e Cosmético por tipo de peça.
	public static Texture2D? For(string name, int size = 48)
	{
		var key = $"{name}@{size}";
		if (Cache.TryGetValue(key, out var cached)) return cached;
		Texture2D? texture = null;
		if (Shapes.TryGetValue(name, out var body))
		{
			var image = new Image();
			var error = image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' width='24' height='24'>{body}</svg>", size / 24f);
			if (error == Error.Ok) texture = ImageTexture.CreateFromImage(image);
		}
		Cache[key] = texture;
		return texture;
	}
}
