using Godot;

namespace ArquipelagoDoCeu;

// As bolinhas de uma tecla de número, desenhadas em vez de escritas: algumas fontes (a para dislexia) não têm o "•".
sealed partial class DotsView : Control
{
	const float Radius = 5f;
	const int PerRow = 5;

	int count;

	public DotsView() => MouseFilter = MouseFilterEnum.Ignore;

	public int Count
	{
		get => count;
		set
		{
			count = value;
			QueueRedraw();
		}
	}

	// Até 9 vira bolinhas; de 10 em diante cada dezena é uma barra e as unidades que sobram são bolinhas, como no material dourado.
	public override void _Draw()
	{
		if (count >= 10)
		{
			DrawTens();
			return;
		}
		var rows = Mathf.CeilToInt(count / (float)PerRow);
		for (var i = 0; i < count; i++)
		{
			var row = i / PerRow;
			var inRow = Mathf.Min(PerRow, count - row * PerRow);
			var x = Size.X / 2f + (i % PerRow - (inRow - 1) / 2f) * (Radius * 2.6f);
			var y = Size.Y / 2f + (row - (rows - 1) / 2f) * (Radius * 2.6f);
			DrawCircle(new Vector2(x, y), Radius, UiKit.TextColor);
		}
	}

	void DrawTens()
	{
		const float BarWidth = 9f;
		const float Gap = 5f;
		var tens = count / 10;
		var ones = count % 10;
		var width = tens * (BarWidth + Gap) + (ones > 0 ? Radius * 5.2f : 0f) - Gap;
		var x = (Size.X - width) / 2f;
		var height = Mathf.Max(8f, Size.Y - 6f);
		for (var i = 0; i < tens; i++)
		{
			DrawRect(new Rect2(x, (Size.Y - height) / 2f, BarWidth, height), UiKit.TextColor);
			x += BarWidth + Gap;
		}
		for (var i = 0; i < ones; i++)
		{
			var column = i % 2;
			var row = i / 2;
			DrawCircle(new Vector2(x + Radius + column * Radius * 2.6f, Size.Y / 2f + (row - (Mathf.CeilToInt(ones / 2f) - 1) / 2f) * Radius * 2.6f), Radius, UiKit.TextColor);
		}
	}
}
