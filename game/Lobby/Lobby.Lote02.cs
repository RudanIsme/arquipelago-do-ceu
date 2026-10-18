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
	readonly Dictionary<int, Material> pathMaterials = new();
	readonly List<Node3D> lote02Roots = new();

	// O chão do Lobby é plano: as pontes entre as ilhas e os corredores da Dungeon ficam rentes ao chão.
	public float GroundHeight(Point p) => 0f;

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

	void AddLote02Details()
	{
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

	// Verifica a mesma grade usada pelo jogo, sem mudar o save: a lagoa fecha o centro e todo objeto tem chão por perto.
	public Godot.Collections.Dictionary ValidateLote02()
	{
		var lakeBlocked = plan.Lake.All(c => !Grid.IsWalkable(c));
		var interactions = Rules.Objects.All(o => Grid.Cells.Any(c => Grid.IsWalkable(c) && Point.Distance(HexGrid.ToWorld(c), HexGrid.ToWorld(new HexCell(o.Q, o.R))) <= Rules.InteractionReach));
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
		return new Godot.Collections.Dictionary { ["lagoa_bloqueada"] = lakeBlocked, ["interacoes_alcancaveis"] = interactions, ["triangulos_lote02_instanciados"] = triangles, ["materiais_ausentes"] = missingMaterials, ["instancias_lote02"] = lote02Roots.Count };
	}
}
