#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec4 fsin_color;
layout(location = 0) out vec4 fsout_color;

void main()
{
	fsout_color = fsin_color;
}
