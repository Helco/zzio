#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 1) in vec2 vsin_uv;
layout(location = 2) in vec4 vsin_color;
layout(location = 3) in vec4 vsin_weights;
layout(location = 4) in uvec4 vsin_indices;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out vec4 fsin_color;

layout(set = 0, binding = 2) uniform Projection { mat4 projection; };
layout(set = 0, binding = 3) uniform View { mat4 view; };
layout(set = 0, binding = 4) uniform World { mat4 world; };
layout(set = 0, binding = 6) readonly buffer PoseBuffer
{
	mat4 pose[];
};

void main()
{
	vec4 pos =
		(pose[vsin_indices.x] * vec4(vsin_pos, 1)) * vsin_weights.x +
		(pose[vsin_indices.y] * vec4(vsin_pos, 1)) * vsin_weights.y +
		(pose[vsin_indices.z] * vec4(vsin_pos, 1)) * vsin_weights.z +
		(pose[vsin_indices.w] * vec4(vsin_pos, 1)) * vsin_weights.w;
	pos = world * vec4(pos.xyz, 1);
	pos = view * pos;
	pos = projection * pos;
	fsin_uv = vsin_uv;
	fsin_color = vsin_color;
	gl_Position = pos;
}
