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
};


#line 47 "/Users/admin.edgar.a.mesquita/projects/equantic/equantic-ui/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
[[vertex]] vertexOutput_0 fullscreen_vertex(uint vid_0 [[vertex_id]], DrawUniforms_0 constant* u_1 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_1 [[texture(0)]])
{

#line 47
    thread KernelContext_0 kernelContext_0;

#line 47
    (&kernelContext_0)->u_0 = u_1;

#line 47
    (&kernelContext_0)->coverageTexture_0 = coverageTexture_1;


    array<float2, int(3)> positions_0 = { float2(-1.0f, -1.0f), float2(3.0f, -1.0f), float2(-1.0f, 3.0f) };

#line 50
    vertexOutput_0 _S1 = { float4(positions_0[vid_0], 0.0f, 1.0f) };
    return _S1;
}


#line 35
float sdRoundedRect_0(float2 p_0, float2 halfSize_0, float4 radii_1)
{

#line 35
    float r_0;


    if((p_0.x) >= 0.0f)
    {

#line 38
        if((p_0.y) >= 0.0f)
        {

#line 38
            r_0 = radii_1.z;

#line 38
        }
        else
        {

#line 38
            r_0 = radii_1.y;

#line 38
        }

#line 38
    }
    else
    {

#line 39
        if((p_0.y) >= 0.0f)
        {

#line 39
            r_0 = radii_1.w;

#line 39
        }
        else
        {

#line 39
            r_0 = radii_1.x;

#line 39
        }

#line 38
    }

    float2 q_0 = abs(p_0) - (halfSize_0 - float2(r_0) );


    return length(max(q_0, float2(0.0f) )) + min(max(q_0.x, q_0.y), 0.0f) - r_0;
}


#line 29
float srgbToLinear_0(float c_0)
{

#line 29
    float _S2;

    if(c_0 <= 0.04044999927282333f)
    {

#line 31
        _S2 = c_0 / 12.92000007629394531f;

#line 31
    }
    else
    {

#line 31
        _S2 = pow((c_0 + 0.05499999970197678f) / 1.0549999475479126f, 2.40000009536743164f);

#line 31
    }

#line 31
    return _S2;
}


#line 31
struct pixelOutput_0
{
    float4 output_1 [[color(0)]];
};


#line 55
[[fragment]] pixelOutput_0 sdf_fragment(float4 position_0 [[position]], DrawUniforms_0 constant* u_2 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_2 [[texture(0)]])
{

#line 55
    thread KernelContext_0 kernelContext_1;

#line 55
    (&kernelContext_1)->u_0 = u_2;

#line 55
    (&kernelContext_1)->coverageTexture_0 = coverageTexture_2;


    float2 pixel_0 = position_0.xy;

    float _S3 = pixel_0.x;

#line 60
    float _S4 = pixel_0.y;

#line 59
    float2 local_0 = float2(_S3 * u_2->inv0_0.x + _S4 * u_2->inv0_0.y + u_2->inv0_0.z, _S3 * u_2->inv1_0.x + _S4 * u_2->inv1_0.y + u_2->inv1_0.z);



    float d_0 = sdRoundedRect_0(local_0 - u_2->rect_0.xy, u_2->rect_0.zw, u_2->radii_0);

#line 63
    bool _S5;
    if((u_2->flags_0.x) > 0.5f)
    {

#line 64
        _S5 = (u_2->flags_0.x) < 1.5f;

#line 64
    }
    else
    {

#line 64
        _S5 = false;

#line 64
    }

#line 64
    float d_1;

#line 64
    if(_S5)
    {

#line 64
        d_1 = abs(d_0) - u_2->inv1_0.w / 2.0f;

#line 64
    }
    else
    {

#line 64
        d_1 = d_0;

#line 64
    }

#line 64
    float coverage_0;



    if((u_2->flags_0.x) > 1.5f)
    {

        float sigma_0 = u_2->inv1_0.w * u_2->inv0_0.w / 2.0f;
        if(sigma_0 <= 0.0f)
        {

#line 72
            coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 72
        }
        else
        {



            float t_0 = clamp((d_1 * u_2->inv0_0.w + 1.5f * sigma_0) / (3.0f * sigma_0), 0.0f, 1.0f);

#line 78
            coverage_0 = 1.0f - t_0 * t_0 * (3.0f - 2.0f * t_0);

#line 72
        }

#line 68
    }
    else
    {

#line 68
        coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 68
    }

#line 86
    if(coverage_0 <= 0.0f)
    {

#line 86
        discard_fragment();

#line 86
    }



    if((u_2->flags_0.z) > 0.5f)
    {

        float coverage_1 = coverage_0 * clamp(0.5f - sdRoundedRect_0(pixel_0 - (&kernelContext_1)->u_0->clipRect_0.xy, (&kernelContext_1)->u_0->clipRect_0.zw, (&kernelContext_1)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_1 <= 0.0f)
        {

#line 94
            discard_fragment();

#line 94
        }

#line 94
        coverage_0 = coverage_1;

#line 90
    }

#line 98
    float4 _S6 = (&kernelContext_1)->u_0->colorA_0;

#line 98
    float4 srgb_0;
    if((u_2->flags_0.y) > 0.5f)
    {
        float2 axis_0 = (&kernelContext_1)->u_0->gradient_0.zw - (&kernelContext_1)->u_0->gradient_0.xy;
        float len2_0 = dot(axis_0, axis_0);

#line 102
        float t_1;
        if(len2_0 <= 0.0f)
        {

#line 103
            t_1 = 0.0f;

#line 103
        }
        else
        {

#line 103
            t_1 = clamp(dot(local_0 - (&kernelContext_1)->u_0->gradient_0.xy, axis_0) / len2_0, 0.0f, 1.0f);

#line 103
        }

#line 103
        srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(t_1) );

#line 99
    }
    else
    {

#line 99
        srgb_0 = _S6;

#line 99
    }

#line 108
    float a_0 = srgb_0.w * coverage_0;

#line 108
    pixelOutput_0 _S7 = { float4(float3(srgbToLinear_0(srgb_0.x), srgbToLinear_0(srgb_0.y), srgbToLinear_0(srgb_0.z)) * float3(a_0) , a_0) };

    return _S7;
}


