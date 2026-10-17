using System;
using System.Collections.Generic;
using Godot;

namespace ArquipelagoDoCeu;

// Mantém os GLBs originais: só troca o material da instância, com máscaras no espaço da malha.
public partial class LobbyAmbience : Node3D
{
	readonly List<ShaderMaterial> materials = new();
	readonly List<MeshInstance3D> motes = new();
	readonly Func<bool> reduceMotion;
	readonly Func<bool> reduceEffects;
	readonly Node3D model;
	readonly bool portal;
	double phase;
	readonly RandomNumberGenerator random = new();
	float gestureWait = 1.5f;
	float gestureTime;
	float look;
	float stretch;
	int gesture;
	int previousGesture = 2;
	Player? player;
	ShaderMaterial? haloMaterial;
	OmniLight3D? light;

	public LobbyAmbience(Node3D model, bool portal, Func<bool> reduceMotion, Func<bool> reduceEffects)
	{
		this.model = model;
		this.portal = portal;
		this.reduceMotion = reduceMotion;
		this.reduceEffects = reduceEffects;
		Name = portal ? "PortalEnergy" : "GuideIdle";
	}

	public override void _Ready()
	{
		random.Randomize();
		foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
			{
				if (mesh.GetActiveMaterial(i) is not BaseMaterial3D original) continue;
				var material = new ShaderMaterial { Shader = GD.Load<Shader>(portal ? "res://Lobby/portal_energy.gdshader" : "res://Lobby/guide_idle.gdshader") };
				material.SetShaderParameter("albedo_tex", original.AlbedoTexture);
				if (portal) material.SetShaderParameter("paint_tex", GD.Load<Texture2D>("res://Lobby/portal_paint.png"));
				mesh.SetSurfaceOverrideMaterial(i, material);
				materials.Add(material);
			}
			if (portal && motes.Count == 0) AddMotes(mesh);
		}
	}

	void AddMotes(MeshInstance3D mesh)
	{
		haloMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Lobby/portal_halo.gdshader") };
		mesh.AddChild(new MeshInstance3D { Name = "PortalHalo", Mesh = new QuadMesh { Size = new Vector2(2.6f, 2.6f) }, Position = new Vector3(0f, -.02f, -.24f), MaterialOverride = haloMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		light = new OmniLight3D { Name = "PortalRadiance", Position = new Vector3(0f, -.05f, .35f), OmniRange = 2.2f, LightColor = Color.FromHtml("#99DAFF"), LightEnergy = .7f, ShadowEnabled = false };
		mesh.AddChild(light);
		var shape = new QuadMesh { Size = new Vector2(.24f, .24f) };
		for (var i = 0; i < 24; i++)
		{
			var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Lobby/portal_mote.gdshader") };
			material.SetShaderParameter("offset", i * .73f);
			var mote = new MeshInstance3D { Name = $"EnergyMote{i:00}", Mesh = shape, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			mesh.AddChild(mote);
			motes.Add(mote);
		}
	}

	public override void _Process(double delta)
	{
		var reduced = reduceMotion();
		if (!portal)
		{
			player ??= GetTree().CurrentScene.GetNodeOrNull<Player>("Player");
			var busy = reduced || player?.Enabled != true;
			if (busy) { gesture = 0; gestureTime = 0; gestureWait = Math.Max(gestureWait, 1.5f); }
			else if (gesture == 0)
			{
				gestureWait -= (float)delta;
				if (gestureWait <= 0) { gesture = previousGesture == 1 ? 2 : 1; previousGesture = gesture; gestureTime = 0; }
			}
			else { gestureTime += (float)delta; if (gestureTime >= 4.5f) { gesture = 0; gestureWait = random.RandfRange(2.5f, 5.5f); } }
			var envelope = gesture == 0 ? 0f : Mathf.Pow(Mathf.Sin(Mathf.Pi * gestureTime / 4.5f), 2f);
			look = Mathf.MoveToward(look, gesture == 1 ? envelope * Mathf.Sin(gestureTime * Mathf.Tau / 4.5f) : 0f, (float)delta * 2f);
			stretch = Mathf.MoveToward(stretch, gesture == 2 ? envelope : 0f, (float)delta * 2f);
		}
		if (!reduced) phase += Math.Min(delta, .1);
		haloMaterial?.SetShaderParameter("strength", reduceEffects() ? .25f : .94f + .08f * Mathf.Sin((float)phase * 1.35f));
		if (light is not null) light.LightEnergy = reduceEffects() ? .15f : .7f * (.94f + .08f * Mathf.Sin((float)phase * 1.35f));
		foreach (var material in materials)
		{
			material.SetShaderParameter("phase", (float)phase);
			if (portal) material.SetShaderParameter("effect_strength", reduceEffects() ? .35f : 1f);
			else
			{
				material.SetShaderParameter("motion", reduced ? 0f : 1f);
				material.SetShaderParameter("look", look);
				material.SetShaderParameter("stretch", stretch);
			}
		}
		for (var i = 0; i < motes.Count; i++)
		{
			var mote = motes[i];
			mote.Visible = !reduced && !reduceEffects();
			var progress = Mathf.PosMod((float)phase / 4.2f + i / 24f, 1f);
			var radius = (1.15f + .12f * (i % 3)) * Mathf.Pow(1f - progress, 1.25f);
			var angle = i * 2.39996f + progress * 2.5f;
			mote.Position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .78f, .08f + .72f * (1f - progress));
			var fade = Mathf.SmoothStep(0f, 1f, progress / .12f) * (1f - Mathf.SmoothStep(0f, 1f, (progress - .84f) / .16f));
			mote.Scale = Vector3.One * fade;
			var camera = GetViewport().GetCamera3D();
			if (camera is not null && fade > .001f) mote.LookAt(camera.GlobalPosition, Vector3.Up);
			((ShaderMaterial)mote.MaterialOverride).SetShaderParameter("phase", (float)phase);

		}
	}

	public Godot.Collections.Dictionary Diagnostics() => new()
	{
		["gesture"] = gesture,
		["look"] = look,
		["stretch"] = stretch,
		["portal"] = portal,
		["materials"] = materials.Count,
		["particles"] = motes.Count,
		["phase"] = phase,
		["reduced_motion"] = reduceMotion(),
		["reduced_effects"] = reduceEffects(),
	};
}
