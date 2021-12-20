#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 vsin_center;
layout(location = 1) in vec2 vsin_hsize;
layout(location = 2) in vec2 vsin_uvCenter;
layout(location = 3) in vec2 vsin_uvSize;
layout(location = 4) in float vsin_texWeight;
layout(location = 5) in vec4 vsin_color;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out float fsin_texWeight;
layout(location = 2) out vec4 fsin_color;

layout(set = 0, binding = 2) uniform Projection { mat4 projection; };

void main()
{
	vec2 VertexOffsets[4] = 
	{
		vec2(-1, -1),
		vec2(+1, -1),
		vec2(-1, +1),
		vec2(+1, +1)
	};
	vec2 pos = vsin_center + (vsin_hsize + 0.5) * VertexOffsets[gl_VertexIndex];
	gl_Position = projection * vec4(pos, -0.5, 1);

	fsin_uv = vsin_uvCenter + vsin_uvSize * VertexOffsets[gl_VertexIndex];
	fsin_texWeight = vsin_texWeight;
	fsin_color = vsin_color;
}
