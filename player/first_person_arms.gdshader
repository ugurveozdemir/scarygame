// The first-person arms, drawn like Unreal Engine's first-person primitives: their depth is squeezed into the sliver
// right in front of the camera, so walls and doors never cut through them. Lighting still uses their real position.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic;
uniform float roughness : hint_range(0.0, 1.0) = 0.6;
uniform float subsurface_scattering : hint_range(0.0, 1.0) = 0.0;
// The share of the depth range the arms keep. Smaller pulls them closer to the camera.
uniform float depth_scale : hint_range(0.001, 1.0) = 0.05;

void vertex() {
	POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	// Depth is reversed: 1 is the near plane, so moving it towards 1 brings the arms in front of everything else.
	POSITION.z = mix(POSITION.w, POSITION.z, depth_scale);
}

void fragment() {
	ALBEDO = texture(albedo_texture, UV).rgb;
	ROUGHNESS = roughness;
	SSS_STRENGTH = subsurface_scattering;
}
