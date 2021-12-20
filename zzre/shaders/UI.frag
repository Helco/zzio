#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in float fsin_texWeight;
layout(location = 2) in vec4 fsin_color;

layout(location = 0) out vec4 fsout_color;

layout(set = 0, binding = 0) uniform texture2D mainTexture;
layout(set = 0, binding = 1) uniform sampler mainSampler;

void main()
{
	vec4 color = texture(sampler2D(mainTexture, mainSampler), fsin_uv) * fsin_texWeight;
	color += (1 - fsin_texWeight) * fsin_color;
	if (color.a < 0.1)
		discard;
	fsout_color = color;
}
