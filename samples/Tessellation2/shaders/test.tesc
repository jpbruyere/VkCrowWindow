#version 450

layout(vertices=3) out;

layout (binding = 1) uniform UBO
{
    mat4 mvp;
    float tessAlpha;
	float tessLevel;
} ubo;

layout (location = 0) in vec3 inColor[];
layout (location = 0) out vec3 outColor[];

void main()
{
	// get data
	gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
	outColor[gl_InvocationID] = inColor[gl_InvocationID];
	// set base
	// set tess levels
	gl_TessLevelOuter[gl_InvocationID] = ubo.tessLevel;
	gl_TessLevelInner[0] = ubo.tessLevel;
}