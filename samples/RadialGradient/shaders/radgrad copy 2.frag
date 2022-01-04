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

const float ELLIPSE_RATIO = 1.0;
const float TILING = 10.0;

vec4 mainImage()
{
	float r0 = cp[0].z / pc.resolution.x;
	
	vec2 endPoint = pc.resolution/1.15;
	vec2 center = pc.resolution/2.0;
	float radius = distance(endPoint, center);
	vec2 focus = vec2(400);
	vec2 p = gl_FragCoord.xy;
    vec2 coord = p.xy;
    
    /// APPLY ELLIPSE MODIFIER    
    // project the coordinate onto the axis between the center and the endpoint of the gradient.
    // the longer this projection is, the more the coordinate should be squashed.
    vec2 axis = endPoint - center;

	/*float l2 = dot(axis, axis);
	if (l2 != 0.0)
	{
		float d = dot(coord - center, axis) / l2;
		vec2 proj = center + d * axis;
        coord = proj - (proj - coord) * ELLIPSE_RATIO;
        
        //ellipsy the focus point as well
        float d2 = dot(focus - center, axis) / l2;
        vec2 proj2 = center + d2 * axis;
        focus = proj2 - (proj2 - focus) * ELLIPSE_RATIO;
    }*/
    
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
    vec3 col = mix(vec3(0.75, 0.75, 0.95), vec3(0.0, 0.25, 0.35), fract(grad));
    
    
    //MARK THE CENTER, END AND FOCUS POINTS
    col = mix(col, vec3(1.0, 1.0, 0.0), 1.0 - smoothstep(1.0, 2.6, distance(coord, center)));
    col = mix(col, vec3(1.0, 1.0, 0.0), 1.0 - smoothstep(1.0, 2.6, distance(coord, endPoint)));
    col = mix(col, vec3(1.0, 1.0, 1.0), 1.0 - smoothstep(1.0, 2.6, distance(coord, focus)));
    //MARK THE PERIMETER
    if (distance(coord, center) > radius)
    {
        col = mix(col, vec3(0.0, 0.0, 0.0), 0.5);

    };
        
    return vec4(col, 1.0);
}

void main()
{



	outFragColor = mainImage ();
}