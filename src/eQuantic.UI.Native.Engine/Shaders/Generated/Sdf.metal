#include <metal_stdlib>
#include <metal_math>
#include <metal_texture>
using namespace metal;

#line 90 "core"
struct vertexOutput_0
{
    float4 output_0 [[position]];
};


#line 8 "/Users/admin.edgar.a.mesquita/projects/equantic/equantic-ui/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
struct DrawUniforms_0
{
    float4 inv0_0;
    float4 inv1_0;
    float4 rect_0;
    float4 radii_0;
    float4 colorA_0;
    float4 colorB_0;
    float4 gradient_0;
    float4 flags_0;
    float4 clipRect_0;
    float4 clipRadii_0;
};


#line 1084 "core"
struct KernelContext_0
{
    DrawUniforms_0 constant* u_0;
    texture2d<float, access::sample> coverageTexture_0;
    texture2d<float, access::sample> colorTexture_0;
};


#line 50 "/Users/admin.edgar.a.mesquita/projects/equantic/equantic-ui/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
[[vertex]] vertexOutput_0 fullscreen_vertex(uint vid_0 [[vertex_id]], DrawUniforms_0 constant* u_1 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_1 [[texture(0)]], texture2d<float, access::sample> colorTexture_1 [[texture(1)]])
{

#line 50
    thread KernelContext_0 kernelContext_0;

#line 50
    (&kernelContext_0)->u_0 = u_1;

#line 50
    (&kernelContext_0)->coverageTexture_0 = coverageTexture_1;

#line 50
    (&kernelContext_0)->colorTexture_0 = colorTexture_1;


    array<float2, int(3)> positions_0 = { float2(-1.0f, -1.0f), float2(3.0f, -1.0f), float2(-1.0f, 3.0f) };

#line 53
    vertexOutput_0 _S1 = { float4(positions_0[vid_0], 0.0f, 1.0f) };
    return _S1;
}


#line 38
float sdRoundedRect_0(float2 p_0, float2 halfSize_0, float4 radii_1)
{

#line 38
    float r_0;


    if((p_0.x) >= 0.0f)
    {

#line 41
        if((p_0.y) >= 0.0f)
        {

#line 41
            r_0 = radii_1.z;

#line 41
        }
        else
        {

#line 41
            r_0 = radii_1.y;

#line 41
        }

#line 41
    }
    else
    {

#line 42
        if((p_0.y) >= 0.0f)
        {

#line 42
            r_0 = radii_1.w;

#line 42
        }
        else
        {

#line 42
            r_0 = radii_1.x;

#line 42
        }

#line 41
    }

    float2 q_0 = abs(p_0) - (halfSize_0 - float2(r_0) );


    return length(max(q_0, float2(0.0f) )) + min(max(q_0.x, q_0.y), 0.0f) - r_0;
}


#line 32
float srgbToLinear_0(float c_0)
{

#line 32
    float _S2;

    if(c_0 <= 0.04044999927282333f)
    {

#line 34
        _S2 = c_0 / 12.92000007629394531f;

#line 34
    }
    else
    {

#line 34
        _S2 = pow((c_0 + 0.05499999970197678f) / 1.0549999475479126f, 2.40000009536743164f);

#line 34
    }

#line 34
    return _S2;
}


#line 34
struct pixelOutput_0
{
    float4 output_1 [[color(0)]];
};


