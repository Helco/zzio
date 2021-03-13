#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 2) in vec4 vsin_color;

layout(location = 0) out vec4 fsin_color;

layout(set = 0, binding = 0) uniform Projection { mat4 projection; };
layout(set = 0, binding = 1) uniform View { mat4 view; };
layout(set = 0, binding = 2) uniform World { mat4 world; };

void main()
{
	vec4 pos = vec4(vsin_pos, 1);
	pos = world * pos;
	pos = view * pos;
	pos = projection * pos;
	gl_Position = pos;
	fsin_color = vsin_color;
}
