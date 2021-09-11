#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec3 inColor;

layout (binding = 1) uniform UBO
{
	mat4 mvp;
};

layout (location = 0) out vec3 outColor;

out gl_PerVertex
{
    vec4 gl_Position;
	float gl_PointSize;
};


void main()
{
	gl_PointSize = 10.0;
	outColor = inColor;
	gl_Position = vec4(inPos.xyz, 1.0);
}
