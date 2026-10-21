shader_type canvas_item;

// Full-screen post-process: film grain, vignette, scanlines, chromatic aberration
// and the camcorder's green night-vision mode.

uniform sampler2D screen_texture : hint_screen_texture, filter_linear;
uniform float grain_amount : hint_range(0.0, 0.5) = 0.03;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.3;
uniform float scanline_strength : hint_range(0.0, 1.0) = 0.0;
uniform float scanline_count = 240.0;
uniform float chromatic_aberration : hint_range(0.0, 0.01) = 0.0;
uniform bool night_vision = false;
uniform vec3 night_vision_tint : source_color = vec3(0.4, 1.0, 0.5);
uniform float night_vision_gain = 2.0;

float random(vec2 uv) {
	return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 aberration = vec2(chromatic_aberration, 0.0);
	vec3 color = vec3(
		texture(screen_texture, uv + aberration).r,
		texture(screen_texture, uv).g,
		texture(screen_texture, uv - aberration).b);

	if (night_vision) {
		float luminance = dot(color, vec3(0.299, 0.587, 0.114));
		color = night_vision_tint * clamp(luminance * night_vision_gain, 0.0, 1.0);
	}

	color += (random(uv + fract(TIME)) - 0.5) * grain_amount;
	color *= 1.0 - scanline_strength * (0.5 + 0.5 * sin(uv.y * scanline_count * TAU));
	color *= 1.0 - smoothstep(0.3, 0.85, distance(uv, vec2(0.5))) * vignette_strength;

	COLOR = vec4(color, 1.0);
}
