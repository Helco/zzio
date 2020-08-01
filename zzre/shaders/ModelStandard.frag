#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in vec4 fsin_color;

layout(location = 0) out vec4 fsout_color;

layout(set = 0, binding = 0) uniform sampler2D mainTexture;
layout (set = 0, binding = 2) uniform UniformBlock
{
	mat4 projection;
	mat4 view;
	mat4 world;
	vec4 tint;
};

void main()
{
	vec4 color = texture(mainTexture, fsin_uv) * fsin_color * tint;
	if (color.a < 0.03) // TODO: put alpha reference in uniforms
		discard;
	fsout_color = color;
}
