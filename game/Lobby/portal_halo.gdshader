shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform float strength=1.0;
void fragment(){
 vec2 p=(UV-0.5)*2.0;
 float r=length(p);
 float halo=exp(-pow((r-0.61)*5.0,2.0))*(1.0-smoothstep(0.78,1.0,r));
 ALBEDO=mix(vec3(0.68,0.38,0.82),vec3(0.28,0.86,0.98),UV.y);
 ALPHA=halo*0.23*strength;
}
