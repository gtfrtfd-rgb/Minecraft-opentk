#version 330 core

in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D textureAtlas;

void main()
{
    // NEAREST-фильтрация задаётся в TextureAtlas, но дублируем для чёткости пикселей
    FragColor = texture(textureAtlas, TexCoord);
}