#version 450

layout(location = 0) in vec2 vsin_pos;
layout(location = 1) in vec2 vsin_uv;
layout(location = 2) in vec3 vsin_center; // these two per instance
layout(location = 3) in vec3 vsin_dir;
layout(location = 4) in vec4 vsin_color;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out vec4 fsin_color;

layout(set = 0, binding = 2) uniform Projection { mat4 projection; };
layout(set = 0, binding = 3) uniform View { mat4 view; };
layout(set = 0, binding = 4) uniform World { mat4 world; };

void main()
{
	vec4 pos = view * world * vec4(vsin_center, 1);
	vec3 right = (view * world * vec4(vsin_dir, 0)).xyz;
	vec3 up = normalize(cross(pos.xyz, right));
	pos += vec4(vsin_pos.x * right + vsin_pos.y * up, 0);
	pos = projection * pos;
	gl_Position = pos;
	fsin_uv = vsin_uv;
	fsin_color = vsin_color;
}
