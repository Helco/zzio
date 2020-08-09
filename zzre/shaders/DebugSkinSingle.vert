#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 3) in vec4 vsin_weights;
layout(location = 4) in uvec4 vsin_indices;

layout(location = 0) out vec4 fsin_color;
layout(set = 0, binding = 0) uniform TransformationUniforms
{
	mat4 projection;
	mat4 view;
	mat4 world;
};
layout(set = 0, binding = 1) uniform MaterialUniforms
{
	int boneIndex;
};

void main()
{
	vec4 pos = vec4(vsin_pos, 1);
	pos = world * pos;
	pos = view * pos;
	pos = projection * pos;
	gl_Position = pos;

	float c =
		float(vsin_indices.x == boneIndex) * vsin_weights.x +
		float(vsin_indices.y == boneIndex) * vsin_weights.y +
		float(vsin_indices.z == boneIndex) * vsin_weights.z +
		float(vsin_indices.w == boneIndex) * vsin_weights.w;
	fsin_color = vec4(c, 0, 0, c);
}
