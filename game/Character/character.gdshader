shader_type spatial;

// A textura do Personagem é uma só, pintada: ilhas de pele (pêssego) e de roupa (cinza) sobre fundo
// preto, com olhos e sobrancelhas escuros. Cada ilha é reconhecida pela saturação e pelo brilho, e
// recolorida pela razão entre a cor escolhida e a cor original, o que mantém o sombreado pintado.
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform vec3 skin_base : source_color = vec3(0.964, 0.775, 0.643);
uniform vec3 cloth_base : source_color = vec3(0.715, 0.683, 0.649);
uniform vec3 skin_color : source_color = vec3(0.964, 0.775, 0.643);
uniform vec3 cloth_color : source_color = vec3(0.715, 0.683, 0.649);

void fragment() {
	vec3 a = texture(albedo_tex, UV).rgb;

	// A textura chega em espaço linear; os limiares foram medidos em sRGB, então a classificação volta para sRGB.
	vec3 s = pow(a, vec3(1.0 / 2.2));
	float mx = max(s.r, max(s.g, s.b));
	float mn = min(s.r, min(s.g, s.b));
	float sat = (mx - mn) / max(mx, 0.0001);

	// Um único corte de saturação separa pele (colorida) de roupa (quase cinza), para as duas máscaras
	// nunca se sobreporem; olhos e sobrancelhas escuros ficam de fora pelo brilho, o branco dos olhos pelo teto.
	float is_skin = smoothstep(0.07, 0.17, sat);
	float lit = smoothstep(0.30, 0.42, mx);
	float skin = is_skin * lit;
	float cloth = (1.0 - is_skin) * lit * (1.0 - smoothstep(0.84, 0.92, mx));

	vec3 as_skin = a * (skin_color / skin_base);
	vec3 as_cloth = a * (cloth_color / cloth_base);
	ALBEDO = mix(mix(a, as_skin, skin), as_cloth, cloth);
	ROUGHNESS = 0.85;
}
