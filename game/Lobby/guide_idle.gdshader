shader_type spatial;
render_mode cull_disabled;
uniform sampler2D albedo_tex : source_color;
uniform float phase=0.0;
uniform float motion=1.0;
uniform float look=0.0;
uniform float stretch=0.0;
vec3 rotate_z(vec3 p,float a){float c=cos(a);float s=sin(a);return vec3(c*p.x-s*p.y,s*p.x+c*p.y,p.z);}
vec3 rotate_y(vec3 p,float a){float c=cos(a);float s=sin(a);return vec3(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);}
vec3 rotate_x(vec3 p,float a){float c=cos(a);float s=sin(a);return vec3(p.x,c*p.y-s*p.z,s*p.y+c*p.z);}
void vertex(){
 vec3 p=VERTEX;
 float breath=sin(phase*1.48);
 float upper=smoothstep(-0.72,-0.15,p.y);
 float middle=1.0-smoothstep(0.30,0.46,abs(p.x));
 float chest=smoothstep(-0.50,-0.18,p.y)*(1.0-smoothstep(0.05,0.34,p.y))*middle;
 VERTEX.z+=motion*chest*breath*0.012;
 VERTEX.x+=motion*upper*middle*sin(phase*0.53)*0.006;
 float head=smoothstep(-0.18,0.27,p.y)*(1.0-smoothstep(0.36,0.48,abs(p.x)));
 float yaw=motion*head*(sin(phase*0.48)*0.024+sin(phase*0.91)*0.007+look*0.38);
 float nod=motion*head*sin(phase*0.74)*0.012;
 vec3 pivot=vec3(0.0,0.16,0.0);
 VERTEX=pivot+rotate_z(rotate_x(rotate_y(VERTEX-pivot,yaw),nod),head*stretch*0.12);
 NORMAL=rotate_z(rotate_x(rotate_y(NORMAL,yaw),nod),head*stretch*0.12);
 float right=smoothstep(0.32,0.49,p.x)*smoothstep(-0.73,-0.51,p.y);
 float left=smoothstep(0.30,0.48,-p.x)*smoothstep(-0.73,-0.49,p.y);
 float arm=motion*(right*sin(phase*0.92)*0.012-left*sin(phase*1.1+0.7)*0.015-left*stretch*0.11);
 vec3 shoulder=vec3(p.x>0.0?0.32:-0.30,0.05,0.0);
 VERTEX=shoulder+rotate_z(VERTEX-shoulder,arm);
 NORMAL=rotate_z(NORMAL,arm);
}
void fragment(){ALBEDO=texture(albedo_tex,UV).rgb;ROUGHNESS=0.9;}
