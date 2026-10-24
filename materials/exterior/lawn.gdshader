shader_type spatial;

// Large flat lawn. A single grass texture repeats visibly over tens of metres, so it is sampled at two scales
// and rotations that blend in large noise patches, and other patches are tinted dry and yellow.
// The ground is horizontal, so it is textured by its world XZ position.

uniform sampler2D grass_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_roughness : hint_roughness_r, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_occlusion : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
/// Smooth noise that decides where the two samples blend and where the grass is dry.
uniform sampler2D variation : filter_linear_mipmap, repeat_enable;
uniform float texture_size = 2.2;
uniform float variation_size = 35.0;
uniform vec3 dry_tint : source_color = vec3(0.95, 0.82, 0.5);
uniform float dry_amount : hint_range(0.0, 1.0) = 0.55;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 coords = vec2(world_position.x, -world_position.z);
	vec2 uv_near = coords / texture_size;
	// The second sample is rotated a quarter turn, so its normal map is rotated back to match.
	vec2 uv_far = vec2(-coords.y, coords.x) / (texture_size * 1.7) + 0.37;

	float blend = smoothstep(0.35, 0.65, texture(variation, coords / variation_size).r);
	float dry = smoothstep(0.5, 0.8, texture(variation, coords / (variation_size * 0.45) + 0.5).r) * dry_amount;

	vec3 albedo = mix(texture(grass_albedo, uv_near).rgb, texture(grass_albedo, uv_far).rgb, blend);
	vec3 normal_far = texture(grass_normal, uv_far).rgb;
	vec3 normal_map = mix(texture(grass_normal, uv_near).rgb, vec3(1.0 - normal_far.g, normal_far.r, normal_far.b), blend);

	ALBEDO = mix(albedo, albedo * dry_tint * 1.25, dry);
	// Blades of grass scatter light; the texture's own roughness alone reads as a wet sheen under the moon.
	ROUGHNESS = mix(0.85, 1.0, mix(texture(grass_roughness, uv_near).r, texture(grass_roughness, uv_far).r, blend));
	SPECULAR = 0.3;
	AO = mix(texture(grass_occlusion, uv_near).r, texture(grass_occlusion, uv_far).r, blend);
	NORMAL_MAP = normal_map;
	TANGENT = normalize((VIEW_MATRIX * vec4(1.0, 0.0, 0.0, 0.0)).xyz);
	BINORMAL = normalize((VIEW_MATRIX * vec4(0.0, 0.0, -1.0, 0.0)).xyz);
}
