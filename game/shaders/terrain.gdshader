// Procedural 2D terrain for the Strategic Map. The relief is fictional game shading, not real elevation data.
// mode: 0 = satellite, 1 = topographic, 2 = tactical. is_land: 1 for land polygons, 0 for the sea plane.
// Kept cheap for integrated GPUs: two texture lookups per land pixel, one per sea pixel, hillshade from derivatives.
shader_type canvas_item;

uniform int mode = 0;
uniform float is_land = 1.0;
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;
varying vec2 world;
varying float lat;

void vertex() {
	world = VERTEX;
	lat = degrees(2.0 * atan(exp(radians(-VERTEX.y / 20.0))) - PI / 2.0); // Mercator y (20 px/deg) -> latitude, per vertex
}

void fragment() {
	vec3 c;
	if (mode == 2) {
		c = is_land > 0.5 ? vec3(0.10, 0.125, 0.15) : vec3(0.018, 0.030, 0.050);
	} else if (is_land < 0.5) {
		float n = texture(noise_tex, world / 900.0).r;
		c = mode == 0 ? mix(vec3(0.012, 0.045, 0.100), vec3(0.028, 0.085, 0.160), n) : mix(vec3(0.60, 0.74, 0.84), vec3(0.72, 0.84, 0.90), n);
	} else {
		float e = clamp((texture(noise_tex, world / 1500.0).r - 0.35) * 1.8 + (texture(noise_tex, world / 260.0).r - 0.5) * 0.35, 0.0, 1.0);
		// hillshade from screen-space derivatives, normalised to world units so it looks the same at every zoom
		float px = max(length(dFdx(world)), 1e-3);
		float shade = clamp(1.0 - (dFdx(e) + dFdy(e)) / px * 24.0, 0.65, 1.3);
		if (mode == 0) {
			float warm = clamp((32.0 - abs(lat)) / 16.0, 0.0, 1.0);
			float cold = clamp((abs(lat) - 55.0) / 15.0, 0.0, 1.0);
			vec3 low = mix(vec3(0.24, 0.36, 0.20), vec3(0.62, 0.55, 0.38), warm * 0.8);
			vec3 mid = vec3(0.42, 0.40, 0.28), high = vec3(0.55, 0.50, 0.45), snow = vec3(0.90, 0.91, 0.93);
			c = e < 0.4 ? mix(low, mid, e / 0.4) : e < 0.8 ? mix(mid, high, (e - 0.4) / 0.4) : mix(high, snow, (e - 0.8) / 0.2);
			c = mix(c, snow, cold * 0.7) * shade;
		} else {
			vec3 b0 = vec3(0.64, 0.77, 0.52), b1 = vec3(0.80, 0.82, 0.59), b2 = vec3(0.87, 0.77, 0.55), b3 = vec3(0.77, 0.59, 0.41), b4 = vec3(0.93, 0.91, 0.89);
			c = e < 0.2 ? b0 : e < 0.4 ? b1 : e < 0.6 ? b2 : e < 0.8 ? b3 : b4;
			if (fract(e * 10.0) < 0.06) c *= 0.7; // contour line
			c *= mix(1.0, shade, 0.5);
		}
	}
	COLOR = vec4(c, 1.0);
}
