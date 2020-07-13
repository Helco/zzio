#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) out vec4 fsout_color;
layout (set = 0, binding = 0) uniform UniformBlock
{
	vec4 color;
};

void main()
{
	fsout_color = color;
}
