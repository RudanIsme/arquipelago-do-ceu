shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never;
uniform float phase=0.0;
uniform float offset=0.0;
void fragment(){
 vec2 p=(UV-.5)*2.0;float r=dot(p,p);
 float halo=exp(-r*4.5)*(1.0-smoothstep(.65,1.0,r));
 float core=exp(-r*65.0);
 float pulse=.75+.25*sin(phase*2.0+offset);
 ALBEDO=(vec3(.04,.70,1.0)*halo*1.7+vec3(.75,1.0,1.0)*core*4.0)*pulse;
 ALPHA=halo;
}
