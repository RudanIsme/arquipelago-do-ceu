using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

public static class MeshyAssets
{
	public const string Folder = "res://assets/modelos/meshy-teste-20261019/";
	public static bool Enabled => OS.IsDebugBuild() && OS.GetCmdlineUserArgs().Contains("--meshy-test");

	public static Node3D Load(string asset, string clip = "") =>
		GD.Load<PackedScene>(Folder + asset + (clip.Length > 0 ? "-" + clip : "") + ".glb").Instantiate<Node3D>();

	public static Node3D Add(Node3D parent, string asset, Vector3 ground, float width, float height, float yaw = 0f)
	{
		var model = Load(asset);
		parent.AddChild(model);
		Fit(model, ground, width, height, yaw);
		return model;
	}

	public static void Fit(Node3D model, Vector3 ground, float width, float height, float yaw = 0f)
	{
		model.Rotation = new Vector3(0f, yaw, 0f);
		var box = Bounds(model);
		var factor = Mathf.Min(width / Mathf.Max(Mathf.Max(box.Size.X, box.Size.Z), 0.001f), height / Mathf.Max(box.Size.Y, 0.001f));
		model.Scale *= factor;
		box = Bounds(model);
		model.GlobalPosition += ground - new Vector3(box.GetCenter().X, box.Position.Y, box.GetCenter().Z);
	}

	public static Aabb Bounds(Node3D model)
	{
		Aabb? result = null;
		foreach (var mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			var box = mesh.GlobalTransform * mesh.GetAabb();
			result = result is null ? box : result.Value.Merge(box);
		}
		return result ?? new Aabb(Vector3.Zero, Vector3.One);
	}
}
