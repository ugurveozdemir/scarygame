shader_type canvas_item;

// Closes in from the edges of the screen and tints the view red while the player is being choked.

uniform float amount : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(0.25, 0.0, 0.0);

void fragment() {
	// A faint heartbeat-like pulse makes the dark ring breathe.
	float pulse = (0.5 + 0.5 * sin(TIME * 7.0)) * 0.04 * amount;
	float radius = mix(0.95, 0.2, amount) - pulse;
	float edge = smoothstep(radius - 0.35, radius, distance(UV, vec2(0.5)));
	float alpha = amount > 0.0 ? clamp(edge + amount * 0.35, 0.0, 1.0) : 0.0;
	COLOR = vec4(tint * (1.0 - edge), alpha);
}
