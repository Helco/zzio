#version 450

layout(location = 0) in vec3 vsin_pos;
layout(location = 1) in vec2 vsin_uvCenter;
layout(location = 2) in vec2 vsin_uvSize;
layout(location = 3) in float vsin_size;
layout(location = 4) in vec4 vsin_color;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out vec4 fsin_color;

layout(set = 0, binding = 2) uniform Projection { mat4 projection; };
layout(set = 0, binding = 3) uniform View { mat4 view; };
layout(set = 0, binding = 4) uniform World { mat4 world; };

layout(set = 0, binding = 5) uniform Uniforms
{
	vec2 screenSize;
};

void main()
{
	vec4 pos = vec4(vsin_pos, 1);
	pos = world * pos;
	pos = view * pos;
	pos = projection * pos;

	vec2 vertexFactor = vec2((gl_VertexIndex % 2) > 0 ? 1 : -1, (gl_VertexIndex / 2) > 0 ? 1 : -1);
	pos.xy += vertexFactor * vsin_size / screenSize * 2;
	gl_Position = pos;
	fsin_uv = vsin_uvCenter + vertexFactor * vsin_uvSize / 2;
	fsin_color = vsin_color;
}
