shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;

// The Remnant's body: a faint glow that is strongest at its outline and flickers like a bad signal.

uniform vec3 glow_color : source_color = vec3(0.75, 0.9, 1.0);
uniform float strength = 0.6;

void fragment() {
	float rim = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.0);
	float flicker = 0.7 + 0.3 * sin(TIME * 31.0 + VERTEX.y * 12.0) * sin(TIME * 7.0);
	ALBEDO = glow_color * (0.15 + rim) * flicker * strength;
}
