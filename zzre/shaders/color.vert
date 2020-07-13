#version 450

layout(location = 0) in vec3 vsin_pos;

void main()
{
	gl_Position = vec4(vsin_pos, 1);
}
