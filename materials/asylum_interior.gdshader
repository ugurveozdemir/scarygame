shader_type spatial;

// Interior surfaces of the asylum building, which is a single CSG shape: worn floor tiles on upward faces,
// a suspended ceiling on downward faces, and walls with glazed tiles up to wainscot height, a painted trim
// and peeling plaster above. All surfaces are axis aligned, so each one is textured by projecting world
// coordinates onto its own plane, with a matching tangent frame for the normal maps.

group_uniforms floor_tiles;
uniform sampler2D floor_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D floor_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D floor_roughness : hint_roughness_r, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float floor_texture_size = 1.8;

group_uniforms ceiling;
uniform sampler2D ceiling_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D ceiling_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D ceiling_roughness : hint_roughness_r, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float ceiling_texture_size = 4.8;
/// The source textures are clean white; tinting ages them into a dirty off-white.
uniform vec3 ceiling_tint : source_color = vec3(0.68, 0.68, 0.64);

group_uniforms wall_tiles;
uniform sampler2D tiles_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tiles_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tiles_roughness : hint_roughness_r, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float tiles_texture_size = 1.5;
uniform vec3 tiles_tint : source_color = vec3(0.74, 0.76, 0.7);
uniform float wainscot_height = 1.2;

group_uniforms plaster;
uniform sampler2D plaster_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D plaster_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D plaster_roughness : hint_roughness_r, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float plaster_texture_size = 2.5;

group_uniforms trim_and_grime;
uniform vec3 trim_color : source_color = vec3(0.22, 0.27, 0.24);
uniform float trim_width = 0.05;
/// Walls get darker towards the floor, where dirt collects.
uniform float grime_height = 0.4;
uniform float grime_darkness : hint_range(0.0, 1.0) = 0.45;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
	// Tangent frame of the surface's plane: v runs down walls, and tangent x binormal = normal.
	bool is_floor = world_normal.y > 0.5;
	bool is_ceiling = world_normal.y < -0.5;
	vec3 normal_axis = is_floor ? vec3(0.0, 1.0, 0.0) : is_ceiling ? vec3(0.0, -1.0, 0.0) : normalize(vec3(world_normal.x, 0.0, world_normal.z));
	vec3 binormal = is_floor || is_ceiling ? vec3(0.0, 0.0, -normal_axis.y) : vec3(0.0, -1.0, 0.0);
	vec3 tangent = cross(binormal, normal_axis);
	vec2 coords = vec2(dot(world_position, tangent), dot(world_position, binormal));

	vec3 albedo;
	vec3 normal_map;
	float roughness;
	if (is_floor) {
		vec2 uv = coords / floor_texture_size;
		albedo = texture(floor_albedo, uv).rgb;
		normal_map = texture(floor_normal, uv).rgb;
		roughness = texture(floor_roughness, uv).r;
	} else if (is_ceiling) {
		vec2 uv = coords / ceiling_texture_size;
		albedo = texture(ceiling_albedo, uv).rgb * ceiling_tint;
		normal_map = texture(ceiling_normal, uv).rgb;
		roughness = texture(ceiling_roughness, uv).r;
	} else if (world_position.y < wainscot_height) {
		vec2 uv = coords / tiles_texture_size;
		albedo = texture(tiles_albedo, uv).rgb * tiles_tint;
		normal_map = texture(tiles_normal, uv).rgb;
		roughness = texture(tiles_roughness, uv).r;
	} else {
		vec2 uv = coords / plaster_texture_size;
		albedo = texture(plaster_albedo, uv).rgb;
		normal_map = texture(plaster_normal, uv).rgb;
		roughness = texture(plaster_roughness, uv).r;
	}

	if (!is_floor && !is_ceiling) {
		if (abs(world_position.y - wainscot_height) < trim_width * 0.5) {
			albedo = trim_color;
			normal_map = vec3(0.5, 0.5, 1.0);
			roughness = 0.5;
		}
		albedo *= mix(1.0 - grime_darkness, 1.0, smoothstep(0.0, grime_height, world_position.y));
	}

	ALBEDO = albedo;
	ROUGHNESS = roughness;
	NORMAL_MAP = normal_map;
	TANGENT = normalize((VIEW_MATRIX * vec4(tangent, 0.0)).xyz);
	BINORMAL = normalize((VIEW_MATRIX * vec4(binormal, 0.0)).xyz);
}
