#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec3 inNormal;
layout (location = 2) in vec2 inUV;


layout (location = 0) out vec3 outN;
layout (location = 1) out vec3 outColor;

out gl_PerVertex
{
    vec4 gl_Position;
};


void main()
{
	outColor = vec3 (inUV, 1.0);
	outN = inNormal;
	gl_Position = vec4(inPos.xyz, 1.0);
}