#line 58
[[fragment]] pixelOutput_0 sdf_fragment(float4 position_0 [[position]], DrawUniforms_0 constant* u_2 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_2 [[texture(0)]], texture2d<float, access::sample> colorTexture_2 [[texture(1)]])
{

#line 58
    thread KernelContext_0 kernelContext_1;

#line 58
    (&kernelContext_1)->u_0 = u_2;

#line 58
    (&kernelContext_1)->coverageTexture_0 = coverageTexture_2;

#line 58
    (&kernelContext_1)->colorTexture_0 = colorTexture_2;


    float2 pixel_0 = position_0.xy;

    float _S3 = pixel_0.x;

#line 63
    float _S4 = pixel_0.y;

#line 63
    float _S5 = _S3 * u_2->inv0_0.x + _S4 * u_2->inv0_0.y + u_2->inv0_0.z;
    float _S6 = _S3 * u_2->inv1_0.x + _S4 * u_2->inv1_0.y + u_2->inv1_0.z;

#line 62
    float2 local_0 = float2(_S5, _S6);



    float d_0 = sdRoundedRect_0(local_0 - u_2->rect_0.xy, u_2->rect_0.zw, u_2->radii_0);

#line 66
    bool _S7;
    if((u_2->flags_0.x) > 0.5f)
    {

#line 67
        _S7 = (u_2->flags_0.x) < 1.5f;

#line 67
    }
    else
    {

#line 67
        _S7 = false;

#line 67
    }

#line 67
    float d_1;

#line 67
    if(_S7)
    {

#line 67
        d_1 = abs(d_0) - u_2->inv1_0.w / 2.0f;

#line 67
    }
    else
    {

#line 67
        d_1 = d_0;

#line 67
    }

#line 67
    float coverage_0;



    if((u_2->flags_0.x) > 1.5f)
    {

        float sigma_0 = u_2->inv1_0.w * u_2->inv0_0.w / 2.0f;
        if(sigma_0 <= 0.0f)
        {

#line 75
            coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 75
        }
        else
        {



            float t_0 = clamp((d_1 * u_2->inv0_0.w + 1.5f * sigma_0) / (3.0f * sigma_0), 0.0f, 1.0f);

#line 81
            coverage_0 = 1.0f - t_0 * t_0 * (3.0f - 2.0f * t_0);

#line 75
        }

#line 71
    }
    else
    {

#line 71
        coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 71
    }

#line 89
    if(coverage_0 <= 0.0f)
    {

#line 89
        discard_fragment();

#line 89
    }



    if((u_2->flags_0.z) > 0.5f)
    {

        float coverage_1 = coverage_0 * clamp(0.5f - sdRoundedRect_0(pixel_0 - (&kernelContext_1)->u_0->clipRect_0.xy, (&kernelContext_1)->u_0->clipRect_0.zw, (&kernelContext_1)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_1 <= 0.0f)
        {

#line 97
            discard_fragment();

#line 97
        }

#line 97
        coverage_0 = coverage_1;

#line 93
    }

#line 102
    float4 _S8 = (&kernelContext_1)->u_0->colorA_0;

#line 102
    float dx_0;

#line 102
    float4 srgb_0;
    if((u_2->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_1)->u_0->gradient_0.z) <= 0.0f)
        {

#line 106
            dx_0 = 0.0f;

#line 106
        }
        else
        {

#line 106
            dx_0 = (_S5 - (&kernelContext_1)->u_0->gradient_0.x) / (&kernelContext_1)->u_0->gradient_0.z;

#line 106
        }

#line 106
        float dy_0;
        if(((&kernelContext_1)->u_0->gradient_0.w) <= 0.0f)
        {

#line 107
            dy_0 = 0.0f;

#line 107
        }
        else
        {

#line 107
            dy_0 = (_S6 - (&kernelContext_1)->u_0->gradient_0.y) / (&kernelContext_1)->u_0->gradient_0.w;

#line 107
        }

#line 107
        srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(clamp(sqrt(dx_0 * dx_0 + dy_0 * dy_0), 0.0f, 1.0f)) );

#line 103
    }
    else
    {

#line 111
        if((u_2->flags_0.y) > 0.5f)
        {
            float2 axis_0 = (&kernelContext_1)->u_0->gradient_0.zw - (&kernelContext_1)->u_0->gradient_0.xy;
            float len2_0 = dot(axis_0, axis_0);
            if(len2_0 <= 0.0f)
            {

#line 115
                dx_0 = 0.0f;

#line 115
            }
            else
            {

#line 115
                dx_0 = clamp(dot(local_0 - (&kernelContext_1)->u_0->gradient_0.xy, axis_0) / len2_0, 0.0f, 1.0f);

#line 115
            }

#line 115
            srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(dx_0) );

#line 111
        }
        else
        {

#line 111
            srgb_0 = _S8;

#line 111
        }

