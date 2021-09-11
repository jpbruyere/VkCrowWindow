#version 450

layout( isolines, equal_spacing) in;

layout (binding = 1) uniform UBO
{
	mat4 mvp;
	float tessAlpha;
	float tessLevel;
} ubo;

layout (location = 0) patch in vec3 inColor;
layout (location = 0) out vec3 outColor;

void main()
{
	outColor = inColor;

	vec4 p0 = gl_in[0].gl_Position;
	vec4 p1 = gl_in[1].gl_Position;
	vec4 p2 = gl_in[2].gl_Position;
	vec4 p3 = gl_in[3].gl_Position;
	float u = gl_TessCoord.x;
	// the basis functions:
	float b0 = (1.-u) * (1.-u) * (1.-u);
	float b1 = 3. * u * (1.-u) * (1.-u);
	float b2 = 3. * u * u * (1.-u);
	float b3 = u * u * u;

	gl_Position = b0*p0 + b1*p1 + b2*p2 + b3*p3;
 	gl_Position = ubo.mvp * gl_Position;
}