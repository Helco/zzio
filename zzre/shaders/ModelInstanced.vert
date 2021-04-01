#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 1) in vec2 vsin_uv;
layout(location = 2) in vec4 vsin_color;
layout(location = 3) in vec4 vsin_world0;
layout(location = 4) in vec4 vsin_world1;
layout(location = 5) in vec4 vsin_world2;
layout(location = 6) in vec4 vsin_world3;
layout(location = 7) in vec4 vsin_tint;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out vec4 fsin_color;
layout(location = 2) out vec4 fsin_tint;

layout(set = 0, binding = 2) uniform Projection { mat4 projection; };
layout(set = 0, binding = 3) uniform View { mat4 view; };

void main()
{
	mat4 world = mat4(vsin_world0, vsin_world1, vsin_world2, vsin_world3);
	vec4 pos = vec4(vsin_pos, 1);
	pos = world * pos;
	pos = view * pos;
	pos = projection * pos;
	gl_Position = pos;
	fsin_uv = vsin_uv;
	fsin_color = vsin_color;
	fsin_tint = vsin_tint;
}
