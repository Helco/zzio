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
	// Based on https://csantosbh.wordpress.com/2014/01/25/manual-texture-filtering-for-pixelated-games-in-webgl/
	// TODO: Create and use SDF fonts
	vec2 alpha = 0.5 / vec2(0.2);
	vec2 tsize = textureSize(sampler2D(mainTexture, mainSampler), 0);
	vec2 iuv = fsin_uv * tsize;
	vec2 fuv = fract(iuv);
	fuv = clamp(alpha * fuv, 0.0, 0.5) +
		clamp(alpha * (fuv - 1.0) + 0.5, 0.0, 0.5);
	vec2 uv = (floor(iuv) + fuv) / tsize;

	vec4 color = texture(sampler2D(mainTexture, mainSampler), uv) * fsin_texWeight;
	color += (1 - fsin_texWeight) * fsin_color;
	if (color.a < 0.2)
		discard;
	color.a = clamp(0.7 + (color.a - 0.4) * 4, 0.0, 1.0);
	fsout_color = color;
}
