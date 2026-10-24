shader_type spatial;

// Still, murky water in the fountain basins. Two scrolling ripple normal maps move across each other,
// and the low roughness lets screen-space reflections show the surroundings.

uniform vec3 water_color : source_color = vec3(0.04, 0.06, 0.05);
uniform sampler2D ripples_a : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D ripples_b : hint_normal, filter_linear_mipmap, repeat_enable;
uniform float ripple_size = 1.2;
uniform vec2 flow_a = vec2(0.03, 0.02);
uniform vec2 flow_b = vec2(-0.02, 0.035);

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 coords = vec2(world_position.x, -world_position.z) / ripple_size;
	vec3 a = texture(ripples_a, coords + TIME * flow_a).rgb * 2.0 - 1.0;
	vec3 b = texture(ripples_b, coords * 1.7 + TIME * flow_b).rgb * 2.0 - 1.0;
	NORMAL_MAP = normalize(vec3(a.xy + b.xy, 1.0)) * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 0.5;
	ALBEDO = water_color;
	ROUGHNESS = 0.04;
	TANGENT = normalize((VIEW_MATRIX * vec4(1.0, 0.0, 0.0, 0.0)).xyz);
	BINORMAL = normalize((VIEW_MATRIX * vec4(0.0, 0.0, -1.0, 0.0)).xyz);
}
