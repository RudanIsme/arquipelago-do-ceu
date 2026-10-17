using Godot;
using System.Linq;
using System;
using ArquipelagoDoCeu.Rules;
using System.Collections.Generic;
using ArquipelagoDoCeu.Combat;

namespace ArquipelagoDoCeu;
public partial class PullTestArena : Node3D
{
	PullTestStage lobby = null!; Node3D hero = null!; Skeleton3D skeleton = null!; AnimationPlayer anim = null!;
	MeshInstance3D[] rings = new MeshInstance3D[4]; ShaderMaterial[] mats = new ShaderMaterial[4]; OmniLight3D captureLight = null!; Label3D syllable = null!;
	MeshInstance3D burst = null!; ShaderMaterial burstMat = null!; public float FlashStrength = 1f;
	PullCharacterMotion motion = null!;
	float clock; int last = -1, hand, forearm; Basis foreRest; bool captured;
	string[] clips = { "Puxada_Precisa", "Puxada_Diagonal", "Puxada_Carregada" }; float[] duration = { 2.4f, 3f, 3.6f };
	public override void _Ready()
	{
		lobby = new PullTestStage(); AddChild(lobby);
		lobby.Camera.Projection = Camera3D.ProjectionType.Perspective; lobby.Camera.Fov = 39; lobby.Camera.Attributes = null;
		hero = GD.Load<PackedScene>("res://Dev/PullTest/warrior-face-sword-v3.glb").Instantiate<Node3D>(); AddChild(hero); hero.Scale = Vector3.One * 1.1f; hero.Position = new Vector3(0, lobby.Ground, 0);
		skeleton = hero.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First(); anim = hero.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
		hand = skeleton.FindBone("LeftHand"); forearm = skeleton.FindBone("LeftForeArm"); foreRest = skeleton.GetBoneGlobalRest(forearm).Basis;
		GD.Print("MODULAR_IMPORT clips=" + string.Join(",", anim.GetAnimationList()) + " bones=" + skeleton.GetBoneCount());
		motion = new PullCharacterMotion(hero, skeleton, anim, clips);
		string[] layers = { "rings", "geometry", "inscriptions", "anchors" };
		var shader = new Shader { Code = "shader_type spatial; render_mode unshaded,cull_disabled,depth_draw_never; uniform sampler2D brush:source_color; uniform sampler2D glow:source_color; uniform float opacity=1.0; uniform float capture_flash=0.0; void fragment(){vec4 b=texture(brush,UV);vec4 e=texture(glow,UV);ALBEDO=mix(b.rgb,vec3(0.86,0.98,1.0),capture_flash*0.65);vec3 vivid=mix(vec3(0.04,0.8,1.0),vec3(0.65,0.12,1.0),step(e.b,e.r*1.4));EMISSION=vivid*max(e.r,max(e.g,e.b))*(3.5+capture_flash*42.0)+vec3(1.0)*capture_flash*12.0*b.a;ALBEDO+=EMISSION;ALPHA=max(b.a,e.a*0.48)*opacity;}" };
		for (int i = 0; i < 4; i++)
		{
			rings[i] = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(.46f, .46f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(rings[i]);
			mats[i] = new ShaderMaterial { Shader = shader }; mats[i].SetShaderParameter("brush", GD.Load<Texture2D>($"res://Dev/PullTest/arcane-v2/arcane-brush-{layers[i]}.png")); mats[i].SetShaderParameter("glow", GD.Load<Texture2D>($"res://Dev/PullTest/arcane-v2/arcane-emissive-{layers[i]}.png")); rings[i].MaterialOverride = mats[i];
		}
		captureLight = new OmniLight3D { LightColor = new Color("b8faff"), OmniRange = .29f, LightEnergy = 0, ShadowEnabled = false }; AddChild(captureLight);
		burst = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(.9f, .9f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(burst);
		burstMat = new ShaderMaterial { Shader = new Shader { Code = "shader_type spatial;render_mode unshaded,cull_disabled,blend_add,depth_draw_never;uniform float age=-1.0;uniform float strength=1.0;void fragment(){vec2 p=(UV-.5)*2.0;float r=length(p);float t=max(age,0.0);float peak=exp(-t*20.0);float core=exp(-r*r/0.025)*peak;float halo=exp(-r*r/0.20)*peak;float wave=exp(-pow((r-(.20+t*1.8))/.035,2.0))*exp(-t*9.0);float ang=atan(p.y,p.x);float rays=pow(max(0.0,cos(ang*13.0+t*4.0)),36.0)*exp(-pow((r-(.25+t*1.3))/.16,2.0))*exp(-t*12.0);vec3 hue=mix(vec3(.02,.55,1.0),vec3(.65,.04,1.0),.5+.5*sin(ang*3.0));ALBEDO=(vec3(1.0)*core*45.0+hue*(halo*5.0+wave*10.0+rays*15.0))*strength;ALPHA=(age>=0.0&&age<.45)?1.0:0.0;}" } }; burst.MaterialOverride = burstMat;
		syllable = new Label3D { FontSize = 55, PixelSize = .0009f, Modulate = new Color("f6edce"), OutlineModulate = new Color("3c4b52"), OutlineSize = 14, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = false }; AddChild(syllable);
		BuildControls();
	}

	public NameBattle Battle = null!;
	readonly LetterCollection letters = new();
	public bool Letters = true; public bool Drawing { get; private set; }
	public NeonLetterTrace Trace = null!;
	public IReadOnlyList<string> Pieces => Letters ? letters.Pieces : Battle.Pieces;
	public IReadOnlyList<string> Target => Letters ? letters.Target : Battle.Name;
	public IReadOnlyList<int> Chosen => Letters ? letters.Chosen : Battle.Chosen;
	public string TargetWord => Letters ? string.Concat(Target) : Battle.Completed;
	public bool IsHidden(int i) => !Letters && Battle.IsHidden(i);
	public void CancelDrawing() { if (Drawing) Trace.CancelTrace(); }
	public void ToggleMode() { if (Playing || Drawing || Linked) return; Letters = !Letters; letters.Reset(); result = ""; RebuildPieces(); }
	void StartDrawing() { Drawing = true; Trace.Visible = true; Trace.ResetTrace(); result = ""; RefreshUi(); }

	public Action? ExitRequested, CheckRequested;
	public bool Linked; public bool ReduceEffects, ReduceMotion;
	public int Variant { get; private set; }
	public bool Playing { get; private set; }
	public int CaptureCount { get; private set; }
	public List<int> History = new();
	float elapsed; int pending = -1, nextVariant; string result = "";
	CanvasLayer ui = null!; Control root = null!; Label status = null!, word = null!, flight = null!; Control tiles = null!;
	Button undo = null!, check = null!; List<Button> pieces = new(); Vector2 flightStart;
	public bool CanPick(int i) => !Playing && !Drawing && i >= 0 && i < Pieces.Count && !IsHidden(i) && !Chosen.Contains(i) && Chosen.Count < Target.Count;
	public Vector2 TileCenter(int i) => GetViewport().GetFinalTransform() * pieces[i].GetGlobalRect().GetCenter();
	public bool Pick(int i) { if (!CanPick(i)) { if (Playing) { result = "Espere o retorno ao idle."; root.QueueRedraw(); } return false; } pending = i; Variant = nextVariant; nextVariant = (nextVariant + 1) % 3; Playing = true; motion.Begin(); elapsed = 0; captured = false; result = ""; flightStart = pieces[i].GetGlobalRect().GetCenter() / root.Scale; History.Add(Variant); RefreshUi(); GD.Print($"QTA_PICK piece={i} action={clips[Variant]}"); return true; }
	public bool Capture() { for (int i = 0; i < Pieces.Count; i++) if (CanPick(i)) return Pick(i); return false; }
	public bool Undo() { if (Playing || Drawing) return false; var ok = Letters ? letters.Undo() : Battle.Undo(); result = ""; RefreshUi(); return ok; }
	public void Check() { if (Playing || Drawing) return; if (Letters) { if (letters.Complete) StartDrawing(); else { result = "Ajuste as letras e confira."; RefreshUi(); } return; } if (Linked) { CheckRequested?.Invoke(); return; } var v = Battle.Check(); result = v.Freed ? "Palavra formada! Muito bem!" : v.Mistaken ? "Vamos ajustar? Desfaça a última sílaba." : "Complete a palavra e confira."; RefreshUi(); }
	public void Restart() { if (Playing || Linked) return; CancelDrawing(); if (Letters) letters.Reset(); else Battle.Next(); result = ""; RebuildPieces(); }
	Button MakeButton(string text, Action action) { var b = new Button { Text = text, CustomMinimumSize = new Vector2(110, 38) }; b.AddThemeFontSizeOverride("font_size", 17); foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) b.AddThemeStyleboxOverride(state, FishingHud.Paper(state == "hover" ? "fff7df" : state == "pressed" ? "e8e6b9" : "f8edcf", state == "focus" ? "79dcd5" : "c59750")); foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color" }) b.AddThemeColorOverride(state, new Color("463a35")); b.Pressed += action; return b; }
	void BuildControls()
	{
		ui = new CanvasLayer { Layer = 100 }; AddChild(ui); root = new FishingHud { Arena = this, MouseFilter = Control.MouseFilterEnum.Ignore }; ui.AddChild(root); root.Size = new Vector2(1280, 720);
		var exit = MakeButton("Voltar · Esc", () => ExitRequested?.Invoke()); exit.Position = new Vector2(1094, 26); exit.Size = new Vector2(146, 42); root.AddChild(exit);
		var options = new PanelContainer { Position = new Vector2(970, 82), CustomMinimumSize = new Vector2(270, 0), Visible = false }; options.AddThemeStyleboxOverride("panel", FishingHud.Paper()); root.AddChild(options); var optionsBox = new VBoxContainer(); options.AddChild(optionsBox);
		var fx = new CheckButton { Text = "Reduzir brilho", ButtonPressed = ReduceEffects }; fx.AddThemeColorOverride("font_color", new Color("463a35")); fx.Toggled += v => ReduceEffects = v; optionsBox.AddChild(fx);
		var motion = new CheckButton { Text = "Reduzir movimento", ButtonPressed = ReduceMotion }; motion.AddThemeColorOverride("font_color", new Color("463a35")); motion.Toggled += v => ReduceMotion = v; optionsBox.AddChild(motion);
		if (!Linked) optionsBox.AddChild(MakeButton("Letras / sílabas", ToggleMode));
		if (!Linked) optionsBox.AddChild(MakeButton("Recomeçar palavra", Restart));
		var optionButton = MakeButton("Opções", () => { options.Visible = !options.Visible; options.MoveToFront(); }); optionButton.Position = new Vector2(970, 26); root.AddChild(optionButton);
		tiles = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; root.AddChild(tiles);
		undo = MakeButton("Desfazer", () => Undo()); undo.Position = new Vector2(990, 650); root.AddChild(undo); check = MakeButton(Linked ? "Conferir ↵" : "Conferir ↵", Check); check.Position = new Vector2(1110, 650); root.AddChild(check);
		word = new Label { Visible = false }; root.AddChild(word);
		status = new Label { Position = new Vector2(66, 652), Size = new Vector2(370, 28) }; status.AddThemeFontSizeOverride("font_size", 15); status.AddThemeColorOverride("font_color", new Color("786b60")); root.AddChild(status);
		flight = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Size = new Vector2(88, 54), MouseFilter = Control.MouseFilterEnum.Ignore }; flight.AddThemeFontSizeOverride("font_size", 26); flight.AddThemeColorOverride("font_color", new Color("463a35")); flight.AddThemeStyleboxOverride("normal", FishingHud.Paper()); root.AddChild(flight);
		Trace = new NeonLetterTrace { Visible = false }; root.AddChild(Trace);
		Trace.Cancelled = () => { Drawing = false; result = ""; RefreshUi(); };
		Trace.CompletedAction = () => { result = "Letra concluída!"; RefreshUi(); };
		RebuildPieces(); options.MoveToFront();
	}
	Vector2 PiecePosition(int i) { Vector2[] places = { new(320, 230), new(900, 240), new(1050, 365), new(210, 360), new(355, 405), new(1080, 185), new(200, 480), new(1080, 490), new(350, 150) }; return places[i % places.Length] + new Vector2(0, (i / places.Length) * 65); }
	void RebuildPieces() { foreach (var b in pieces) { tiles.RemoveChild(b); b.QueueFree(); } pieces.Clear(); for (int i = 0; i < Pieces.Count; i++) { int n = i; var b = MakeButton("", () => Pick(n)); b.CustomMinimumSize = new Vector2(88, 54); b.Size = new Vector2(88, 54); b.Position = PiecePosition(i) - b.Size * .5f; b.AddThemeFontSizeOverride("font_size", 26); b.TooltipText = $"Tecla {i + 1} · Clique e solte para pescar"; var caption = new Label { Name = "SyllableCaption", Text = Pieces[i].ToUpperInvariant(), Size = new Vector2(88, 54), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; caption.AddThemeFontOverride("font", ThemeDB.FallbackFont); caption.AddThemeFontSizeOverride("font_size", 26); caption.AddThemeColorOverride("font_color", new Color("463a35")); b.AddChild(caption); tiles.AddChild(b); pieces.Add(b); } RefreshUi(); }
	void RefreshUi() { if (word == null) return; for (int i = 0; i < pieces.Count; i++) { pieces[i].Disabled = IsHidden(i); pieces[i].Visible = !Drawing && !Chosen.Contains(i) && !(Playing && pending == i); pieces[i].GetNode<Label>("SyllableCaption").Text = IsHidden(i) ? "?" : Pieces[i].ToUpperInvariant(); } undo.Visible = check.Visible = !Drawing; undo.Disabled = Playing || Chosen.Count == 0; check.Disabled = Playing || Chosen.Count == 0; root.QueueRedraw(); }
	public string HudResult => result;
	public string MotionState => motion.State;
	public void HandleKey(Key key) { if (key >= Key.Key1 && key <= Key.Key9) Pick((int)key - (int)Key.Key1); else if (key == Key.Space) Capture(); else if (key == Key.Backspace) Undo(); else if (key == Key.Enter || key == Key.KpEnter) Check(); }
	float E(float v) => Mathf.SmoothStep(0, 1, Mathf.Clamp(v, 0, 1));
	public override void _Process(double dt)
	{
		Trace.ReduceEffects = ReduceEffects; Trace.ReduceMotion = ReduceMotion; clock += (float)dt; if (Playing) elapsed += (float)dt;
		int index = Variant; bool vfx = Playing; float playSpan = duration[index] * (ReduceMotion ? 1.35f : 1f); float local = Playing ? Mathf.Max(0, elapsed - PullCharacterMotion.Entry) : 0;
		float q = Playing ? Mathf.Clamp(local / playSpan, 0, 1) : 0; float time = q * duration[index];
		motion.Update(dt, Playing, index, elapsed, playSpan); vfx = Playing && motion.IsCapturing;
		var hp = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(hand); var fp = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(forearm);
		Vector3 axis = (hp.Origin - fp.Origin).Normalized(); var map = fp.Basis * foreRest.Inverse(); Vector3 x = map * new Vector3(-.758555f, -.624128f, .187238f); x = (x - axis * x.Dot(axis)).Normalized(); Vector3 y = axis.Cross(x).Normalized();
		// Bracelet is orthogonal to the actual forearm axis, depth-tested, with an empty center.
		Vector3 center = hp.Origin - axis * .045f; float closeStart = index == 2 ? .52f : index == 1 ? .34f : .39f; float pullStart = index == 2 ? .64f : index == 1 ? .48f : .53f; float pullEnd = index == 2 ? .77f : index == 1 ? .69f : .71f;
		float eventAge = local - pullEnd * playSpan; float flash = eventAge < 0 ? 0 : eventAge < .04f ? eventAge / .04f : eventAge < .22f ? Mathf.Pow(1 - (eventAge - .04f) / .18f, 2) : 0;
		if (vfx && !captured && eventAge >= 0) { captured = true; if (Letters ? letters.Place(pending) : Battle.Place(pending)) { CaptureCount++; GD.Print($"QTA_CAPTURE piece={pending} action={index} count={CaptureCount} time={clock:F3}"); } RefreshUi(); }
		float ambient = ReduceEffects ? .4f : .94f; flash *= ReduceEffects ? .06f : 1f;
		for (int i = 0; i < 4; i++)
		{
			rings[i].GlobalTransform = new Transform3D(new Basis(x, y, axis), center + axis * (i * .0007f)); rings[i].RotateObjectLocal(Vector3.Back, (ReduceMotion ? 0 : clock) * (i == 2 ? -.24f : .33f) + i * .025f);
			rings[i].Scale = Vector3.One * (1 + .015f * Mathf.Sin(clock * 3) + flash * .035f); rings[i].Visible = vfx && q > .16f && q < .93f;
			mats[i].SetShaderParameter("opacity", E((q - .16f - i * .025f) / .12f) * (1 - E((q - .83f) / .10f)) * ambient); mats[i].SetShaderParameter("capture_flash", flash);
		}
		burst.Visible = vfx && !ReduceEffects && eventAge >= 0 && eventAge < .45f; burst.GlobalPosition = center + (lobby.Camera.GlobalPosition - center).Normalized() * .08f; burst.LookAt(lobby.Camera.GlobalPosition); burstMat.SetShaderParameter("age", eventAge); burstMat.SetShaderParameter("strength", FlashStrength);
		captureLight.Position = center; captureLight.LightEnergy = vfx && !ReduceEffects ? (.18f + flash * 9f) * FlashStrength : 0;
		syllable.Visible = false;
		float catchP = E((q - pullStart) / (pullEnd - pullStart));
		root.Scale = Vector2.One * Mathf.Min(GetViewport().GetVisibleRect().Size.X / 1280f, GetViewport().GetVisibleRect().Size.Y / 720f);
		Vector3 focus = new(.12f, .65f, 0); Vector3 restCam = new(.5f, 1.5f, 3.9f);
		Vector3 actionCam = index == 1 ? new Vector3(-1.9f, 1.6f, 2.9f) : index == 2 ? new Vector3(1.9f, 1.22f, 2.8f) : new Vector3(.86f, 1.3f, 2.85f);
		float camBlend = 0; lobby.Camera.LookAtFromPosition(hero.Position + restCam.Lerp(actionCam, camBlend), hero.Position + focus);
		flight.Visible = Playing && !captured; if (pending >= 0) flight.Text = Pieces[pending].ToUpperInvariant();
		var screenHand = lobby.Camera.UnprojectPosition(hp.Origin) / root.Scale; flight.Position = flightStart.Lerp(screenHand, catchP) - flight.Size * .5f + Vector2.Up * Mathf.Sin(catchP * Mathf.Pi) * 65;
		status.Text = Playing ? $"{motion.State}" : "Clique e solte · Teclas 1–9 · Sem pressa";
		var hud = (FishingHud)root; hud.HandPoint = screenHand; hud.Fishing = Playing && !captured; hud.LineEnd = flight.Position + flight.Size * .5f; hud.QueueRedraw();

		if (Playing && elapsed >= motion.TotalDuration(index, playSpan)) { Playing = false; result = ""; RefreshUi(); GD.Print("QTA_COMPLETE " + Variant); }

	}
}
public partial class PullTestStage : Node3D
{
	public Camera3D Camera = null!; public float Ground;
	public override void _Ready()
	{
		var scene = new Lobby { Rules = GameRules.Parse(Godot.FileAccess.GetFileAsString("res://data/rules.json")).Value!.Lobby }; AddChild(scene);
		foreach (var n in scene.FindChildren("*", "Label3D", true, false)) ((Label3D)n).Visible = false;
		foreach (var n in scene.FindChildren("GuideIdle", "", true, false)) if (n.GetParent() is Node3D p) p.Visible = false;
		Camera = scene.Camera; Camera.MakeCurrent(); Ground = scene.GroundHeight(new System.Numerics.Vector2(0, 0));
	}
}

public partial class FishingHud : Control
{
	public PullTestArena Arena = null!; public Vector2 HandPoint, LineEnd; public bool Fishing;
	readonly Font font = ThemeDB.FallbackFont; readonly Color ink = new("463a35"), paper = new("f8edcf"), cyan = new("79f8f0");
	public static StyleBoxFlat Paper(string fill = "f8edcf", string border = "c59750") => new() { BgColor = new Color(fill), BorderColor = new Color(border), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 18, CornerRadiusTopRight = 18, CornerRadiusBottomLeft = 18, CornerRadiusBottomRight = 18, ShadowColor = new Color(.16f, .13f, .2f, .18f), ShadowSize = 8, ShadowOffset = new Vector2(0, 4), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
	void Text(string text, Vector2 p, int size, bool center = false) { if (center) p.X -= font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X / 2; DrawString(font, p, text, HorizontalAlignment.Left, -1, size, ink); }
	void Panel(Rect2 r, string fill = "f8edcf") { DrawStyleBox(Paper(fill), r); }
	public override void _Draw()
	{
		Panel(new Rect2(32, 26, 358, 80)); Text("ARQUIPÉLAGO DO CÉU", new Vector2(52, 55), 17); Text(Arena.Letters ? "Pescar letras" : "Pescar palavras", new Vector2(52, 87), 27);
		Panel(new Rect2(40, 600, 1200, 100)); Text(Arena.Drawing ? "Desenhe a letra luminosa" : Arena.Letters ? "Pesque as letras, na ordem" : "Pesque as sílabas, na ordem", new Vector2(66, 632), 22);
		int count = Arena.Target.Count; float step = Mathf.Min(98, 500f / Mathf.Max(1, count)); float start = 700 - (count - 1) * step / 2;
		for (int i = 0; i < count; i++) { bool done = i < Arena.Chosen.Count; var sp = new Vector2(start + i * step, 638); Panel(new Rect2(sp - new Vector2(step * .44f, 28), new Vector2(step * .88f, 56)), done ? "e8e6b9" : "efe0bd"); Text(done ? Arena.Pieces[Arena.Chosen[i]].ToUpperInvariant() : "·", sp + new Vector2(0, 9), 25, true); if (done) Text("✓", sp + new Vector2(step * .28f, -15), 13, true); }
		Text("Alvo: " + Arena.TargetWord.ToUpperInvariant(), new Vector2(66, 686), 13);
		if (Arena.HudResult.Length > 0 && !Arena.Drawing) { Panel(new Rect2(42, 120, 365, 82)); Text(Arena.HudResult.StartsWith("Palavra") ? "Palavra formada!" : Arena.HudResult, new Vector2(224, 153), 23, true); Text(Arena.HudResult.StartsWith("Palavra") ? "Muito bem!" : "Desfaça ou complete e confira.", new Vector2(224, 183), 16, true); }
		if (Fishing) { var pts = new Vector2[30]; for (int i = 0; i < pts.Length; i++) { float t = i / (float)(pts.Length - 1); pts[i] = HandPoint.Lerp(LineEnd, t) + new Vector2(Mathf.Sin(t * Mathf.Pi) * 25, -Mathf.Sin(t * Mathf.Pi) * 20); } DrawPolyline(pts, new Color(cyan, .24f), 7, true); DrawPolyline(pts, cyan, 2, true); DrawArc(LineEnd, 48, 0, Mathf.Tau, 48, cyan, 2, true); }
		var b = new Vector2(1166, 620); for (int side = -1; side <= 1; side += 2) { DrawEllipse(b + new Vector2(side * 15, -5), new Vector2(15, 19), new Color("b49bd4")); DrawEllipse(b + new Vector2(side * 12, 12), new Vector2(10, 12), new Color("c59750")); }
		DrawLine(b - new Vector2(0, 10), b + new Vector2(0, 19), ink, 3, true);
	}
	void DrawEllipse(Vector2 p, Vector2 r, Color c) { var pts = new Vector2[40]; for (int i = 0; i < 40; i++) { float a = i * Mathf.Tau / 40; pts[i] = p + new Vector2(Mathf.Cos(a) * r.X, Mathf.Sin(a) * r.Y); } DrawColoredPolygon(pts, c); }
}
