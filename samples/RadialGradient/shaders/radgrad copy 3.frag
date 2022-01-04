#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 inUv;
layout (location = 0) out vec4 outFragColor;

const vec3 cp[2] = vec3[2](
	vec3(220,300,0),
	vec3(300,400,400)
);
const vec3 colors[] = vec3[](
	vec3(1,0,0),
	vec3(0,1,0),
	vec3(0,0,1),
	vec3(0,1,0),
	vec3(1,0,0)
);
const float stops[] = float[] (0.0,0.25,0.5,0.75,1.0);
const int count = 5;

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

    /// APPLY FOCUS MODIFIER    
    //project a point on the circle such that it passes through the focus and through the coord,
    //and then get the distance of the focus from that point.
    //that is the effective gradient length
   	float gradLength = 1.0;
   	vec2 diff =c0 - c1;
    vec2 rayDir = normalize(p - c0);
	float a = dot(rayDir, rayDir);
	float b = 2.0 * dot(rayDir, diff);
	float c = dot(diff, diff) - r1 * r1;
	float disc = b * b - 4.0 * a * c;
	if (disc >= 0.0) 
    {
	    float t = (-b + sqrt(abs(disc))) / (2.0 * a);
	    vec2 projection = c0 + rayDir * t;
    	gradLength = distance(projection, c0)-r0;
    }
    else
    {
     	//gradient is undefined for this coordinate   
    }
    
    /// OUTPUT
    float grad = (distance(p, c0)-r0) / gradLength ; 

	float dr = r1 - r0;
	float dist = length(p - c0);
	vec3 color = mix (colors[0], colors[1], smoothstep(stops[0],stops[1], grad));
	for (int i=2; i < count; i++ )
		color = mix(color, colors[i], smoothstep(stops[i-1],stops[i], grad));
	outFragColor = vec4(color,1.0);
}