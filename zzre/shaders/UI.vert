#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 vsin_pos;
layout(location = 1) in vec2 vsin_size;
layout(location = 2) in vec2 vsin_uvPos;
layout(location = 3) in vec2 vsin_uvSize;
layout(location = 4) in float vsin_texWeight;
layout(location = 5) in vec4 vsin_color;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out float fsin_texWeight;
layout(location = 2) out vec4 fsin_color;

layout(set = 0, binding = 2) uniform ScreenSize { vec2 screenSize; };

void main()
{
	vec2 VertexOffsets[4] = 
	{
		vec2(0, 0),
		vec2(1, 0),
		vec2(0, 1),
		vec2(1, 1)
	};
	vec2 pos = floor(floor(vsin_pos) + vsin_size * VertexOffsets[gl_VertexIndex]);
	pos = pos / screenSize;
	pos = pos * 2 - 1;
	pos.y *= -1;
	gl_Position = vec4(pos, 0.5, 1);

	fsin_uv = vsin_uvPos + vsin_uvSize * VertexOffsets[gl_VertexIndex];
	fsin_texWeight = vsin_texWeight;
	fsin_color = vsin_color;
}
