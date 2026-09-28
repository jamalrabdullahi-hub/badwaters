// Real-Earth base for the Strategic Map. earth_tex is equirectangular (Natural Earth, public domain):
// R = shaded relief, G = ocean depth band at sea / urban area on land, B = lake.
// mode: 0 = satellite, 1 = topographic, 2 = tactical. is_land: 1 for land polygons, 0 for the sea plane.
// One texture lookup per pixel: cheap on integrated GPUs.
shader_type canvas_item;

uniform int mode = 0;
uniform float is_land = 1.0;
uniform float px_per_deg = 20.0;
uniform bool show_relief = true;
uniform bool show_depth = true;
uniform bool show_urban = false;
uniform bool show_lakes = true;
uniform sampler2D earth_tex : filter_linear_mipmap;
varying vec2 world;

void fragment() {
	float lon = world.x / px_per_deg;
	float lat = degrees(2.0 * atan(exp(radians(-world.y / px_per_deg))) - PI / 2.0); // inverse Mercator
	vec4 e = texture(earth_tex, vec2((lon + 180.0) / 360.0, (90.0 - lat) / 180.0));
	float shade = show_relief ? clamp(e.r / 0.78, 0.45, 1.25) : 1.0; // 0.78 ~ flat ground in the relief raster
	vec3 c;
	if (is_land < 0.5) {
		float d = show_depth ? e.g : 0.33;
		if (mode == 0) c = mix(vec3(0.030, 0.090, 0.150), vec3(0.006, 0.022, 0.055), d);
		else if (mode == 1) c = mix(vec3(0.74, 0.86, 0.92), vec3(0.44, 0.62, 0.78), d);
		else c = mix(vec3(0.028, 0.045, 0.070), vec3(0.012, 0.020, 0.036), d);
	} else {
		if (mode == 0) {
			float warm = clamp((32.0 - abs(lat)) / 16.0, 0.0, 1.0);
			float cold = clamp((abs(lat) - 58.0) / 14.0, 0.0, 1.0);
			c = mix(vec3(0.30, 0.37, 0.25), vec3(0.58, 0.52, 0.38), warm * 0.75);
			c = mix(c, vec3(0.86, 0.88, 0.90), cold * 0.6);
		} else if (mode == 1) c = vec3(0.86, 0.83, 0.70);
		else c = vec3(0.13, 0.155, 0.18);
		c *= mode == 2 ? mix(1.0, shade, 0.5) : shade;
		if (show_lakes && e.b > 0.5) c = mode == 1 ? vec3(0.62, 0.78, 0.88) : mode == 0 ? vec3(0.04, 0.11, 0.18) : vec3(0.03, 0.05, 0.08);
		if (show_urban && e.g > 0.5) c = mix(c, mode == 1 ? vec3(0.80, 0.55, 0.45) : vec3(0.85, 0.72, 0.40), 0.3);
	}
	COLOR = vec4(c, 1.0);
}

void vertex() {
	world = VERTEX;
}
