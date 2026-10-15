using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

// Experimento local: tecido com particulas em espaco mundial, sem substituir o rig do personagem.
public partial class DynamicCape : Node3D
{
	const int Cols = 9, Rows = 13, Count = Cols * Rows;
	const float Step = 1f / 60f;
	readonly record struct Link(int A, int B, float Length, float Stiffness);
	readonly List<Link> links = new();
	readonly Vector3[] rest = new Vector3[Count], points = new Vector3[Count], previous = new Vector3[Count], normals = new Vector3[Count];
	readonly Vector2[] uvs = new Vector2[Count];
	readonly int[][] indices = new int[3][];
	readonly Material[] materials = new Material[3];
	readonly ArrayMesh mesh = new();
	MeshInstance3D visual = null!;
	MeshInstance3D baked = null!;
	Node3D model = null!;
	Skeleton3D skeleton = null!;
	int rightShoulder, leftShoulder, spine, hips;
	readonly int[] legBones = new int[6];
	readonly Vector3[] legPoints = new Vector3[6];
	Vector3 rightOffset, leftOffset, lastRight, lastLeft;
	Transform3D torsoReference, hipsReference, torso, pelvis, torsoInverse, pelvisInverse, modelInverse;
	float accumulator;
	bool selected = true, reduced;
	int resets, frames;
	double totalMs, peakMs;
	float maxStretch = 1, maxPinError, maxTipLag, maxProxyPenetration;
	Label modeLabel = null!;

	public void Initialize(Node3D character, MeshInstance3D original, Skeleton3D rig)
	{
		model = character; baked = original; skeleton = rig;
		Name = "DynamicCape";
	}

	Transform3D Bone(int index) => skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(index);
	Vector3 RightAnchor() => Bone(rightShoulder) * rightOffset;
	Vector3 LeftAnchor() => Bone(leftShoulder) * leftOffset;
	static bool Pinned(int i) => i == 0 || i == Cols - 1;

