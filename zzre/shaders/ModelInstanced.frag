#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in vec4 fsin_color;
layout(location = 2) in vec4 fsin_tint;

layout(location = 0) out vec4 fsout_color;

layout(set = 0, binding = 0) uniform sampler2D mainTexture;
layout (set = 0, binding = 4) uniform MaterialUniforms
{
	float vectorColorFactor;
	float tintFactor;
	float alphaReference;
};

vec4 weighColor(vec4 color, float factor)
{
	return color * factor + vec4(1,1,1,1) * (1 - factor);
}

void main()
{
	vec4 color = texture(mainTexture, fsin_uv)
		* weighColor(fsin_color, vectorColorFactor)
		* weighColor(fsin_tint, tintFactor);
	if (color.a < alphaReference)
		discard;
	fsout_color = color;
}
