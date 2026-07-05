#include <metal_stdlib>
#include <metal_math>
#include <metal_texture>
using namespace metal;

#line 90 "core"
struct vertexOutput_0
{
    float4 output_0 [[position]];
};


#line 8 "src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
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


#line 43
[[vertex]] vertexOutput_0 fullscreen_vertex(uint vid_0 [[vertex_id]], DrawUniforms_0 constant* u_0 [[buffer(0)]])
{

    array<float2, int(3)> positions_0 = { float2(-1.0f, -1.0f), float2(3.0f, -1.0f), float2(-1.0f, 3.0f) };

#line 46
    vertexOutput_0 _S1 = { float4(positions_0[vid_0], 0.0f, 1.0f) };
    return _S1;
}


#line 31
float sdRoundedRect_0(float2 p_0, float2 halfSize_0, float4 radii_1)
{

#line 31
    float r_0;


    if((p_0.x) >= 0.0f)
    {

#line 34
        if((p_0.y) >= 0.0f)
        {

#line 34
            r_0 = radii_1.z;

#line 34
        }
        else
        {

#line 34
            r_0 = radii_1.y;

#line 34
        }

#line 34
    }
    else
    {

#line 35
        if((p_0.y) >= 0.0f)
        {

#line 35
            r_0 = radii_1.w;

#line 35
        }
        else
        {

#line 35
            r_0 = radii_1.x;

#line 35
        }

#line 34
    }

    float2 q_0 = abs(p_0) - (halfSize_0 - float2(r_0) );


    return length(max(q_0, float2(0.0f) )) + min(max(q_0.x, q_0.y), 0.0f) - r_0;
}


#line 25
float srgbToLinear_0(float c_0)
{

#line 25
    float _S2;

    if(c_0 <= 0.04044999927282333f)
    {

#line 27
        _S2 = c_0 / 12.92000007629394531f;

#line 27
    }
    else
    {

#line 27
        _S2 = pow((c_0 + 0.05499999970197678f) / 1.0549999475479126f, 2.40000009536743164f);

#line 27
    }

#line 27
    return _S2;
}


#line 27
struct pixelOutput_0
{
    float4 output_1 [[color(0)]];
};


#line 27
struct KernelContext_0
{
    DrawUniforms_0 constant* u_1;
};


#line 51
[[fragment]] pixelOutput_0 sdf_fragment(float4 position_0 [[position]], DrawUniforms_0 constant* u_2 [[buffer(0)]])
{

#line 51
    thread KernelContext_0 kernelContext_0;

#line 51
    (&kernelContext_0)->u_1 = u_2;


    float2 pixel_0 = position_0.xy;

    float _S3 = pixel_0.x;

#line 56
    float _S4 = pixel_0.y;

#line 55
    float2 local_0 = float2(_S3 * u_2->inv0_0.x + _S4 * u_2->inv0_0.y + u_2->inv0_0.z, _S3 * u_2->inv1_0.x + _S4 * u_2->inv1_0.y + u_2->inv1_0.z);



    float d_0 = sdRoundedRect_0(local_0 - u_2->rect_0.xy, u_2->rect_0.zw, u_2->radii_0);

#line 59
    float d_1;
    if((u_2->flags_0.x) > 0.5f)
    {

#line 60
        d_1 = abs(d_0) - u_2->inv1_0.w / 2.0f;

#line 60
    }
    else
    {

#line 60
        d_1 = d_0;

#line 60
    }


    float coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);
    if(coverage_0 <= 0.0f)
    {

#line 64
        discard_fragment();

#line 64
    }

#line 64
    float coverage_1;



    if((u_2->flags_0.z) > 0.5f)
    {

        float coverage_2 = coverage_0 * clamp(0.5f - sdRoundedRect_0(pixel_0 - (&kernelContext_0)->u_1->clipRect_0.xy, (&kernelContext_0)->u_1->clipRect_0.zw, (&kernelContext_0)->u_1->clipRadii_0), 0.0f, 1.0f);
        if(coverage_2 <= 0.0f)
        {

#line 72
            discard_fragment();

#line 72
        }

#line 72
        coverage_1 = coverage_2;

#line 68
    }
    else
    {

#line 68
        coverage_1 = coverage_0;

#line 68
    }

#line 76
    float4 _S5 = (&kernelContext_0)->u_1->colorA_0;

#line 76
    float4 srgb_0;
    if((u_2->flags_0.y) > 0.5f)
    {
        float2 axis_0 = (&kernelContext_0)->u_1->gradient_0.zw - (&kernelContext_0)->u_1->gradient_0.xy;
        float len2_0 = dot(axis_0, axis_0);

#line 80
        float t_0;
        if(len2_0 <= 0.0f)
        {

#line 81
            t_0 = 0.0f;

#line 81
        }
        else
        {

#line 81
            t_0 = clamp(dot(local_0 - (&kernelContext_0)->u_1->gradient_0.xy, axis_0) / len2_0, 0.0f, 1.0f);

#line 81
        }

#line 81
        srgb_0 = mix((&kernelContext_0)->u_1->colorA_0, (&kernelContext_0)->u_1->colorB_0, float4(t_0) );

#line 77
    }
    else
    {

#line 77
        srgb_0 = _S5;

#line 77
    }

#line 86
    float a_0 = srgb_0.w * coverage_1;

#line 86
    pixelOutput_0 _S6 = { float4(float3(srgbToLinear_0(srgb_0.x), srgbToLinear_0(srgb_0.y), srgbToLinear_0(srgb_0.z)) * float3(a_0) , a_0) };

    return _S6;
}

