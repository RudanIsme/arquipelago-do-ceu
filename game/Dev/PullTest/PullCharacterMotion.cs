using Godot;
using System;
using System.Linq;
namespace ArquipelagoDoCeu;
public sealed class PullCharacterMotion
{
	readonly Skeleton3D rig; readonly AnimationPlayer anim; readonly Node3D model;
	readonly string[] pulls, attacks = { "approved/atacando", "approved/ataque2", "approved/ataque3" }; readonly string idle = "approved/idle";
	readonly Quaternion[][] pullBase = new Quaternion[3][]; readonly Vector3[] savedPos, savedScale; readonly Quaternion[] savedRot; readonly MeshInstance3D[] capes = new MeshInstance3D[3]; readonly MeshInstance3D baseCape;
	readonly float[] attackDuration = new float[3]; float idleClock;
	public const float Entry = .28f, Return = .36f;
	public string State { get; private set; } = "Idle animado"; public bool IsCapturing { get; private set; }
	public float TotalDuration(int index, float span) => Entry + span + attackDuration[index] + Return;
	public PullCharacterMotion(Node3D model, Skeleton3D skeleton, AnimationPlayer player, string[] pullNames)
	{
		this.model = model; rig = skeleton; anim = player; pulls = pullNames.Select(n => anim.GetAnimationList().First(a => a.ToString().Contains(n)).ToString()).ToArray();
		baseCape = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().First(m => m.Mesh is ArrayMesh a && Enumerable.Range(0, a.GetBlendShapeCount()).Any(i => a.GetBlendShapeName(i).ToString().StartsWith("Cloth_parado")));
		var lib = new AnimationLibrary(); anim.AddAnimationLibrary("approved", lib);
		Import("res://assets/modelos/spectrum-cape-body-v2/guerreiro-parado.glb", "idle", -1, lib);
		for (int i = 0; i < 3; i++) Import("res://Dev/PullTest/" + attacks[i].Split('/')[1] + ".glb", attacks[i].Split('/')[1], i, lib);
		savedPos = new Vector3[rig.GetBoneCount()]; savedScale = new Vector3[rig.GetBoneCount()]; savedRot = new Quaternion[rig.GetBoneCount()];
		for (int n = 0; n < 3; n++) { Sample(pulls[n], 0); pullBase[n] = Enumerable.Range(0, rig.GetBoneCount()).Select(rig.GetBonePoseRotation).ToArray(); }
		Sample(idle, 0);
	}
	void Import(string file, string name, int index, AnimationLibrary lib)
	{
		var sourceModel = GD.Load<PackedScene>(file).Instantiate<Node3D>(); model.AddChild(sourceModel); sourceModel.Visible = false;
		var source = sourceModel.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
		var clip = (Animation)source.GetAnimation(source.GetAnimationList().First(n => n != "RESET")).Duplicate();
		if (index >= 0) { var original = sourceModel.FindChildren("ApprovedCape_" + name, "MeshInstance3D", true, false).OfType<MeshInstance3D>().First(); var transform = model.GlobalTransform.AffineInverse() * original.GlobalTransform; var cape = (MeshInstance3D)original.Duplicate(); model.AddChild(cape); cape.Transform = transform; cape.Visible = false; capes[index] = cape; attackDuration[index] = (float)clip.Length; }
		var targetRoot = anim.GetNode(anim.RootNode); var sourceRoot = source.GetNode(source.RootNode);
		for (int t = clip.GetTrackCount() - 1; t >= 0; t--)
		{
			string txt = clip.TrackGetPath(t).ToString(); int colon = txt.IndexOf(':'); string sub = colon >= 0 ? txt[(colon + 1)..] : ""; string nodePath = colon >= 0 ? txt[..colon] : txt;
			if (sub.Length > 0 && rig.FindBone(sub) >= 0) { clip.TrackSetPath(t, new NodePath(targetRoot.GetPathTo(rig) + ":" + sub)); continue; }
			if (index >= 0 && clip.TrackGetType(t) != Animation.TrackType.BlendShape) { clip.RemoveTrack(t); continue; }
			var oldNode = sourceRoot.GetNodeOrNull(nodePath); Node? target = oldNode == null ? null : model.FindChildren(oldNode.Name, "", true, false).FirstOrDefault(n => !sourceModel.IsAncestorOf(n));
			if (clip.TrackGetType(t) == Animation.TrackType.BlendShape)
			{
				if (index >= 0 && oldNode?.Name.ToString().StartsWith("ApprovedCape_") == true) target = capes[index];
				else target = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault(m => !sourceModel.IsAncestorOf(m) && m.Mesh is ArrayMesh a && Enumerable.Range(0, a.GetBlendShapeCount()).Any(i => a.GetBlendShapeName(i).ToString() == sub));
			}
			if (target != null) clip.TrackSetPath(t, new NodePath(targetRoot.GetPathTo(target) + (colon >= 0 ? ":" + sub : ""))); else clip.RemoveTrack(t);
		}
		if (index >= 0) { for (int t = 0; t < clip.GetTrackCount(); t++) for (int k = 0; k < clip.TrackGetKeyCount(t); k++) clip.TrackSetKeyTime(t, k, Math.Max(0, clip.TrackGetKeyTime(t, k) - 1.0 / 30)); clip.Length -= 1.0 / 30; attackDuration[index] = (float)clip.Length; }
		clip.LoopMode = index < 0 ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None; lib.AddAnimation(name, clip);
		if (index < 0) { var oldSword = sourceModel.FindChildren("PROP_SwordGrip", "", true, false).OfType<Node3D>().First(); var sword = model.FindChildren("PROP_SwordGrip", "", true, false).OfType<Node3D>().First(n => !sourceModel.IsAncestorOf(n)); sword.Transform = oldSword.Transform; }
		GD.Print($"APPROVED_CLIP {name} length={clip.Length} tracks={clip.GetTrackCount()} source={file}"); sourceModel.Free();
	}
	void Sample(string clip, double at) { rig.ResetBonePoses(); anim.Play(clip); anim.Seek(at, true); anim.Pause(); }
	void Snapshot() { for (int i = 0; i < rig.GetBoneCount(); i++) { savedPos[i] = rig.GetBonePosePosition(i); savedRot[i] = rig.GetBonePoseRotation(i); savedScale[i] = rig.GetBonePoseScale(i); } }
	void Blend(float t) { t = Mathf.SmoothStep(0, 1, Mathf.Clamp(t, 0, 1)); for (int i = 0; i < rig.GetBoneCount(); i++) { rig.SetBonePosePosition(i, savedPos[i].Lerp(rig.GetBonePosePosition(i), t)); rig.SetBonePoseRotation(i, savedRot[i].Slerp(rig.GetBonePoseRotation(i), t)); rig.SetBonePoseScale(i, savedScale[i].Lerp(rig.GetBonePoseScale(i), t)); } }
	public void Begin() { }
	public void Update(double delta, bool playing, int index, float elapsed, float span)
	{
		idleClock += (float)delta; double idleTime = idleClock % anim.GetAnimation(idle).Length; float attackTime = elapsed - Entry - span;
		IsCapturing = playing && attackTime < 0; bool attacking = playing && attackTime >= 0 && attackTime < attackDuration[index];
		baseCape.Visible = !attacking; for (int n = 0; n < 3; n++) capes[n].Visible = attacking && n == index;
		if (!playing) { State = "Idle animado"; Sample(idle, idleTime); return; }
		if (IsCapturing)
		{
			State = "Captura sobre idle"; float q = Mathf.Clamp((elapsed - Entry) / span, 0, 1); Sample(pulls[index], q * anim.GetAnimation(pulls[index]).Length); Snapshot(); Sample(idle, idleTime);
			float envelope = Mathf.SmoothStep(0, 1, Mathf.Clamp(q / .12f, 0, 1)) * Mathf.SmoothStep(0, 1, Mathf.Clamp((1 - q) / .12f, 0, 1));
			for (int i = 0; i < rig.GetBoneCount(); i++)
			{
				string bone = rig.GetBoneName(i); float weight = bone == "LeftShoulder" ? .4f : bone == "LeftArm" || bone == "LeftForeArm" || bone == "LeftHand" || bone.StartsWith("Contour_") ? 1 : 0;
				if (weight == 0) continue; var deltaRot = pullBase[index][i].Inverse() * savedRot[i]; rig.SetBonePoseRotation(i, rig.GetBonePoseRotation(i) * Quaternion.Identity.Slerp(deltaRot, weight * envelope));
			}
			return;
		}
		if (attacking) { State = "Golpe " + new[] { "A", "B", "C" }[index] + " completo"; Sample(attacks[index], attackTime); return; }
		State = "Recuperação → idle"; Sample(attacks[index], attackDuration[index]); Snapshot(); Sample(idle, idleTime); Blend((attackTime - attackDuration[index]) / Return);
	}
}
