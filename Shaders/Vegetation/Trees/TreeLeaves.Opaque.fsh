#version 140
#pragma Pipeline

#pragma ACGLimport <Common/Lighting.fsh>
#pragma ACGLimport <Common/Camera.csh>

// material:
uniform sampler2D uColor;

uniform float uDiscardBias = 0.5;
uniform vec4 uColorModulation = vec4(1.0);

in vec2 vTexCoord;
in vec3 vNormal;
in vec3 vWorldPos;

OUTPUT_CHANNEL_Color(vec3)
OUTPUT_CHANNEL_Normal(vec3)
OUTPUT_CHANNEL_Position(vec3)

void main()
{
    vec4 texColor = texture(uColor, vTexCoord);

    float disc = uDiscardBias;
    disc = distance(vWorldPos, uCameraPosition);
    disc = 0.901-clamp(disc/100,0,0.9);

    if(texColor.a < disc)
        discard;

    texColor.rgb /= texColor.a + 0.001; // premultiplied alpha
    texColor.rgb *= uColorModulation.rgb;


    vec3 normalFront = mix(vNormal, -vNormal, float(!gl_FrontFacing));
    const float translucency = 0.5;
    vec3 color = leafLighting(vWorldPos, normalFront, translucency, texColor.rgb, vec4(vec3(0),1));

    OUTPUT_Color(color);
    OUTPUT_Normal(normalFront);
    OUTPUT_Position(vWorldPos);
}