#line 103
    }

#line 120
    float a_0 = srgb_0.w * coverage_0;

#line 120
    pixelOutput_0 _S9 = { float4(float3(srgbToLinear_0(srgb_0.x), srgbToLinear_0(srgb_0.y), srgbToLinear_0(srgb_0.z)) * float3(a_0) , a_0) };

    return _S9;
}


#line 122
struct pixelOutput_1
{
    float4 output_2 [[color(0)]];
};



[[fragment]] pixelOutput_1 textured_fragment(float4 position_1 [[position]], DrawUniforms_0 constant* u_3 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_3 [[texture(0)]], texture2d<float, access::sample> colorTexture_3 [[texture(1)]])
{

#line 129
    thread KernelContext_0 kernelContext_2;

#line 129
    (&kernelContext_2)->u_0 = u_3;

#line 129
    (&kernelContext_2)->coverageTexture_0 = coverageTexture_3;

#line 129
    (&kernelContext_2)->colorTexture_0 = colorTexture_3;

    float2 pixel_1 = position_1.xy;

    float _S10 = pixel_1.x;

#line 133
    float _S11 = pixel_1.y;

#line 132
    float2 local_1 = float2(_S10 * u_3->inv0_0.x + _S11 * u_3->inv0_0.y + u_3->inv0_0.z, _S10 * u_3->inv1_0.x + _S11 * u_3->inv1_0.y + u_3->inv1_0.z);

#line 138
    float2 uv_0 = (local_1 - (u_3->rect_0.xy - u_3->rect_0.zw)) / (u_3->rect_0.zw * float2(2.0f) );
    float _S12 = uv_0.x;

#line 139
    bool _S13;

#line 139
    if(_S12 < 0.0f)
    {

#line 139
        _S13 = true;

#line 139
    }
    else
    {

#line 139
        _S13 = _S12 >= 1.0f;

#line 139
    }

#line 139
    if(_S13)
    {

#line 139
        _S13 = true;

#line 139
    }
    else
    {

#line 139
        _S13 = (uv_0.y) < 0.0f;

#line 139
    }

#line 139
    if(_S13)
    {

#line 139
        _S13 = true;

#line 139
    }
    else
    {

#line 139
        _S13 = (uv_0.y) >= 1.0f;

#line 139
    }

#line 139
    if(_S13)
    {

#line 139
        discard_fragment();

#line 139
    }



    int3 _S14 = int3(min(int((&kernelContext_2)->u_0->gradient_0.x) - int(1), int(_S12 * (&kernelContext_2)->u_0->gradient_0.x)), min(int((&kernelContext_2)->u_0->gradient_0.y) - int(1), int(uv_0.y * (&kernelContext_2)->u_0->gradient_0.y)), int(0));

#line 143
    float coverage_2 = (((&kernelContext_2)->coverageTexture_0).read(vec<uint,2>(((_S14)).xy), uint(((_S14)).z)).x);
    if(coverage_2 <= 0.0f)
    {

#line 144
        discard_fragment();

#line 144
    }

#line 144
    float coverage_3;

    if(((&kernelContext_2)->u_0->flags_0.z) > 0.5f)
    {

        float coverage_4 = coverage_2 * clamp(0.5f - sdRoundedRect_0(pixel_1 - (&kernelContext_2)->u_0->clipRect_0.xy, (&kernelContext_2)->u_0->clipRect_0.zw, (&kernelContext_2)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_4 <= 0.0f)
        {

#line 150
            discard_fragment();

#line 150
        }

#line 150
        coverage_3 = coverage_4;

#line 146
    }
    else
    {

#line 146
        coverage_3 = coverage_2;

#line 146
    }

#line 157
    float4 _S15 = (&kernelContext_2)->u_0->colorA_0;

#line 157
    float4 srgb_1;
    if(((&kernelContext_2)->u_0->flags_0.y) > 0.5f)
    {
        float2 axis_1 = (&kernelContext_2)->u_0->radii_0.zw - (&kernelContext_2)->u_0->radii_0.xy;
        float len2_1 = dot(axis_1, axis_1);

#line 161
        float t_1;
        if(len2_1 <= 0.0f)
        {

#line 162
            t_1 = 0.0f;

#line 162
        }
        else
        {

#line 162
            t_1 = clamp(dot(local_1 - (&kernelContext_2)->u_0->radii_0.xy, axis_1) / len2_1, 0.0f, 1.0f);

#line 162
        }

#line 162
        srgb_1 = mix((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, float4(t_1) );

#line 158
    }
    else
    {

#line 158
        srgb_1 = _S15;

#line 158
    }

#line 166
    float a_1 = srgb_1.w * coverage_3;

#line 166
    pixelOutput_1 _S16 = { float4(float3(srgbToLinear_0(srgb_1.x), srgbToLinear_0(srgb_1.y), srgbToLinear_0(srgb_1.z)) * float3(a_1) , a_1) };

    return _S16;
}


#line 168
struct pixelOutput_2
{
    float4 output_3 [[color(0)]];
};



[[fragment]] pixelOutput_2 textured_rgba_fragment(float4 position_2 [[position]], DrawUniforms_0 constant* u_4 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_4 [[texture(0)]], texture2d<float, access::sample> colorTexture_4 [[texture(1)]])
{

#line 175
    thread KernelContext_0 kernelContext_3;

#line 175
    (&kernelContext_3)->u_0 = u_4;

#line 175
    (&kernelContext_3)->coverageTexture_0 = coverageTexture_4;

#line 175
    (&kernelContext_3)->colorTexture_0 = colorTexture_4;

    float2 pixel_2 = position_2.xy;

    float _S17 = pixel_2.x;

#line 179
    float _S18 = pixel_2.y;



    float2 uv_1 = (float2(_S17 * u_4->inv0_0.x + _S18 * u_4->inv0_0.y + u_4->inv0_0.z, _S17 * u_4->inv1_0.x + _S18 * u_4->inv1_0.y + u_4->inv1_0.z) - (u_4->rect_0.xy - u_4->rect_0.zw)) / (u_4->rect_0.zw * float2(2.0f) );
    float _S19 = uv_1.x;

#line 184
    bool _S20;

#line 184
    if(_S19 < 0.0f)
    {

#line 184
        _S20 = true;

#line 184
    }
    else
    {

#line 184
        _S20 = _S19 >= 1.0f;

#line 184
    }

#line 184
    if(_S20)
    {

#line 184
        _S20 = true;

#line 184
    }
    else
    {

#line 184
        _S20 = (uv_1.y) < 0.0f;

#line 184
    }

#line 184
    if(_S20)
    {

#line 184
        _S20 = true;

#line 184
    }
    else
    {

#line 184
        _S20 = (uv_1.y) >= 1.0f;

#line 184
    }

#line 184
    if(_S20)
    {

#line 184
        discard_fragment();

#line 184
    }



    int3 _S21 = int3(min(int((&kernelContext_3)->u_0->gradient_0.x) - int(1), int(_S19 * (&kernelContext_3)->u_0->gradient_0.x)), min(int((&kernelContext_3)->u_0->gradient_0.y) - int(1), int(uv_1.y * (&kernelContext_3)->u_0->gradient_0.y)), int(0));

#line 188
    float4 texel_0 = (((&kernelContext_3)->colorTexture_0).read(vec<uint,2>(((_S21)).xy), uint(((_S21)).z)));

#line 188
    float coverage_5;


    if(((&kernelContext_3)->u_0->flags_0.z) > 0.5f)
    {

#line 191
        coverage_5 = clamp(0.5f - sdRoundedRect_0(pixel_2 - (&kernelContext_3)->u_0->clipRect_0.xy, (&kernelContext_3)->u_0->clipRect_0.zw, (&kernelContext_3)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 191
    }
    else
    {

#line 191
        coverage_5 = 1.0f;

#line 191
    }

#line 197
    float a_2 = texel_0.w * coverage_5;
    if(a_2 <= 0.0f)
    {

#line 198
        discard_fragment();

#line 198
    }

#line 198
    pixelOutput_2 _S22 = { float4(texel_0.xyz * float3(a_2) , a_2) };
    return _S22;
}

