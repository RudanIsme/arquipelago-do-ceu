using Godot;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using System;
using System.Linq;
using System.Collections.Generic;
namespace ArquipelagoDoCeu;

public partial class Main
{
	public NamePanel CreatePullQaPanel(NameBattle b) { var p = new NamePanel(b, rules.Combat.Tracing, voice); uiRoot.AddChild(p); return p; }
	void InstallPullTest() { var gate = new PullTestGate { Host = this, Preferences = () => (settings.Current.ReduceEffects, settings.Current.ReduceMotion) }; GetTree().Root.AddChild(gate); }
}
public partial class PullTestGate : Node
{
	public Node3D Host = null!; public Func<(bool effects, bool motion)> Preferences = null!;
	NamePanel? sourcePanel;
	PullTestArena? arena; CanvasLayer launch = null!; Camera3D? camera; bool paused, visible; double speed; Input.MouseModeEnum mouse;
	Dictionary<Node, ProcessModeEnum> modes = new(); Dictionary<CanvasLayer, bool> layers = new();
	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; launch = new CanvasLayer { Layer = 90 }; AddChild(launch); var b = new Button { Text = "Pescar com puxada · F9", Position = new Vector2((GetViewport().GetVisibleRect().Size.X - 230) / 2, 12), CustomMinimumSize = new Vector2(230, 46) }; launch.AddChild(b); b.Pressed += Enter;
		GetViewport().SizeChanged += () => b.Position = new Vector2((GetViewport().GetVisibleRect().Size.X - 230) / 2, 12);
		if (OS.GetCmdlineUserArgs().Contains("--pull-test")) CallDeferred(nameof(Enter));
		if (OS.GetCmdlineUserArgs().Contains("--attack-diagnostic")) CallDeferred(nameof(AttackDiagnostic));
		if (OS.GetCmdlineUserArgs().Contains("--letters-demo")) CallDeferred(nameof(LettersDemo));
		if (OS.GetCmdlineUserArgs().Contains("--idle-proof")) CallDeferred(nameof(IdleProof));
		if (OS.GetCmdlineUserArgs().Contains("--pull-qa")) CallDeferred(nameof(RunQa));
	}
	IEnumerable<Node> Walk(Node n) { yield return n; foreach (var c in n.GetChildren()) foreach (var x in Walk(c)) yield return x; }
	public static NameBattle PracticeBattle() { var m = MissionSource.Load().Missions.Where(m => m.Mechanic == "formar_palavras").OrderByDescending(m => m.Params["syllables"].GetArrayLength()).First(); return NameBattle.Start(new[] { m }, new Pcg32(41)).Value!; }
	public void EnterWithPanel(NamePanel panel) { if (arena != null) return; sourcePanel = panel; Enter(); }
	// A pesca de teste não vale dentro de uma Dungeon: ela pausa e esconde o jogo todo.
	public bool Blocked { get => blocked; set { blocked = value; if (IsInstanceValid(launch)) launch.Visible = !value && arena == null; } }
	bool blocked;
	public void Enter()
	{
		if (arena != null || blocked) return; var battle = sourcePanel?.PullBattle ?? PracticeBattle(); camera = GetViewport().GetCamera3D(); paused = GetTree().Paused; speed = Engine.TimeScale; mouse = Input.MouseMode; visible = Host.Visible;
		modes = Walk(Host).ToDictionary(n => n, n => n.ProcessMode); layers = Walk(Host).OfType<CanvasLayer>().ToDictionary(n => n, n => n.Visible);
		foreach (var n in modes.Keys) n.ProcessMode = ProcessModeEnum.Disabled; foreach (var n in layers.Keys) n.Visible = false; Host.Visible = false; GetTree().Paused = true; Engine.TimeScale = 1; Input.MouseMode = Input.MouseModeEnum.Visible; launch.Hide();
		var pref = Preferences(); arena = new PullTestArena { ProcessMode = ProcessModeEnum.Always, ReduceEffects = pref.effects, ReduceMotion = pref.motion, ExitRequested = Exit, Battle = battle, Linked = sourcePanel != null, Letters = sourcePanel == null, CheckRequested = () => ExitAndCheck() }; GetTree().Root.AddChild(arena); GD.Print("PULL_TEST_ENTER");
	}
	void ExitAndCheck() { var p = sourcePanel; Exit(); if (IsInstanceValid(p)) p!.ReturnFromPull(true); }
	public void Exit()
	{
		if (arena == null) return; arena.QueueFree(); arena = null; foreach (var x in modes) if (IsInstanceValid(x.Key)) x.Key.ProcessMode = x.Value; foreach (var x in layers) if (IsInstanceValid(x.Key)) x.Key.Visible = x.Value;
		Host.Visible = visible; GetTree().Paused = paused; Engine.TimeScale = speed; Input.MouseMode = mouse; if (IsInstanceValid(camera)) camera!.MakeCurrent(); launch.Show(); var p = sourcePanel; sourcePanel = null; if (IsInstanceValid(p)) p!.ReturnFromPull(false); GD.Print("PULL_TEST_EXIT_RESTORED");
	}
	public override void _Input(InputEvent e) { if (e is not InputEventKey { Pressed: true, Echo: false } k) return; if (k.PhysicalKeycode == Key.F9) { if (arena == null) Enter(); else Exit(); GetViewport().SetInputAsHandled(); } else if (arena != null) { if (k.PhysicalKeycode == Key.Escape) { if (arena.Drawing) arena.CancelDrawing(); else Exit(); } else arena.HandleKey(k.PhysicalKeycode); GetViewport().SetInputAsHandled(); } }
	async System.Threading.Tasks.Task Delay(double t) => await ToSignal(GetTree().CreateTimer(t, true), SceneTreeTimer.SignalName.Timeout);
	void Check(bool ok, string what) { if (!ok) throw new Exception("PULL_QA_FAILED " + what); GD.Print("PULL_QA_OK " + what); }
	async System.Threading.Tasks.Task Shot(string name) { await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); var dir = OS.GetCmdlineUserArgs().First(x => x.StartsWith("--pull-out="))[11..]; GetViewport().GetTexture().GetImage().SavePng(dir + "/" + name + ".png"); }
	async System.Threading.Tasks.Task ClickTile(int i) { var pos = arena!.TileCenter(i); Input.ParseInputEvent(new InputEventMouseMotion { Position = pos, GlobalPosition = pos }); Input.ParseInputEvent(new InputEventMouseButton { Position = pos, GlobalPosition = pos, ButtonIndex = MouseButton.Left, Pressed = true }); await Delay(.04); Input.ParseInputEvent(new InputEventMouseButton { Position = pos, GlobalPosition = pos, ButtonIndex = MouseButton.Left, Pressed = false }); await Delay(.05); }
	int NextCorrect() { var a = arena!; return Enumerable.Range(0, a.Pieces.Count).First(i => !a.Chosen.Contains(i) && a.Pieces[i] == a.Target[a.Chosen.Count]); }
	public async void IdleProof() { await Delay(.3); Enter(); await Delay(.4); await Shot("idle-sword-a"); await Delay(.65); await Shot("idle-sword-b"); arena!.Capture(); await Delay(1.5); await Shot("sword-pull"); await Delay(2); await Shot("idle-after-pull"); GetTree().Quit(); }

	async System.Threading.Tasks.Task WaitIdle() { for (int n = 0; arena!.Playing && n < 1500; n++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); Check(!arena!.Playing, "return_to_idle"); await Delay(.3); }
	async System.Threading.Tasks.Task Pointer(Vector2 viewport, bool down) { var p = GetViewport().GetFinalTransform() * viewport; Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = down }); await Delay(.025); }
	async System.Threading.Tasks.Task DrawLetter(bool slow = false) { foreach (var stroke in arena!.Trace.GetGuidePointsScreen()) { await Pointer(stroke[0], true); foreach (var point in stroke) { var p = GetViewport().GetFinalTransform() * (point + new Vector2(8, 0)); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left }); await Delay(slow ? .025 : .001); } await Pointer(stroke[^1] + new Vector2(8, 0), false); await Delay(slow ? .35 : .02); } }
	public async void RunQa()
	{
		try
		{
			await Delay(.6); Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F9, Pressed = true }); await Delay(.5); Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F9, Pressed = false }); Check(arena != null && GetTree().Paused, "f9_enters_and_pauses_game"); await Shot("letters-idle");
			for (int i = 0; i < 3; i++) { int pick = NextCorrect(); await ClickTile(pick); Check(arena!.Playing && arena.Variant == i, "mouse_starts_action_" + i); Check(!arena.Capture() && !arena.Pick(pick) && !arena.Undo(), "rapid_input_locked"); await ClickTile((pick + 1) % arena.Pieces.Count); Check(arena.History.Count == i + 1, "rapid_mouse_no_queue"); await Delay(1.5); await Shot("letters-action-" + i); await WaitIdle(); Check(arena.CaptureCount == i + 1 && arena.Chosen.Count == i + 1, "one_capture_" + i); Check(!arena.Pick(pick), "duplicate_rejected"); await Shot("letters-idle-" + i); }
			Check(arena!.History.SequenceEqual(new[] { 0, 1, 2 }), "three_accepted_actions_cycle"); var confirm = Walk(arena).OfType<Button>().First(b => b.Text.StartsWith("Conferir")); var center = confirm.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = center, GlobalPosition = center }); Input.ParseInputEvent(new InputEventMouseButton { Position = center, GlobalPosition = center, ButtonIndex = MouseButton.Left, Pressed = true }); await Delay(.04); Input.ParseInputEvent(new InputEventMouseButton { Position = center, GlobalPosition = center, ButtonIndex = MouseButton.Left, Pressed = false }); await Delay(.1); Check(arena.Drawing, "conferir_mouse_enters_drawing"); var point = arena.Trace.GetGuidePointsScreen()[0][0]; await Pointer(point, true); await Pointer(point, false); Check(!arena.Trace.Completed && arena.Trace.Progress == 0, "click_is_not_drawing");
			await Pointer(point, true); var drag = GetViewport().GetFinalTransform() * (point + new Vector2(0, -20)); Input.ParseInputEvent(new InputEventMouseMotion { Position = drag, GlobalPosition = drag, ButtonMask = MouseButtonMask.Left }); await Delay(.05); Check(arena.Trace.Progress > 0, "drawing_drag_active");
			_Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true }); await Pointer(point, false); Check(!arena.Drawing && arena.Chosen.Count == 3, "cancel_active_drawing_keeps_letters"); arena.Check(); Check(arena.Trace.Progress == 0, "drawing_reentry_clears_partial_stroke"); await DrawLetter(); Check(arena.Trace.Completed, "real_drag_tolerates_offset"); await Shot("letters-drawing-complete");
			Exit(); Check(!GetTree().Paused && Host.Visible == visible, "exit_restores_pause_and_host"); Check(modes.All(x => x.Key.ProcessMode == x.Value) && layers.All(x => x.Key.Visible == x.Value) && GetViewport().GetCamera3D() == camera && Engine.TimeScale == speed, "restore_camera_ui_process");
			Enter(); await Delay(.2); await ClickTile(NextCorrect()); await Delay(.25); Exit(); Enter(); Check(arena!.Chosen.Count == 0 && arena.CaptureCount == 0, "cancel_pull_and_clean_reentry"); await Delay(.2); arena.ToggleMode(); Check(!arena.Letters, "syllable_mode_preserved"); Exit();
			Enter(); await Delay(.3); await ClickTile(NextCorrect()); await Delay(3.1); Check(arena!.MotionState.StartsWith("Golpe"), "approved_attack_phase_reached"); Check(!arena.Capture() && !arena.Undo(), "attack_input_stays_locked"); _Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true }); Check(arena == null && !GetTree().Paused, "escape_during_attack_restores_game"); Enter(); Check(arena!.Chosen.Count == 0, "reentry_after_attack_clean"); Exit();
			var battle = PracticeBattle(); var panel = ((Main)Host).CreatePullQaPanel(battle); EnterWithPanel(panel); await Delay(.3); Check(ReferenceEquals(arena!.Battle, battle) && !arena.Letters, "linked_original_battle"); arena.ReduceEffects = true; arena.ReduceMotion = true; await ClickTile(NextCorrect()); await WaitIdle(); Exit(); Check(battle.Chosen.Count == 1, "linked_capture_preserved"); panel.QueueFree();
			Enter(); DisplayServer.WindowSetSize(new Vector2I(1920, 1080)); await Delay(.5); for (int i = 0; i < 3; i++) { await ClickTile(NextCorrect()); await WaitIdle(); }
			arena!.Check(); await DrawLetter(); Check(arena.Trace.Completed, "mouse_drawing_1920"); await Shot("letters-1920"); Exit(); GD.Print("LETTERS_QA_ALL_PASS"); GetTree().Quit();
		}
		catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
	}
	public async void AttackDiagnostic() { try { await Delay(.8); Enter(); await Delay(1.7); await Shot("diagnostic-idle"); await ClickTile(NextCorrect()); await Delay(1.25); await Shot("diagnostic-capture"); await Delay(1.75); await Shot("diagnostic-attack-start"); await Delay(.4); await Shot("diagnostic-attack-impact"); await WaitIdle(); await Delay(1.5); await Shot("diagnostic-return"); GetTree().Quit(); } catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); } }
	public async void LettersDemo() { try { await Delay(.8); Enter(); await Delay(2.2); for (int i = 0; i < 3; i++) { await ClickTile(NextCorrect()); await WaitIdle(); await Delay(1.25); } arena!.Check(); await Delay(1); await DrawLetter(true); await Delay(5); Check(arena.Trace.Completed, "demo_draw_completed"); GD.Print("LETTERS_DEMO_COMPLETE"); GetTree().Quit(); } catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); } }
}
