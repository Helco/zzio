#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 1) in vec4 vsin_color;
layout(location = 2) in vec4 vsin_weights;
layout(location = 3) in uvec4 vsin_indices;

layout(location = 0) out vec4 fsin_color;

layout(set = 0, binding = 0) uniform Projection { mat4 projection; };
layout(set = 0, binding = 1) uniform View { mat4 view; };
layout(set = 0, binding = 2) uniform World { mat4 world; };
layout(set = 0, binding = 3) readonly buffer PoseBuffer
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
	gl_Position = pos;
	fsin_color = vsin_color;
}
