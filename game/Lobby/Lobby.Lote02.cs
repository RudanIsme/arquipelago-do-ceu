using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.World;
using Godot;
using Point = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Camada visual reversivel do lote 02; a grade e os objetos de interacao continuam no Lobby.
public partial class Lobby
{
	static readonly HexCell BridgeCell = new(2, -2);
	static readonly Point BridgeCenter = HexGrid.ToWorld(BridgeCell);
	static readonly float BridgeHalfLength = MathF.Sqrt(3f);
	readonly Dictionary<int, Material> pathMaterials = new();
	readonly List<Node3D> lote02Roots = new();

	bool CanCrossLote02(HexCell a, HexCell b)
	{
		if (a != BridgeCell && b != BridgeCell) return true;
		var other = a == BridgeCell ? b : a;
		return other == new HexCell(2, -3) || other == new HexCell(2, -1);
	}

	bool IsLote02PointFree(Point p)
	{
		var d = p - BridgeCenter;
		// As faixas laterais impedem entrada pelo guarda-corpo; os extremos ficam abertos.
		if (MathF.Abs(d.Y) < BridgeHalfLength - .04f && MathF.Abs(d.X) < .84f)
			return MathF.Abs(d.X) <= .54f;
		return HexGrid.FromWorld(p) != BridgeCell || MathF.Abs(d.X) <= .54f;
	}

	public float GroundHeight(Point p)
	{
		var d = p - BridgeCenter;
		if (MathF.Abs(d.X) > .65f || MathF.Abs(d.Y) > BridgeHalfLength) return 0f;
		return .30f * (1f - d.Y * d.Y / 3f);
	}

	Node3D AddLote02(string asset, Vector3 position, Vector3? scale = null, float yaw = 0f)
	{
		var model = LoadModel("lote02/" + asset)!;
		model.Name = "Lote02_" + asset;
		AddChild(model);
		model.Position = position;
		model.Scale = scale ?? Vector3.One;
		model.Rotation = new Vector3(0, yaw, 0);
		lote02Roots.Add(model);
		return model;
	}

	void AddLote02Tile(Vector3 center, int mask)
	{
		var tile = AddLote02("hex-base-v02", center);
		foreach (var mesh in tile.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
			for (var s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				if (mesh.Mesh.SurfaceGetMaterial(s) is not StandardMaterial3D source || !source.ResourceName.StartsWith("Terreno_Atlas64")) continue;
				if (!pathMaterials.TryGetValue(mask, out var material))
				{
					var copy = (StandardMaterial3D)source.Duplicate();
					copy.Uv1Offset = new Vector3((mask % 8) / 8f, -(mask / 8) / 8f, 0);
					pathMaterials[mask] = material = copy;
				}
				mesh.SetSurfaceOverrideMaterial(s, material);
			}
	}

	void AddRock() => AddLote02("rocha-inferior-avaliacao", new Vector3(0, .573f, 0), new Vector3(10f, 2.146f, 10f));
	void AddIslet(Vector3 center, float radius = 2.3f)
	{
		AddLote02("hex-base-v02", center, new Vector3(radius, .8f, radius));
		AddLote02("rocha-inferior-avaliacao", center + new Vector3(0, .28f, 0), new Vector3(radius + .2f, 1.36f, radius + .2f));
	}
	void AddLote02Details()
	{
		var center = new Vector3(BridgeCenter.X, 0, BridgeCenter.Y);
		var bridge = AddLote02("ponte-piso", center, yaw: Mathf.Pi / 2f);
		bridge.Name = "Lote02_Ponte";
		foreach (var side in new[] { -1f, 1f })
		{
			AddLote02("ponte-guarda-corpo", center + new Vector3(side * .64f, 0, 0), yaw: Mathf.Pi / 2f);
			AddLote02("ponte-cabeceira", center + new Vector3(0, 0, side * (BridgeHalfLength + .2f)), yaw: Mathf.Pi / 2f);
		}
		// Bordas retas seguem as arestas exatas da lagoa; curvas ficam para recortes de lagoa futuros.
		foreach (var (cell, kind) in tiles)
		{
			if (kind != Kind.Water) continue;
			var water = HexToWorld(cell.X, cell.Y);
			for (var i = 0; i < Neighbors.Length; i++)
			{
				var next = cell + Neighbors[i];
				if (!tiles.TryGetValue(next, out var land) || land == Kind.Water) continue;
				var n = (water - HexToWorld(next.X, next.Y)).Normalized();
				var t = new Vector3(-n.Z, 0, n.X);
				var edge = water - n * .8660254f;
				// Nao colocar margem sobre as entradas do piso da ponte.
				if (Mathf.Abs(edge.X - center.X) < .75f && Mathf.Abs(edge.Z - center.Z) < 2.2f) continue;
				AddLote02("margem-reta", edge - t * .5f, yaw: Mathf.Atan2(-t.Z, t.X));
			}
		}
		Vector2I[] accents = [new(-3, 2), new(-3, -1), new(-1, -3), new(3, 1), new(1, 3), new(-4, 3), new(4, -1), new(3, -4)];
		foreach (var cell in accents)
		{
			if (!tiles.TryGetValue(cell, out var kind) || kind == Kind.Path || kind == Kind.Water || !Grid.IsWalkable(new HexCell(cell.X, cell.Y))) continue;
			var pos = HexToWorld(cell.X, cell.Y);
			if (Rules.Objects.Any(o => pos.DistanceTo(HexToWorld(o.Q, o.R)) < 2.5f)) continue;
			var variation = (cell.X + cell.Y + 12) % 3;
			AddLote02("grama-tres-pontas-v01", pos, Vector3.One * (.9f + .1f * variation), Mathf.DegToRad(30f + 15f * variation));
		}
	}

	// Verifica a mesma grade e o mesmo controlador usados pelo jogo, sem mudar o save.
	public Godot.Collections.Dictionary ValidateLote02()
	{
		var start = HexGrid.ToWorld(new HexCell(2, -3));
		var finish = HexGrid.ToWorld(new HexCell(2, -1));
		var mover = new MovementController(Grid, start, 3f);
		var accepted = mover.GoTo(finish);
		var peak = 0f;
		for (var i = 0; i < 240; i++) { mover.Tick(Point.Zero, 1f / 60f); peak = MathF.Max(peak, GroundHeight(mover.Position)); }
		var crossing = accepted && Point.Distance(mover.Position, finish) < .02f && peak > .29f;
		var guarded = !Grid.IsFree(BridgeCenter + new Point(.65f, 0), .2f);
		var interactions = Rules.Objects.All(o => Grid.Cells.Any(c => Grid.IsWalkable(c) && Point.Distance(HexGrid.ToWorld(c), HexGrid.ToWorld(new HexCell(o.Q, o.R))) <= Rules.InteractionReach && Grid.FindPath(new HexCell(0, 0), c) is not null));
		long triangles = 0;
		var missingMaterials = 0;
		foreach (var root in lote02Roots)
			foreach (var mesh in root.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
				for (var s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
				{
					if (mesh.GetActiveMaterial(s) is null) missingMaterials++;
					var arrays = mesh.Mesh.SurfaceGetArrays(s);
					var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
					triangles += indices.Length > 0 ? indices.Length / 3 : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
				}
		return new Godot.Collections.Dictionary { ["passagem_ponte"] = crossing, ["altura_maxima"] = peak, ["laterais_bloqueadas"] = guarded, ["interacoes_alcancaveis"] = interactions, ["triangulos_lote02_instanciados"] = triangles, ["materiais_ausentes"] = missingMaterials, ["instancias_lote02"] = lote02Roots.Count };
	}
}
