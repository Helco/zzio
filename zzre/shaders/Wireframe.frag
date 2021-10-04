#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec4 fsin_uv;
layout(location = 0) out vec4 fsout_color;

layout(set = 0, binding = 3) uniform Params
{
	vec4 color;
	float width;
};

void main()
{
	vec3 f = step(fwidth(fsin_uv) * width, fsin_uv);
	if (min(min(f.x, f.y), f.z) < 0.9)
		discard;
	fsout_color = color;
}
