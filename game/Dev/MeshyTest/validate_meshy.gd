extends SceneTree

var results: Array = []
var failures: Array = []
var folder = "res://assets/modelos/meshy-teste-20261019/"

func _initialize():
	call_deferred("validate")

func validate():
	var catalog = JSON.parse_string(FileAccess.get_file_as_string(folder + "catalogo.json"))
	for entry in catalog:
		var clips = ["idle", "walk"] if entry.Animated else [""]
		for clip in clips:
			var file = folder + entry.Asset + ("-" + clip if clip != "" else "") + ".glb"
			var packed = load(file) as PackedScene
			if packed == null:
				failures.append("Cannot load " + file)
				continue
			var model = packed.instantiate()
			root.add_child(model)
			await process_frame
			var tris = 0
			var box: AABB
			var first = true
			for mesh in model.find_children("*", "MeshInstance3D", true, false):
				for surface in mesh.mesh.get_surface_count():
					var arrays = mesh.mesh.surface_get_arrays(surface)
					tris += arrays[Mesh.ARRAY_INDEX].size() / 3 if arrays[Mesh.ARRAY_INDEX] != null else arrays[Mesh.ARRAY_VERTEX].size() / 3
				var mesh_box = mesh.global_transform * mesh.get_aabb()
				box = mesh_box if first else box.merge(mesh_box)
				first = false
			var data = {"asset": entry.Asset, "clip": clip, "triangles": tris, "source_bounds_y_up": {"min": str(box.position), "size": str(box.size)}}
			if entry.Animated:
				var rigs = model.find_children("*", "Skeleton3D", true, false)
				var players = model.find_children("*", "AnimationPlayer", true, false)
				if rigs.size() != 1 or players.size() != 1:
					failures.append("Rig/player count " + file)
				else:
					var rig: Skeleton3D = rigs[0]
					var player: AnimationPlayer = players[0]
					var name = ""
					for candidate in player.get_animation_list():
						if candidate != "RESET": name = candidate
					var animation = player.get_animation(name)
					animation.loop_mode = Animation.LOOP_NONE
					player.play(name)
					player.pause()
					var endpoints: Array = []
					var finite = true
					for frame in 49:
						player.seek(animation.length * frame / 48.0, true)
						player.advance(0)
						await process_frame
						var poses: Array = []
						for bone in rig.get_bone_count():
							var pose = rig.get_bone_pose(bone)
							finite = finite and pose.is_finite()
							poses.append(pose)
						if frame == 0 or frame == 48: endpoints.append(poses)
					var max_delta = 0.0
					for bone in rig.get_bone_count():
						var a: Transform3D = endpoints[0][bone]
						var b: Transform3D = endpoints[1][bone]
						max_delta = max(max_delta, a.origin.distance_to(b.origin), a.basis.x.distance_to(b.basis.x), a.basis.y.distance_to(b.basis.y), a.basis.z.distance_to(b.basis.z))
					data.merge({"bones": rig.get_bone_count(), "animation": name, "duration": animation.length, "samples": 49, "finite": finite, "loop_endpoint_pose_max_delta": max_delta})
					if not finite or max_delta > 0.001: failures.append("Invalid animation " + file)
			results.append(data)
			print("MESHY_IMPORT_OK ", JSON.stringify(data))
			model.queue_free()
			await process_frame
	var report = {"engine": Engine.get_version_info().string, "assets": 20, "glbs": results.size(), "results": results, "failures": failures, "scope": "Godot reimport, mesh counts, Y-up bounds, skeleton poses across 49 samples and loop endpoints. Gameplay collision and root motion remain logical game systems; no naturalness approval inferred."}
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--report="):
			var output = FileAccess.open(arg.substr(9), FileAccess.WRITE)
			output.store_string(JSON.stringify(report, "\t"))
	print("MESHY_VALIDATION_DONE ", results.size(), " glbs; failures=", failures)
	quit(0 if failures.is_empty() else 1)
