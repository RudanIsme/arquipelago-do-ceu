using System.Collections.Generic;
using ArquipelagoDoCeu.World;
using Godot;

namespace ArquipelagoDoCeu;

// Dia e noite do Lobby: só luz, céu e lampiões, no tempo do jogo (nunca no relógio do aparelho). Dentro das torres a luz é fixa.
public partial class Lobby
{
	static readonly HexCell[] LampCells = [new(0, -3), new(0, 3), new(-3, 0), new(3, 0)];
	static readonly Color DaySky = Color.FromHtml("#A8D1F5");
	static readonly Color NightSky = Color.FromHtml("#1E2547");
	static readonly Color DayAmbient = Color.FromHtml("#D6E2FF");
	static readonly Color NightAmbient = Color.FromHtml("#6F7BB5");
	static readonly Color DaySun = Color.FromHtml("#FFE4BC");
	static readonly Color DuskSun = Color.FromHtml("#FFB070");

	readonly List<(OmniLight3D Light, MeshInstance3D Bulb)> lamps = new();
	DirectionalLight3D? sun;
	bool fixedDay;
	DayCycle? cycle;
	float dayClock;

	public void SetFixedDay(bool fixedValue)
	{
		fixedDay = fixedValue;
		cycle = null;
		if (Rules is not null) TickDay();
	}

	void TickDay()
	{
		if (environment is null || sun is null) return;
		cycle ??= new DayCycle(Rules.DayCycleSeconds, fixedDay);
		if (!fixedDay) dayClock += (float)GetProcessDeltaTime();

		var light = cycle.Daylight(dayClock);
		environment.BackgroundColor = NightSky.Lerp(DaySky, light);
		environment.AmbientLightColor = NightAmbient.Lerp(DayAmbient, light);
		environment.AmbientLightEnergy = Mathf.Lerp(0.45f, 0.7f, light);
		sun.LightEnergy = Mathf.Lerp(0.12f, 1.2f, light);
		sun.LightColor = DuskSun.Lerp(DaySun, Mathf.Clamp((light - 0.3f) / 0.5f, 0f, 1f));

		var on = cycle.LampsOn(dayClock);
		foreach (var (lamp, bulb) in lamps)
		{
			lamp.Visible = on;
			bulb.Visible = on;
			lamp.LightEnergy = reducedEffects ? 0.5f : 1.2f;
		}
	}

	void AddLamps()
	{
		var post = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#5B4A3A"), Roughness = 0.9f };
		var glow = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#FFD27A"), EmissionEnabled = true, Emission = Color.FromHtml("#FFB84D"), EmissionEnergyMultiplier = 2f };
		foreach (var cell in LampCells)
		{
			// Fica na borda do hexágono para não atrapalhar quem anda no caminho.
			var at = HexToWorld(cell.Q, cell.R) + new Vector3(0.62f, 0f, 0.35f);
			AddChild(new MeshInstance3D { Name = $"LampPost{cell.Q}_{cell.R}", Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 1.1f }, MaterialOverride = post, Position = at + new Vector3(0f, 0.55f, 0f) });
			var bulb = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.14f, Height = 0.28f }, MaterialOverride = glow, Position = at + new Vector3(0f, 1.2f, 0f), Visible = false };
			var lamp = new OmniLight3D { Position = at + new Vector3(0f, 1.2f, 0f), OmniRange = 3.5f, LightColor = Color.FromHtml("#FFC060"), Visible = false };
			AddChild(bulb);
			AddChild(lamp);
			lamps.Add((lamp, bulb));
		}
	}
}
