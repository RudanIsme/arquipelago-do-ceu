using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ArquipelagoDoCeu.World;

namespace ArquipelagoDoCeu;

public partial class Lobby
{
    readonly List<ShaderMaterial> landmarkWind = new();
    ShaderMaterial? waterfallMaterial;
    float landscapeTime;

    void PrepareLandmark(Node3D model, string id)
    {
        model.Name = "Landmark_" + id;
        if (id != "ciencias") return;
        var shader = new Shader { Code = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D leaf_texture : source_color;
uniform float wind_time = 0.0;
uniform float wind_strength = 1.0;
void vertex() {
    float sway = sin(wind_time * 1.3 + VERTEX.y * 3.5 + VERTEX.z * 2.0);
    VERTEX.x += sway * 0.008 * wind_strength * smoothstep(0.0, 0.6, VERTEX.y);
}
void fragment() { ALBEDO = texture(leaf_texture, UV).rgb; ROUGHNESS = 0.85; }
" };
        foreach (var mesh in model.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
        {
            if (!mesh.Name.ToString().Contains("Foliage")) continue;
            for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
            {
                if (mesh.GetActiveMaterial(s) is not StandardMaterial3D source) continue;
                var material = new ShaderMaterial { Shader = shader };
                material.SetShaderParameter("leaf_texture", source.AlbedoTexture);
                mesh.SetSurfaceOverrideMaterial(s, material);
                landmarkWind.Add(material);
            }
        }
    }

    void AddConceptWaterfall()
    {
        var root = new Node3D { Name = "ConceptWaterfall" };
        AddChild(root);
        var direction = new Vector3(Mathf.Sqrt(3f) / 2f, 0, .5f);
        var across = new Vector3(-direction.Z, 0, direction.X);
        var bank = HexToWorld(5, -2) + direction * .87f + Vector3.Down * .10f;
        // A rocha inferior avança além do hexágono; a lâmina deve cair fora desse contorno.
        var lip = bank + direction * .55f;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(float t, float side)
        {
            float width = Mathf.Lerp(.65f, .80f, t);
            return lip + across * side * width + direction * (.75f * t * t)
                + Vector3.Down * (4.6f * t);
        }
        void Vertex(float t, float side)
        {
            surface.SetUV(new Vector2((side + 1f) / 2f, t));
            surface.AddVertex(Point(t, side));
        }
        // Recobre o encontro do último hexágono com a borda, evitando uma faixa de rocha entre rio e queda.
        var inlet = HexToWorld(5, -2) + Vector3.Down * .08f;
        void Inlet(float side) {
            surface.SetUV(new Vector2((side + 1f) / 2f, .15f));
            surface.AddVertex(inlet + across * side * .65f);
        }
        Inlet(-1); Vertex(0, -1); Inlet(1);
        Inlet(1); Vertex(0, -1); Vertex(0, 1);
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f, b = (i + 1) / 24f;
            Vertex(a, -1); Vertex(b, -1); Vertex(a, 1);
            Vertex(a, 1); Vertex(b, -1); Vertex(b, 1);
        }
        surface.GenerateNormals();
        var shader = new Shader { Code = @"
shader_type spatial;
render_mode cull_disabled, blend_mix, depth_prepass_alpha;
uniform float flow_time = 0.0;
uniform float motion = 1.0;
void vertex() {
    VERTEX.x += sin(UV.y * 17.0 - flow_time * 3.0) * 0.025 * motion * UV.y;
}
void fragment() {
    float lanes = pow(0.5 + 0.5 * sin(UV.x * 54.0 + sin(UV.x * 19.0) * 2.0), 14.0);
    float drops = 0.25 + 0.75 * pow(0.5 + 0.5 * sin(UV.y * 22.0 - flow_time * 5.0 + sin(UV.x * 31.0) * 3.0), 3.0);
    float foam = max(lanes * drops * 0.8, (1.0 - smoothstep(0.0, 0.08, UV.y)) * 0.7);
    ALBEDO = mix(vec3(0.06,0.47,0.64), vec3(0.78,0.96,0.94), clamp(foam,0.0,1.0));
    ROUGHNESS = 0.32;
    ALPHA = (1.0 - smoothstep(0.80, 1.0, UV.y)) * smoothstep(0.0, 0.04, UV.x) * (1.0 - smoothstep(0.96, 1.0, UV.x));
}
" };
        waterfallMaterial = new ShaderMaterial { Shader = shader };
        root.AddChild(new MeshInstance3D { Name = "WaterCurtain", Mesh = surface.Commit(), MaterialOverride = waterfallMaterial });
        var stone = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#B8A67D"), Roughness = .95f };
        foreach (int side in new[] {-1, 1})
            root.AddChild(new MeshInstance3D {
                Name = side < 0 ? "LeftBankRock" : "RightBankRock",
                Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 4 },
                Position = bank + across * (side * .9f) + Vector3.Up * .20f,
                Scale = new Vector3(.28f, .34f, .34f), MaterialOverride = stone
            });
    }

    void TickLandmarks(double delta)
    {
        if (!ReduceMotion) landscapeTime += (float)delta;
        waterfallMaterial?.SetShaderParameter("flow_time", landscapeTime);
        waterfallMaterial?.SetShaderParameter("motion", ReduceMotion ? 0f : 1f);
        foreach (var material in landmarkWind)
        {
            material.SetShaderParameter("wind_time", landscapeTime);
            material.SetShaderParameter("wind_strength", ReduceMotion ? 0f : 1f);
        }
    }

    public Godot.Collections.Dictionary ValidateLandmarks()
    {
        var validation = ValidateLote02();
        var reverse = new MovementController(Grid, HexGrid.ToWorld(new HexCell(2,-1)), 3f);
        bool reverseAccepted = reverse.GoTo(HexGrid.ToWorld(new HexCell(2,-3)));
        for(int i=0;i<240;i++) reverse.Tick(System.Numerics.Vector2.Zero, 1f/60f);
        validation["ponte_sentido_inverso"] = reverseAccepted && System.Numerics.Vector2.Distance(reverse.Position, HexGrid.ToWorld(new HexCell(2,-3))) < .02f;
        validation["sem_rota_pela_agua"] = Grid.FindPath(new HexCell(0,0), new HexCell(4,-2)) is null;
        validation["canal_bloqueado"] = !Grid.IsWalkable(new HexCell(4,-2)) && !Grid.IsWalkable(new HexCell(5,-2));
        validation["cascata_presente"] = FindChild("ConceptWaterfall", true, false) is not null;
        foreach (var id in new[] { "ciencias", "historia", "geografia" })
        {
            var model = FindChild("Landmark_" + id, true, false) as Node3D;
            validation[id + "_presente"] = model is not null;
            if (model is not null) validation[id + "_bounds"] = WorldBox(model);
        }
        validation["folhagem_separada"] = landmarkWind.Count > 0;
        return validation;
    }
}
