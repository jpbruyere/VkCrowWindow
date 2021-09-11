#version 450

layout(vertices=4) out;

layout (binding = 1) uniform UBO
{
    mat4 mvp;
    float tessAlpha;
	float tessLevel;
} ubo;

layout (location = 0) in vec3 inColor[];
layout (location = 0) patch out vec3 outColor;

void main()
{
	// get data
	gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
	outColor = inColor[gl_InvocationID];

	gl_TessLevelOuter[0] = float( ubo.tessAlpha );
	gl_TessLevelOuter[1] = float( ubo.tessLevel );
}