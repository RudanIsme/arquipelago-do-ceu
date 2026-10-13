shader_type spatial;
render_mode cull_disabled;

// Cada tile traz no COLOR: r = tipo (0 grama, 1 flores, 2 caminho, 3 água), g = vizinhos que
// também são caminho (6 bits), b = variação sorteada do tile, a = 1 no topo e 0 na lateral.
uniform vec3 grass_a : source_color = vec3(0.37, 0.72, 0.23);
uniform vec3 grass_b : source_color = vec3(0.29, 0.62, 0.20);
uniform vec3 dirt_a : source_color = vec3(0.79, 0.54, 0.28);
uniform vec3 dirt_b : source_color = vec3(0.66, 0.44, 0.23);
uniform vec3 water_a : source_color = vec3(0.20, 0.65, 0.91);
uniform vec3 water_b : source_color = vec3(0.42, 0.77, 0.96);
uniform vec3 side_top : source_color = vec3(0.25, 0.55, 0.18);
uniform vec3 side_bottom : source_color = vec3(0.60, 0.42, 0.25);

varying vec4 info;
varying vec2 local;
varying vec3 wpos;

void vertex() {
	info = COLOR;
	local = UV;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Distância de p ao segmento a-b.
float seg(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

void fragment() {
	ROUGHNESS = 0.9;
	if (info.a < 0.5) {
		// Lateral do tile: faixa de grama no alto, terra embaixo.
		float d = clamp(local.y, 0.0, 1.0);
		ALBEDO = mix(side_top, side_bottom, smoothstep(0.05, 0.35, d)) * (0.9 + 0.2 * vnoise(wpos.xz * 6.0 + wpos.y * 4.0));
	} else {
		float tipo = floor(info.r * 3.0 + 0.5);
		int mask = int(floor(info.g * 63.0 + 0.5));
		vec2 p = local;

		float n = vnoise(wpos.xz * 3.0) * 0.6 + vnoise(wpos.xz * 9.0) * 0.4;
		vec3 col = mix(grass_a, grass_b, n);
		col *= 0.88 + 0.24 * vnoise(wpos.xz * 24.0);

		if (tipo > 0.5 && tipo < 1.5) {
			// Flores: pontinhos coloridos espalhados em células.
			vec2 g = p * 3.2;
			vec2 id = floor(g);
			vec2 f = fract(g) - 0.5;
			float h = hash(id + info.b * 31.0);
			vec2 off = (vec2(hash(id + 1.3), hash(id + 2.7)) - 0.5) * 0.45;
			float d = length(f - off);
			float on = step(0.45, h) * smoothstep(0.17, 0.11, d);
			float pick = hash(id + 9.1);
			vec3 fc = pick < 0.25 ? vec3(0.91, 0.27, 0.24) : (pick < 0.5 ? vec3(1.0, 0.78, 0.24) : (pick < 0.75 ? vec3(0.31, 0.72, 0.94) : vec3(1.0, 1.0, 1.0)));
			col = mix(col, fc, on);
		} else if (tipo > 1.5 && tipo < 2.5) {
			// Caminho: um miolo e um braço até a aresta de cada vizinho que também é caminho.
			float width = 0.27 + 0.05 * vnoise(wpos.xz * 5.0);
			float dist = length(p) - 0.12;
			vec2 dirs[6] = vec2[](vec2(0.866, 0.5), vec2(0.866, -0.5), vec2(0.0, -1.0), vec2(-0.866, -0.5), vec2(-0.866, 0.5), vec2(0.0, 1.0));
			for (int i = 0; i < 6; i++) {
				if ((mask & (1 << i)) != 0) {
					dist = min(dist, seg(p, vec2(0.0), dirs[i] * 1.05));
				}
			}
			float path = smoothstep(width + 0.04, width - 0.04, dist);
			vec3 dirt = mix(dirt_a, dirt_b, vnoise(wpos.xz * 10.0));
			dirt *= 0.9 + 0.2 * vnoise(wpos.xz * 30.0);
			col = mix(col, dirt, path);
		} else if (tipo > 2.5) {
			float w = vnoise(wpos.xz * 3.0 + vec2(TIME * 0.35, -TIME * 0.2));
			float w2 = vnoise(wpos.xz * 8.0 - vec2(TIME * 0.5, TIME * 0.3));
			col = mix(water_a, water_b, smoothstep(0.35, 0.75, w * 0.7 + w2 * 0.3));
			ROUGHNESS = 0.25;
		}

		// Borda do hexágono levemente mais escura, para os tiles se lerem como peças.
		float hd = max(max(abs(dot(p, vec2(0.866, 0.5))), abs(p.y)), abs(dot(p, vec2(-0.866, 0.5)))) / 0.866;
		col *= mix(1.0, 0.82, smoothstep(0.88, 1.0, hd));
		ALBEDO = col;
	}
}
