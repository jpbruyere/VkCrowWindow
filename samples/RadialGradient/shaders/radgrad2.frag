#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 inUv;
layout (location = 0) out vec4 outFragColor;

const vec3 cp[2] = vec3[2](
	vec3(400,400,10),
	vec3(40,40,200)
);
const vec3 colors[] = vec3[](
	vec3(1,1,1),
	vec3(0.2,0.1,0.2),
	vec3(0,0,0),
	vec3(0,0,0)
);
const float stops[] = float[] (0.0,0.5,1.0);
const int count = 3;

layout(push_constant) uniform PushConsts {
    vec2 resolution;
} pc;

const float ELLIPSE_RATIO = 1.0;
const float TILING = 10.0;

vec4 mainImage()
{
	float r0 = cp[0].z / pc.resolution.x;
	
	vec2 endPoint = pc.resolution/1.15;
	vec2 center = pc.resolution/2.0;
	float radius = distance(endPoint, center);
	vec2 focus = vec2(260);
	vec2 p = gl_FragCoord.xy;
    vec2 coord = p.xy;
       
    /// APPLY FOCUS MODIFIER    
    //project a point on the circle such that it passes through the focus and through the coord,
    //and then get the distance of the focus from that point.
    //that is the effective gradient length
   	float gradLength = 1.0;
   	vec2 diff = focus - center;
    vec2 rayDir = normalize(coord - focus);
	float a = dot(rayDir, rayDir);
	float b = 2.0 * dot(rayDir, diff);
	float c = dot(diff, diff) - radius * radius;
	float disc = b * b - 4.0 * a * c;
	if (disc >= 0.0) 
    {
	    float t = (-b + sqrt(abs(disc))) / (2.0 * a);
	    vec2 projection = focus + rayDir * t;
    	gradLength = distance(projection, focus);
    }
    else
    {
     	//gradient is undefined for this coordinate   
    }
    
    /// OUTPUT
    float grad = distance(coord, focus) / gradLength * TILING;   

	vec3 color = mix (colors[0], colors[1], smoothstep(stops[0],stops[1], grad));
	for (int i=2; i < count; i++ )
		color = mix(color, colors[i], smoothstep(stops[i-1],stops[i], grad));
        
    return vec4(color, 1.0);
}

void main()
{



	outFragColor = mainImage ();
}