	public override void _Ready()
	{
		TopLevel = true;
		GlobalTransform = Transform3D.Identity;
		rightShoulder = skeleton.FindBone("RightShoulder"); leftShoulder = skeleton.FindBone("LeftShoulder");
		spine = skeleton.FindBone("Spine"); hips = skeleton.FindBone("Hips");
		string[] names = ["LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot"];
		for (var i = 0; i < names.Length; i++) legBones[i] = skeleton.FindBone(names[i]);
		// Grade local simplificada conserva silhueta e faixa dourada; o original continua disponivel para comparar.
		for (var r = 0; r < Rows; r++)
			for (var c = 0; c < Cols; c++)
			{
				var u = c / (float)(Cols - 1); var v = r / (float)(Rows - 1);
				rest[r * Cols + c] = new Vector3(Mathf.Lerp(-.13f, .17f, u) * (1f + .55f * v), .79f - .54f * v - .025f * Mathf.Sin(u * Mathf.Pi) * (1f - v), -.145f - .06f * v);
				uvs[r * Cols + c] = new Vector2(u, v);
			}
		rightOffset = Bone(rightShoulder).AffineInverse() * model.ToGlobal(rest[0]);
		leftOffset = Bone(leftShoulder).AffineInverse() * model.ToGlobal(rest[Cols - 1]);
		torsoReference = Bone(spine).AffineInverse() * model.GlobalTransform;
		hipsReference = Bone(hips).AffineInverse() * model.GlobalTransform;
		var groups = new[] { new List<int>(), new List<int>(), new List<int>() };
		for (var r = 0; r < Rows - 1; r++)
			for (var c = 0; c < Cols - 1; c++)
			{
				var a = r * Cols + c; var group = r == Rows - 2 || c == 0 || c == Cols - 2 ? 2 : 0;
				groups[group].AddRange([a, a + Cols, a + 1, a + 1, a + Cols, a + Cols + 1]);
			}
		for (var i = 0; i < 3; i++)
		{
			indices[i] = groups[i].ToArray();
			var mat = baked.GetActiveMaterial(Math.Min(i, baked.Mesh.GetSurfaceCount() - 1));
			materials[i] = (Material)mat.Duplicate();
			if (materials[i] is BaseMaterial3D standard) standard.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		}
		for (var r = 0; r < Rows; r++)
			for (var c = 0; c < Cols; c++)
			{
				var a = r * Cols + c;
				if (c + 1 < Cols) AddLink(a, a + 1, 1f);
				if (r + 1 < Rows) AddLink(a, a + Cols, 1f);
				if (c + 1 < Cols && r + 1 < Rows) { AddLink(a, a + Cols + 1, .8f); AddLink(a + 1, a + Cols, .8f); }
				if (c + 2 < Cols) AddLink(a, a + 2, .12f);
				if (r + 2 < Rows) AddLink(a, a + Cols * 2, .12f);
			}
		visual = new MeshInstance3D { Name = "CapePhysicsMesh", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.DoubleSided };
		AddChild(visual);
		var layer = new CanvasLayer(); AddChild(layer);
		var ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; layer.AddChild(ui); ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		modeLabel = new Label { Visible = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--cape-diagnostics") >= 0, MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right };
		ui.AddChild(modeLabel); modeLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		modeLabel.OffsetLeft = -390; modeLabel.OffsetRight = -18; modeLabel.OffsetTop = 14;
		modeLabel.AddThemeFontSizeOverride("font_size", 18); modeLabel.AddThemeColorOverride("font_color", new Color("292137"));
		Reset(); UpdateMode(); UpdateMesh();
	}

	void AddLink(int a, int b, float stiffness) => links.Add(new Link(a, b, model.ToGlobal(rest[a]).DistanceTo(model.ToGlobal(rest[b])), stiffness));
	void Reset()
	{
		for (var i = 0; i < Count; i++) points[i] = previous[i] = model.ToGlobal(rest[i]);
		lastRight = RightAnchor(); lastLeft = LeftAnchor(); accumulator = 0;
	}
	public void Toggle() { selected = !selected; Reset(); UpdateMode(); }
	public void SetReduced(bool value) { if (reduced == value) return; reduced = value; Reset(); UpdateMode(); }
	void UpdateMode()
	{
		visual.Visible = selected && !reduced; baked.Visible = !visual.Visible;
		modeLabel.Text = visual.Visible ? "Capa din\u00e2mica - F8 comparar" : "Capa pr\u00e9-calculada - F8 comparar";
	}

	public override void _Process(double delta)
	{
		if (!selected || reduced) return;
		var watch = Stopwatch.StartNew();
		var right = RightAnchor(); var left = LeftAnchor();
		if (right.DistanceTo(lastRight) > 1.5f || left.DistanceTo(lastLeft) > 1.5f) { Reset(); resets++; }
		torso = Bone(spine) * torsoReference; pelvis = Bone(hips) * hipsReference;
		torsoInverse = torso.AffineInverse(); pelvisInverse = pelvis.AffineInverse(); modelInverse = model.GlobalTransform.AffineInverse();
		for (var i = 0; i < legBones.Length; i++) legPoints[i] = Bone(legBones[i]).Origin;
		accumulator += Math.Min((float)delta, 1f / 15f);
		var steps = Math.Min(4, (int)(accumulator / Step));
		for (var sub = 0; sub < steps; sub++)
		{
			var a = lastRight.Lerp(right, (sub + 1f) / steps); var b = lastLeft.Lerp(left, (sub + 1f) / steps);
			Simulate(a, b); accumulator -= Step;
		}
		lastRight = right; lastLeft = left;
		UpdateMesh();
		frames++; var ms = watch.Elapsed.TotalMilliseconds;
		if (frames > 20) { totalMs += ms; peakMs = Math.Max(peakMs, ms); }
	}

	void Simulate(Vector3 a, Vector3 b)
	{
		for (var i = 0; i < Count; i++)
		{
			if (Pinned(i)) continue;
			var p = points[i]; var inertia = ((p - previous[i]) * MathF.Exp(-1.8f * Step)).LimitLength(.08f);
			points[i] += inertia + Vector3.Down * (9.81f * Step * Step); previous[i] = p;
		}
		for (var iteration = 0; iteration < 12; iteration++)
		{
			points[0] = a; points[Cols - 1] = b;
			foreach (var link in links)
			{
				var d = points[link.B] - points[link.A]; var length = d.Length(); if (length < .00001f) continue;
				var wa = Pinned(link.A) ? 0f : 1f; var wb = Pinned(link.B) ? 0f : 1f;
				if (wa + wb == 0f) continue;
				var correction = d * ((length - link.Length) / length * link.Stiffness / (wa + wb));
				points[link.A] += correction * wa; points[link.B] -= correction * wb;
			}
			for (var i = 0; i < Count; i++)
			{
				if (Pinned(i)) continue;
				var prior = points[i];
				var fraction = (i / Cols) / (float)(Rows - 1);
				var target = torso * rest[i] + (a + b) * .5f - torso * ((rest[0] + rest[Cols - 1]) * .5f);
				var deviation = points[i] - target;
				var allowance = .015f + fraction * .24f;
				if (deviation.LengthSquared() > allowance * allowance) points[i] = target + deviation.Normalized() * allowance;
				CollideEllipsoid(ref points[i], torso, torsoInverse, new Vector3(.01f, .67f, .055f), new Vector3(.195f, .185f, .16f));
				CollideEllipsoid(ref points[i], pelvis, pelvisInverse, new Vector3(0, .53f, .075f), new Vector3(.19f, .13f, .145f));
				for (var leg = 0; leg < 2; leg++)
				{
					CollideCapsule(ref points[i], legPoints[leg * 3], legPoints[leg * 3 + 1], .105f);
					CollideCapsule(ref points[i], legPoints[leg * 3 + 1], legPoints[leg * 3 + 2], .075f);
				}
				// Barreira traseira conservadora evita o tecido atravessar o torso durante uma meia-volta brusca.
				var local = torsoInverse * points[i];
				local.Z = Mathf.Min(local.Z, local.Y > .45f ? -.12f : -.07f);
				points[i] = torso * local;
				previous[i] += (points[i] - prior) * .8f;
				points[i].Y = Mathf.Max(points[i].Y, model.GlobalPosition.Y + .04f);
				var center = (a + b) * .5f; var offset = points[i] - center;
				if (offset.LengthSquared() > 1.44f) points[i] = center + offset.Normalized() * 1.2f;
			}
		}
		points[0] = previous[0] = a; points[Cols - 1] = previous[Cols - 1] = b;
		maxPinError = Mathf.Max(maxPinError, points[0].DistanceTo(a));
		foreach (var link in links) if (link.Stiffness == 1f) maxStretch = Mathf.Max(maxStretch, points[link.A].DistanceTo(points[link.B]) / link.Length);
		for (var i = 0; i < Count; i++)
		{
			if (Pinned(i)) continue;
			var n = (torsoInverse * points[i] - new Vector3(.01f, .67f, .055f)) / new Vector3(.195f, .185f, .16f);
			maxProxyPenetration = Mathf.Max(maxProxyPenetration, Mathf.Max(0f, 1f - n.Length()) * .16f);
		}
		maxTipLag = Mathf.Max(maxTipLag, points[Count - Cols / 2 - 1].DistanceTo(model.ToGlobal(rest[Count - Cols / 2 - 1])));
	}

	static void CollideEllipsoid(ref Vector3 p, Transform3D frame, Transform3D inverse, Vector3 center, Vector3 radii)
	{
		var local = inverse * p - center;
		var n = local / radii; var length = n.Length();
		if (length >= 1f) return;
		if (length < .0001f) n = Vector3.Back;
		p = frame * (center + n.Normalized() * radii);
	}
	static void CollideCapsule(ref Vector3 p, Vector3 a, Vector3 b, float radius)
	{
		var axis = b - a; var t = Mathf.Clamp((p - a).Dot(axis) / Mathf.Max(axis.LengthSquared(), .00001f), 0f, 1f);
		var center = a + axis * t; var d = p - center;
		if (d.LengthSquared() < radius * radius) p = center + (d.LengthSquared() > .00001f ? d.Normalized() : Vector3.Back) * radius;
	}

	void UpdateMesh()
	{
		Array.Clear(normals);
		foreach (var surface in indices)
			for (var i = 0; i < surface.Length; i += 3)
			{
				var a = surface[i]; var b = surface[i + 1]; var c = surface[i + 2];
				var normal = (points[b] - points[a]).Cross(points[c] - points[a]);
				normals[a] += normal; normals[b] += normal; normals[c] += normal;
			}
		for (var i = 0; i < Count; i++) normals[i] = normals[i].Normalized();
		mesh.ClearSurfaces();
		for (var i = 0; i < indices.Length; i++)
		{
			if (indices[i].Length == 0) continue;
			var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = points; arrays[(int)Mesh.ArrayType.Normal] = normals;
			arrays[(int)Mesh.ArrayType.TexUV] = uvs; arrays[(int)Mesh.ArrayType.Index] = indices[i];
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, materials[i]);
		}
	}

	public Godot.Collections.Dictionary Diagnostics() => new()
	{
		["dinamica"] = selected && !reduced, ["particulas"] = Count, ["triangulos"] = 192,
		["finita"] = points.All(p => p.IsFinite()), ["alongamento_maximo"] = maxStretch, ["erro_fixacao"] = maxPinError,
		["penetracao_colisor_maxima"] = maxProxyPenetration, ["deslocamento_barra_maximo"] = maxTipLag, ["reinicios_seguranca"] = resets,
		["media_ms"] = frames > 20 ? totalMs / (frames - 20) : 0, ["pico_ms"] = peakMs,
		["frames"] = frames, ["vertices_mundo"] = points,
	};
}
