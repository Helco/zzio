#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 3) in vec4 vsin_weights;
layout(location = 4) in uvec4 vsin_indices;

layout(location = 0) out vec4 fsin_color;
layout(set = 0, binding = 0) uniform Projection { mat4 projection; };
layout(set = 0, binding = 1) uniform View { mat4 view; };
layout(set = 0, binding = 2) uniform World { mat4 world; };
layout(set = 0, binding = 3) uniform MaterialUniforms
{
	float alpha;
};

vec4 GetDebugColorOf(uint index)
{
	vec3 colors[3];
	colors[0] = vec3(1, 0, 0);
	colors[1] = vec3(0, 1, 0);
	colors[2] = vec3(0, 0, 1);
	return vec4(colors[(index + 1) % 3], 1);
	// add by 2 (aka -1 in the modulo class) to align color with debug skeleton bones
}

void main()
{
	vec4 pos = vec4(vsin_pos, 1);
	pos = world * pos;
	pos = view * pos;
	pos = projection * pos;
	gl_Position = pos;

	fsin_color =
		vsin_weights.x * GetDebugColorOf(vsin_indices.x) +
		vsin_weights.y * GetDebugColorOf(vsin_indices.y) +
		vsin_weights.z * GetDebugColorOf(vsin_indices.z) +
		vsin_weights.w * GetDebugColorOf(vsin_indices.w);
}
