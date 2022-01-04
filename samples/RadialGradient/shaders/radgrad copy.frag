#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 inUv;
layout (location = 0) out vec4 outFragColor;

const vec3 cp[2] = vec3[2](
	vec3(300,300,294),
	vec3(200,400,300)
);
const vec3 colors[] = vec3[](
	vec3(1,0,0),
	vec3(0,1,0),
	vec3(0,0,0),
	vec3(0,0,0)
);
const float stops[] = float[] (0.0,0.5,1.0);
const int count = 3;

layout(push_constant) uniform PushConsts {
    vec2 resolution;
} pc;

void main()
{
	float r0 = cp[0].z / pc.resolution.x;
	float r1 = cp[1].z / pc.resolution.x;
	vec2 c0 = cp[0].xy / pc.resolution;
	vec2 c1 = cp[1].xy / pc.resolution;
	vec2 p = gl_FragCoord.xy / pc.resolution;

	/*float a = (p.y - c0.y) / (p.x - c0.x);
	float b = p.y - a * p.x;
	float x1 = (-(a * b) + sqrt (a*a*b*b-(b*b-1)*(a+1)))/(1+a);
	float y1 = a * x1 + b;
	vec2 u = normalize (p - c0);
	vec2 p2 = c0 + u * r0;*/

	float dr = r1 - r0;
	float dist = length(p - c0);
	vec3 color = mix (colors[0], colors[1], smoothstep(r0+(stops[0])*(r1-r0),r0 + (stops[1])*(r1-r0), dist));
	for (int i=2; i < count; i++ )
		color = mix(color, colors[i], smoothstep(r0+(stops[i-1])*(r1-r0),r0+(stops[i])*(r1-r0), dist));
	outFragColor = vec4(color,1.0);
}