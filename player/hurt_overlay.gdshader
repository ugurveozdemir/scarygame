shader_type canvas_item;

// Reddens the edges of the screen the more hurt the player is, and flashes red with every hit.

uniform float hurt : hint_range(0.0, 1.0) = 0.0;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(0.3, 0.0, 0.0);

void fragment() {
	float edge = distance(UV, vec2(0.5));
	// A badly hurt player's vignette throbs faintly, like a pulse.
	float pulse = (0.5 + 0.5 * sin(TIME * 6.0)) * 0.03 * hurt;
	float vignette = smoothstep(mix(0.8, 0.35, hurt) - pulse, 0.9, edge) * hurt;
	float hit = flash * (0.15 + edge * 0.75);
	COLOR = vec4(tint, clamp(vignette + hit, 0.0, 1.0));
}
