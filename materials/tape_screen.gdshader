shader_type spatial;
render_mode unshaded;

// An old monitor showing a tape: soft scanlines, a little wobble, grain, and snow between recordings.

uniform sampler2D picture : source_color, filter_linear;
uniform float snow = 1.0;
uniform float brightness = 1.2;

float random(vec2 uv) {
	return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 uv = UV;
	uv.x += sin(uv.y * 30.0 + TIME * 4.0) * 0.0015;
	vec3 color = texture(picture, uv).rgb;
	color += (random(uv * 300.0 + fract(TIME)) - 0.5) * 0.1;
	color *= 0.82 + 0.18 * sin(uv.y * 520.0);
	float flakes = random(floor(uv * vec2(180.0, 135.0)) + fract(TIME * 9.0));
	color = mix(color, vec3(flakes * 0.8), snow);
	ALBEDO = color * brightness;
}