#line 110
struct pixelOutput_1
{
    float4 output_2 [[color(0)]];
};



[[fragment]] pixelOutput_1 textured_fragment(float4 position_1 [[position]], DrawUniforms_0 constant* u_3 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_3 [[texture(0)]])
{

#line 117
    thread KernelContext_0 kernelContext_2;

#line 117
    (&kernelContext_2)->u_0 = u_3;

#line 117
    (&kernelContext_2)->coverageTexture_0 = coverageTexture_3;

    float2 pixel_1 = position_1.xy;

    float _S8 = pixel_1.x;

#line 121
    float _S9 = pixel_1.y;

#line 126
    float2 uv_0 = (float2(_S8 * u_3->inv0_0.x + _S9 * u_3->inv0_0.y + u_3->inv0_0.z, _S8 * u_3->inv1_0.x + _S9 * u_3->inv1_0.y + u_3->inv1_0.z) - (u_3->rect_0.xy - u_3->rect_0.zw)) / (u_3->rect_0.zw * float2(2.0f) );
    float _S10 = uv_0.x;

#line 127
    bool _S11;

#line 127
    if(_S10 < 0.0f)
    {

#line 127
        _S11 = true;

#line 127
    }
    else
    {

#line 127
        _S11 = _S10 >= 1.0f;

#line 127
    }

#line 127
    if(_S11)
    {

#line 127
        _S11 = true;

#line 127
    }
    else
    {

#line 127
        _S11 = (uv_0.y) < 0.0f;

#line 127
    }

#line 127
    if(_S11)
    {

#line 127
        _S11 = true;

#line 127
    }
    else
    {

#line 127
        _S11 = (uv_0.y) >= 1.0f;

#line 127
    }

#line 127
    if(_S11)
    {

#line 127
        discard_fragment();

#line 127
    }



    int3 _S12 = int3(min(int((&kernelContext_2)->u_0->gradient_0.x) - int(1), int(_S10 * (&kernelContext_2)->u_0->gradient_0.x)), min(int((&kernelContext_2)->u_0->gradient_0.y) - int(1), int(uv_0.y * (&kernelContext_2)->u_0->gradient_0.y)), int(0));

#line 131
    float coverage_2 = (((&kernelContext_2)->coverageTexture_0).read(vec<uint,2>(((_S12)).xy), uint(((_S12)).z)).x);
    if(coverage_2 <= 0.0f)
    {

#line 132
        discard_fragment();

#line 132
    }

#line 132
    float coverage_3;

    if(((&kernelContext_2)->u_0->flags_0.z) > 0.5f)
    {

        float coverage_4 = coverage_2 * clamp(0.5f - sdRoundedRect_0(pixel_1 - (&kernelContext_2)->u_0->clipRect_0.xy, (&kernelContext_2)->u_0->clipRect_0.zw, (&kernelContext_2)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_4 <= 0.0f)
        {

#line 138
            discard_fragment();

#line 138
        }

#line 138
        coverage_3 = coverage_4;

#line 134
    }
    else
    {

#line 134
        coverage_3 = coverage_2;

#line 134
    }

#line 141
    float a_1 = (&kernelContext_2)->u_0->colorA_0.w * coverage_3;

#line 141
    pixelOutput_1 _S13 = { float4(float3(srgbToLinear_0((&kernelContext_2)->u_0->colorA_0.x), srgbToLinear_0((&kernelContext_2)->u_0->colorA_0.y), srgbToLinear_0((&kernelContext_2)->u_0->colorA_0.z)) * float3(a_1) , a_1) };

    return _S13;
}

