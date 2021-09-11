#version 450

layout(triangles, fractional_odd_spacing, ccw) in;

layout (binding = 1) uniform UBO
{
	mat4 mvp;
	float tessAlpha;
	float tessLevel;
} ubo;

layout (location = 0) in vec3 inColor[];
layout (location = 0) out vec3 outColor;

void main()
{
	outColor = (gl_TessCoord.x * inColor[0] + gl_TessCoord.y * inColor[1] + gl_TessCoord.z * inColor[2]) / 3.0;
	gl_Position = (gl_TessCoord.x * gl_in[0].gl_Position) +
				(gl_TessCoord.y * gl_in[1].gl_Position) +
				(gl_TessCoord.z * gl_in[2].gl_Position);
 	gl_Position = ubo.mvp * gl_Position;
}