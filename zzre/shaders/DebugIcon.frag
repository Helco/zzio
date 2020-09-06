#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in vec4 fsin_color;

layout(location = 0) out vec4 fsout_color;

layout(set = 0, binding = 0) uniform sampler2D mainTexture;

void main()
{
	vec4 color = texture(mainTexture, fsin_uv).r * fsin_color;
	if (color.a < 0.1)
		discard;
	fsout_color = color;
}
