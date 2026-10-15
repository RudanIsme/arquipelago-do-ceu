shader_type spatial;
render_mode cull_disabled;
uniform sampler2D albedo_tex : source_color;
uniform sampler2D paint_tex : source_color,filter_linear_mipmap;
uniform float phase=0.0;
uniform float effect_strength=1.0;
varying vec3 pos;
void vertex(){pos=VERTEX;}
vec3 paint(vec2 p,float cycle){
 float r=length(p);
 float a=atan(p.y,p.x)-cycle*0.75;
 float wobble=sin(a*3.0+r*11.0)*0.035+sin(a*7.0-r*5.0)*0.015;
 // Sampling progressively farther out makes the painted strokes travel inward.
 float source_r=r*(0.72+cycle*0.50);
 source_r=0.50*tanh(source_r/0.50);
 vec2 source=vec2(cos(a+wobble),sin(a+wobble))*source_r;
 source.y-=0.04;
 return texture(paint_tex,vec2(source.x*.5+.5,.5-source.y*.5)).rgb;
}
void fragment(){
 vec3 original=texture(albedo_tex,UV).rgb;
 vec2 p=pos.xy-vec2(-.005,.015);
 float arch=length(vec2(p.x,max(p.y,0.0)));
 float inside=(1.0-smoothstep(.49,.565,arch))*smoothstep(-.94,-.87,pos.y);
 float t=fract(phase/7.0);
 float t2=fract(t+0.5);
 float blend=abs(t*2.0-1.0);
 vec3 energy=mix(paint(p,t),paint(p,t2),blend);
 float core=exp(-dot(p,p)*140.0);
 float pulse=.94+.08*sin(phase*1.35);
 ALBEDO=mix(original,energy*.18,inside);
 EMISSION=inside*(energy*(.55+effect_strength*.90)+vec3(.72,.92,1.0)*core*.6)*pulse;
 ROUGHNESS=.85;
}
