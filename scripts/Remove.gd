extends Node3D


func _on_animation_finished(animation):
	if animation == "Hide":
		self.queue_free()
