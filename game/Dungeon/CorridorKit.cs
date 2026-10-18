using System;
using System.Collections.Generic;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// Os corredores da Dungeon feitos com as peças da ponte do kit lote02: piso a cada dois hexágonos, guarda-corpo dos dois lados e
// cabeceira na ponta; nas curvas, o hexágono do kit. Sem o modelo (arquivo faltando) o corredor volta ao piso de caixas: a criança nunca fica sem caminho.
public static class CorridorKit
{
	const string Folder = "lote02/";
	// O piso tem 3,455 de comprimento (dois hexágonos); o guarda-corpo fica a meia largura do piso.
	const float RailOffset = 0.64f;
	// A cabeceira fica logo depois da ponta do corredor: meio hexágono mais a metade da espessura dela.
	const float HeadReach = 0.866f + 0.2f;
	const string FirstModel = "res://assets/modelos/lote02/ponte-piso.glb";
	// O topo do piso do kit fica 0,30 acima do centro; baixa para ficar rente ao chão da Sala.
	const float FloorDrop = -0.30f;

	// Devolve falso quando o kit não carregou: quem chama desenha o piso de reserva.
	public static bool Add(Node3D parent, IReadOnlyList<HexCell> cells)
	{
		if (cells.Count == 0 || !ResourceLoader.Exists(FirstModel)) return false;
		for (var i = 0; i < cells.Count; i++)
		{
			var turn = IsTurn(cells, i);
			if (turn || cells.Count == 1) Place(parent, "hex-base-v02", Center(cells[i]) + new Vector3(0f, FloorDrop, 0f), 0f);
		}
		foreach (var (from, to) in StraightRuns(cells))
			Span(parent, Center(cells[from]), Center(cells[to]), to - from + 1);
		if (cells.Count > 1)
		{
			Head(parent, Center(cells[0]), Center(cells[0]) - Center(cells[1]));
			Head(parent, Center(cells[^1]), Center(cells[^1]) - Center(cells[^2]));
		}
		return true;
	}

	// Cada trecho reto de `from` até `to` (índices dos hexágonos): uma curva abre outro trecho.
	static IEnumerable<(int From, int To)> StraightRuns(IReadOnlyList<HexCell> cells)
	{
		var start = 0;
		for (var i = 1; i < cells.Count; i++)
			if (i == cells.Count - 1 || IsTurn(cells, i))
			{
				if (i > start) yield return (start, i);
				start = i;
			}
	}

	static bool IsTurn(IReadOnlyList<HexCell> cells, int i) =>
		i > 0 && i < cells.Count - 1 && cells[i] - cells[i - 1] != cells[i + 1] - cells[i];

	// Cobre o trecho de `a` a `b` com pisos de dois hexágonos e põe os guarda-corpos e a cabeceira.
	static void Span(Node3D parent, Vector3 a, Vector3 b, int count)
	{
		var along = b - a;
		var length = along.Length();
		if (length < 0.01f) return;
		var direction = along / length;
		var yaw = Mathf.Atan2(-direction.Z, direction.X);
		var side = new Vector3(-direction.Z, 0f, direction.X);
		var step = length / (count - 1);
		// Um piso a cada dois hexágonos; o último se apoia no anterior se sobrar um.
		for (var first = 0; first <= count - 1; first += 2)
		{
			var center = a + direction * (Mathf.Min(first + 0.5f, count - 1.5f) * step);
			Place(parent, "ponte-piso", center + new Vector3(0f, FloorDrop, 0f), yaw);
			foreach (var sign in new[] { -1f, 1f })
				Place(parent, "ponte-guarda-corpo", center + side * (sign * RailOffset), yaw);
		}
	}

	// A cabeceira da ponta: um degrau baixo que fecha o corredor de frente para a Sala.
	static void Head(Node3D parent, Vector3 end, Vector3 outward)
	{
		var direction = outward.Normalized();
		Place(parent, "ponte-cabeceira", end + direction * HeadReach + new Vector3(0f, FloorDrop, 0f), Mathf.Atan2(-direction.Z, direction.X));
	}

	static Vector3 Center(HexCell cell)
	{
		var world = HexGrid.ToWorld(cell);
		return new Vector3(world.X, 0f, world.Y);
	}

	static void Place(Node3D parent, string asset, Vector3 position, float yaw)
	{
		if (Lobby.LoadModel(Folder + asset) is not { } model) return;
		model.Name = "Kit_" + asset;
		parent.AddChild(model);
		model.Position = position;
		model.Rotation = new Vector3(0f, yaw, 0f);
	}
}
