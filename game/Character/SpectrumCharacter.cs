using System.Linq;
using ArquipelagoDoCeu.Equipment;
using Godot;

namespace ArquipelagoDoCeu;

// Variante visual de teste; movimento e colisao continuam sob responsabilidade do Player.
public partial class SpectrumCharacter : Node3D
{
	AnimationPlayer animations = null!;
	DynamicCape dynamicCape = null!;
	EquipmentVisuals equipment = null!;
	EquipmentLoadout? pendingEquipment;
	string idle = "";
	string walk = "spectrum/andando";
	string run = "spectrum/correndo";
	const float WalkStrideSpeed = .5544f;
	const float RunStrideSpeed = 3.305f;
	public bool ReduceMotion { get; set; }
	public bool IsWalking => animations is not null && animations.IsPlaying() && (animations.CurrentAnimation == walk || animations.CurrentAnimation == run);

	public override void _Ready()
	{
		var model = GD.Load<PackedScene>("res://assets/modelos/spectrum-cape-body-v2/guerreiro-parado.glb").Instantiate<Node3D>();
		AddChild(model);
		// Altura da pele medida no Blender na guarda; preserva o chao e a mesma escala em ambos os clips.
		model.Scale = Vector3.One * (1.5f / 1.35681236f);
		animations = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
		idle = animations.GetAnimationList().First(n => n != "RESET");
		var library = new AnimationLibrary();
		foreach (var name in new[] { "andando", "correndo" })
		{
			var motion = GD.Load<PackedScene>($"res://assets/modelos/spectrum-cape-body-v2/guerreiro-{name}.glb").Instantiate<Node3D>();
			var source = motion.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
			var clip = (Animation)source.GetAnimation(source.GetAnimationList().First(n => n != "RESET")).Duplicate();
			clip.LoopMode = Animation.LoopModeEnum.Linear;
			library.AddAnimation(name, clip);
			motion.Free();
		}
		animations.AddAnimationLibrary("spectrum", library);
		animations.GetAnimation(idle).LoopMode = Animation.LoopModeEnum.Linear;
		animations.Play(idle);
		animations.Advance(0);
		var cape = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().First(m => m.Name.ToString().Contains("PROP_Cape"));
		var rig = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
		dynamicCape = new DynamicCape();
		dynamicCape.Initialize(model, cape, rig);
		AddChild(dynamicCape);
		// A capa aprovada e pre-calculada e o padrao; F8 compara o experimento.
		dynamicCape.Toggle();
		equipment = new EquipmentVisuals();
		model.AddChild(equipment);
		equipment.Initialize(model, rig);
		if (pendingEquipment is not null) equipment.Apply(pendingEquipment);
	}

	public void ApplyEquipment(EquipmentLoadout loadout) { pendingEquipment = loadout; if (equipment is not null) equipment.Apply(loadout); }

	public void ToggleCapeMode() => dynamicCape.Toggle();

	public void SetLocomotion(float speed, bool running)
	{
		dynamicCape.SetReduced(ReduceMotion);
		var moving = speed > .015f && !ReduceMotion;
		var desired = moving ? (running ? run : walk) : idle;
		// Velocidade de apoio medida nos pes da fonte, ja convertida para a escala de 1,5 unidade.
		animations.SpeedScale = moving ? speed / (running ? RunStrideSpeed : WalkStrideSpeed) : 1f;
		if (animations.CurrentAnimation != desired) animations.Play(desired, .08);
		if (ReduceMotion) { animations.Seek(0, true); animations.Pause(); }
		else if (!animations.IsPlaying()) animations.Play(desired);
	}
}
