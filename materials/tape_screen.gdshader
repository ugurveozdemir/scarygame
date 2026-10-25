shader_type spatial;
render_mode unshaded;

// An old monitor showing a tape or a security camera: soft scanlines, a little wobble, grain, and snow
// between recordings. Security cameras film in infrared, so their picture is black and white.

uniform sampler2D picture : source_color, filter_linear;
uniform float snow = 1.0;
uniform float brightness = 1.2;
uniform bool monochrome = false;

float random(vec2 uv) {
	return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 uv = UV;
	uv.x += sin(uv.y * 30.0 + TIME * 4.0) * 0.0015;
	vec3 color = texture(picture, uv).rgb;
	if (monochrome) {
		color = vec3(dot(color, vec3(0.299, 0.587, 0.114))) * vec3(0.9, 1.0, 0.95);
	}
	color += (random(uv * 300.0 + fract(TIME)) - 0.5) * 0.06;
	color *= 0.82 + 0.18 * sin(uv.y * 520.0);
	float flakes = random(floor(uv * vec2(180.0, 135.0)) + fract(TIME * 9.0));
	color = mix(color, vec3(flakes * 0.8), snow);
	ALBEDO = color * brightness;
}
