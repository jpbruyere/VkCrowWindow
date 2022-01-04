#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 inUv;
layout (location = 0) out vec4 outFragColor;

const vec2 cp[2] = vec2[2](
	vec2(0,400),
	vec2(0,200)
);
const vec3 colors[] = vec3[](
	vec3(0,0,0),
	vec3(1,0,0),
	vec3(0,1,0),
	vec3(0,0,0)
);
const float stops[] = float[] (0.0,0.35,0.65,1.0);
const int count = 4;

layout(push_constant) uniform PushConsts {
    vec2 resolution;
} pc;

void main()
{
	vec2 p0 = cp[0].xy / pc.resolution;
	vec2 p1 = cp[1].xy / pc.resolution;
	vec2 p = gl_FragCoord.xy / pc.resolution;
	float dist = 1;

	float l = length (p1 - p0);
	vec2 u = normalize (p1 - p0);

	if (u.y == 0)
		dist = (p.x-p0.x) / l;
	else {
		float m = -u.x / u.y;
		float b = p0.y - m * p0.x;
		dist =((p.y - m * p.x - b) / sqrt (1 + m * m)) / l;
	}

	vec3 color = mix (colors[0], colors[1], smoothstep(stops[0], stops[1], dist));
	for (int i=2; i < count; i++ )
		color = mix(color, colors[i], smoothstep(stops[i-1],stops[i], dist));
	outFragColor = vec4(color,1.0);
}