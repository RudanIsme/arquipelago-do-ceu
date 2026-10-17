using System;
using System.Linq;
using Godot;
namespace ArquipelagoDoCeu;

public partial class GuideIdle : Node
{
	readonly Node3D model;
	readonly Func<bool> reduced;
	readonly RandomNumberGenerator random = new();
	AnimationPlayer animations = null!;
	Player? player;
	string last = "";
	string current = "Repouso";
	float remaining = 1.5f;
	float settling;
	bool wasReduced;
	static readonly string[] Gestures = ["Bocejo", "Olhar", "Alongar"];
	public GuideIdle(Node3D model, Func<bool> reduced)
	{ this.model = model; this.reduced = reduced; Name = "GuideIdle"; }
	public override void _Ready()
	{
		animations = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
		random.Randomize();
		animations.GetAnimation("Repouso").LoopMode = Animation.LoopModeEnum.Linear;
		animations.AnimationFinished += _ => Rest();
		animations.Play("Repouso");
	}
	void Rest()
	{
		current = "Repouso";
		animations.Play(current, .45);
		remaining = random.RandfRange(2.5f, 5.5f);
	}
	public override void _Process(double delta)
	{
		player ??= GetTree().CurrentScene.GetNodeOrNull<Player>("Player");
		var reduce = reduced();
		if (reduce)
		{
			if (!wasReduced) { Rest(); settling = .5f; }
			settling -= (float)delta;
			if (settling <= 0) { animations.Seek(0, true); animations.Pause(); }
			wasReduced = true;
			return;
		}
		if (wasReduced) { wasReduced = false; Rest(); }
		if (player?.Enabled != true)
		{
			if (current != "Repouso") Rest();
			remaining = Math.Max(remaining, 1.5f);
			return;
		}
		if (current != "Repouso") return;
		remaining -= (float)delta;
		if (remaining > 0) return;
		var choices = Gestures.Where(g => g != last).ToArray();
		PlayGesture(last.Length == 0 ? "Bocejo" : choices[random.RandiRange(0, choices.Length - 1)]);
	}
	public void PlayGesture(string name)
	{
		if (reduced() || player?.Enabled != true || !Gestures.Contains(name)) return;
		last = current = name;
		animations.Play(name, .35);
	}
	public Godot.Collections.Dictionary Diagnostics() => new()
	{
		["clip"] = current,
		["previous_gesture"] = last,
		["wait_seconds"] = remaining,
		["reduced_motion"] = reduced(),
		["bones"] = 6,
		["playing"] = animations.IsPlaying(),
		["position"] = animations.CurrentAnimationPosition,
	};
}
