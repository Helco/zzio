#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 1) in vec3 vsin_uv;

layout(location = 0) out vec3 fsin_uv;

layout(set = 0, binding = 0) uniform Projection { mat4 projection; };
layout(set = 0, binding = 1) uniform View { mat4 view; };
layout(set = 0, binding = 2) uniform World { mat4 world; };
layout(set = 0, binding = 3) uniform Params
{
	vec4 color;
	float width;
};

void main()
{
	vec4 pos = vec4(vsin_pos, 1);
	pos = world * pos;
	pos = view * pos;
	pos = projection * pos;
	gl_Position = pos;
	fsin_uv = vsin_uv;
}
