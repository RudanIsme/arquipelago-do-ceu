using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

public partial class MeshyCreature : Node3D
{
	readonly string asset;
	Node3D idle = null!;
	Node3D walk = null!;
	Vector3 previous;
	bool walking;

	public MeshyCreature() : this("criaturas/automato-base") { }
	public MeshyCreature(string asset) => this.asset = asset;
	public TrainingDummy? Follow { get; set; }
	public bool PreviewWalk { get; set; }

	public override void _Ready()
	{
		idle = AddClip("idle");
		walk = AddClip("walk");
		walk.Visible = false;
		previous = GlobalPosition;
	}

	Node3D AddClip(string clip)
	{
		var model = MeshyAssets.Load(asset, clip);
		AddChild(model);
		MeshyAssets.Fit(model, GlobalPosition, 1.6f, 1.7f);
		foreach (var animation in model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>())
		{
			var name = animation.GetAnimationList().First(n => n != "RESET");
			animation.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
			animation.Play(name);
		}
		return model;
	}

	public override void _Process(double delta)
	{
		if (Follow is not null) Position = new Vector3(Follow.Center.X, 0f, Follow.Center.Z);
		var motion = GlobalPosition - previous;
		var moves = Follow is null ? PreviewWalk : motion.LengthSquared() > 0.000001f && !Follow.ReduceMotion;
		if (moves != walking)
		{
			walking = moves;
			idle.Visible = !moves;
			walk.Visible = moves;
		}
		if (Follow is not null && moves) Rotation = new Vector3(0f, Mathf.Atan2(motion.X, motion.Z), 0f);
		previous = GlobalPosition;
	}
}
