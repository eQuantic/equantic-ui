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
    sampler textureSampler_0;
};


#line 56 "/Users/admin.edgar.a.mesquita/projects/equantic/equantic-ui/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
[[vertex]] vertexOutput_0 fullscreen_vertex(uint vid_0 [[vertex_id]], DrawUniforms_0 constant* u_1 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_1 [[texture(0)]], texture2d<float, access::sample> colorTexture_1 [[texture(1)]], sampler textureSampler_1 [[sampler(0)]])
{

#line 56
    thread KernelContext_0 kernelContext_0;

#line 56
    (&kernelContext_0)->u_0 = u_1;

#line 56
    (&kernelContext_0)->coverageTexture_0 = coverageTexture_1;

#line 56
    (&kernelContext_0)->colorTexture_0 = colorTexture_1;

#line 56
    (&kernelContext_0)->textureSampler_0 = textureSampler_1;


    array<float2, int(3)> positions_0 = { float2(-1.0f, -1.0f), float2(3.0f, -1.0f), float2(-1.0f, 3.0f) };

#line 59
    vertexOutput_0 _S1 = { float4(positions_0[vid_0], 0.0f, 1.0f) };
    return _S1;
}


#line 44
float sdRoundedRect_0(float2 p_0, float2 halfSize_0, float4 radii_1)
{

#line 44
    float r_0;


    if((p_0.x) >= 0.0f)
    {

#line 47
        if((p_0.y) >= 0.0f)
        {

#line 47
            r_0 = radii_1.z;

#line 47
        }
        else
        {

#line 47
            r_0 = radii_1.y;

#line 47
        }

#line 47
    }
    else
    {

#line 48
        if((p_0.y) >= 0.0f)
        {

#line 48
            r_0 = radii_1.w;

#line 48
        }
        else
        {

#line 48
            r_0 = radii_1.x;

#line 48
        }

#line 47
    }

    float2 q_0 = abs(p_0) - (halfSize_0 - float2(r_0) );


    return length(max(q_0, float2(0.0f) )) + min(max(q_0.x, q_0.y), 0.0f) - r_0;
}


#line 38
float srgbToLinear_0(float c_0)
{

#line 38
    float _S2;

    if(c_0 <= 0.04044999927282333f)
    {

#line 40
        _S2 = c_0 / 12.92000007629394531f;

#line 40
    }
    else
    {

#line 40
        _S2 = pow((c_0 + 0.05499999970197678f) / 1.0549999475479126f, 2.40000009536743164f);

#line 40
    }

#line 40
    return _S2;
}


#line 40
struct pixelOutput_0
{
    float4 output_1 [[color(0)]];
};


#line 64
[[fragment]] pixelOutput_0 sdf_fragment(float4 position_0 [[position]], DrawUniforms_0 constant* u_2 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_2 [[texture(0)]], texture2d<float, access::sample> colorTexture_2 [[texture(1)]], sampler textureSampler_2 [[sampler(0)]])
{

#line 64
    thread KernelContext_0 kernelContext_1;

#line 64
    (&kernelContext_1)->u_0 = u_2;

#line 64
    (&kernelContext_1)->coverageTexture_0 = coverageTexture_2;

#line 64
    (&kernelContext_1)->colorTexture_0 = colorTexture_2;

#line 64
    (&kernelContext_1)->textureSampler_0 = textureSampler_2;


    float2 pixel_0 = position_0.xy;

    float _S3 = pixel_0.x;

#line 69
    float _S4 = pixel_0.y;

#line 69
    float _S5 = _S3 * u_2->inv0_0.x + _S4 * u_2->inv0_0.y + u_2->inv0_0.z;
    float _S6 = _S3 * u_2->inv1_0.x + _S4 * u_2->inv1_0.y + u_2->inv1_0.z;

#line 68
    float2 local_0 = float2(_S5, _S6);



    float d_0 = sdRoundedRect_0(local_0 - u_2->rect_0.xy, u_2->rect_0.zw, u_2->radii_0);

#line 72
    bool _S7;
    if((u_2->flags_0.x) > 0.5f)
    {

#line 73
        _S7 = (u_2->flags_0.x) < 1.5f;

#line 73
    }
    else
    {

#line 73
        _S7 = false;

#line 73
    }

#line 73
    float d_1;

#line 73
    if(_S7)
    {

#line 73
        d_1 = abs(d_0) - u_2->inv1_0.w / 2.0f;

#line 73
    }
    else
    {

#line 73
        d_1 = d_0;

#line 73
    }

#line 73
    float coverage_0;



    if((u_2->flags_0.x) > 1.5f)
    {

        float sigma_0 = u_2->inv1_0.w * u_2->inv0_0.w / 2.0f;
        if(sigma_0 <= 0.0f)
        {

#line 81
            coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 81
        }
        else
        {



            float t_0 = clamp((d_1 * u_2->inv0_0.w + 1.5f * sigma_0) / (3.0f * sigma_0), 0.0f, 1.0f);

#line 87
            coverage_0 = 1.0f - t_0 * t_0 * (3.0f - 2.0f * t_0);

#line 81
        }

#line 77
    }
    else
    {

#line 77
        coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 77
    }

#line 95
    if(coverage_0 <= 0.0f)
    {

#line 95
        discard_fragment();

#line 95
    }



    if((u_2->flags_0.z) > 0.5f)
    {

        float coverage_1 = coverage_0 * clamp(0.5f - sdRoundedRect_0(pixel_0 - (&kernelContext_1)->u_0->clipRect_0.xy, (&kernelContext_1)->u_0->clipRect_0.zw, (&kernelContext_1)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_1 <= 0.0f)
        {

#line 103
            discard_fragment();

#line 103
        }

#line 103
        coverage_0 = coverage_1;

#line 99
    }

#line 108
    float4 _S8 = (&kernelContext_1)->u_0->colorA_0;

#line 108
    float dx_0;

#line 108
    float4 srgb_0;
    if((u_2->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_1)->u_0->gradient_0.z) <= 0.0f)
        {

#line 112
            dx_0 = 0.0f;

#line 112
        }
        else
        {

#line 112
            dx_0 = (_S5 - (&kernelContext_1)->u_0->gradient_0.x) / (&kernelContext_1)->u_0->gradient_0.z;

#line 112
        }

#line 112
        float dy_0;
        if(((&kernelContext_1)->u_0->gradient_0.w) <= 0.0f)
        {

#line 113
            dy_0 = 0.0f;

#line 113
        }
        else
        {

#line 113
            dy_0 = (_S6 - (&kernelContext_1)->u_0->gradient_0.y) / (&kernelContext_1)->u_0->gradient_0.w;

#line 113
        }

#line 113
        srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(clamp(sqrt(dx_0 * dx_0 + dy_0 * dy_0), 0.0f, 1.0f)) );

#line 109
    }
    else
    {

#line 117
        if((u_2->flags_0.y) > 0.5f)
        {
            float2 axis_0 = (&kernelContext_1)->u_0->gradient_0.zw - (&kernelContext_1)->u_0->gradient_0.xy;
            float len2_0 = dot(axis_0, axis_0);
            if(len2_0 <= 0.0f)
            {

#line 121
                dx_0 = 0.0f;

#line 121
            }
            else
            {

#line 121
                dx_0 = clamp(dot(local_0 - (&kernelContext_1)->u_0->gradient_0.xy, axis_0) / len2_0, 0.0f, 1.0f);

#line 121
            }

#line 121
            srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(dx_0) );

#line 117
        }
        else
        {

#line 117
            srgb_0 = _S8;

#line 117
        }

#line 109
    }

#line 126
    float a_0 = srgb_0.w * coverage_0;

#line 126
    pixelOutput_0 _S9 = { float4(float3(srgbToLinear_0(srgb_0.x), srgbToLinear_0(srgb_0.y), srgbToLinear_0(srgb_0.z)) * float3(a_0) , a_0) };

    return _S9;
}


#line 128
struct pixelOutput_1
{
    float4 output_2 [[color(0)]];
};



[[fragment]] pixelOutput_1 textured_fragment(float4 position_1 [[position]], DrawUniforms_0 constant* u_3 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_3 [[texture(0)]], texture2d<float, access::sample> colorTexture_3 [[texture(1)]], sampler textureSampler_3 [[sampler(0)]])
{

#line 135
    thread KernelContext_0 kernelContext_2;

#line 135
    (&kernelContext_2)->u_0 = u_3;

#line 135
    (&kernelContext_2)->coverageTexture_0 = coverageTexture_3;

#line 135
    (&kernelContext_2)->colorTexture_0 = colorTexture_3;

#line 135
    (&kernelContext_2)->textureSampler_0 = textureSampler_3;

    float2 pixel_1 = position_1.xy;

    float _S10 = pixel_1.x;

#line 139
    float _S11 = pixel_1.y;

#line 139
    float _S12 = _S10 * u_3->inv0_0.x + _S11 * u_3->inv0_0.y + u_3->inv0_0.z;
    float _S13 = _S10 * u_3->inv1_0.x + _S11 * u_3->inv1_0.y + u_3->inv1_0.z;

#line 138
    float2 local_1 = float2(_S12, _S13);

#line 144
    float2 uv_0 = (local_1 - (u_3->rect_0.xy - u_3->rect_0.zw)) / (u_3->rect_0.zw * float2(2.0f) );
    float _S14 = uv_0.x;

#line 145
    bool _S15;

#line 145
    if(_S14 < 0.0f)
    {

#line 145
        _S15 = true;

#line 145
    }
    else
    {

#line 145
        _S15 = _S14 >= 1.0f;

#line 145
    }

#line 145
    if(_S15)
    {

#line 145
        _S15 = true;

#line 145
    }
    else
    {

#line 145
        _S15 = (uv_0.y) < 0.0f;

#line 145
    }

#line 145
    if(_S15)
    {

#line 145
        _S15 = true;

#line 145
    }
    else
    {

#line 145
        _S15 = (uv_0.y) >= 1.0f;

#line 145
    }

#line 145
    if(_S15)
    {

#line 145
        discard_fragment();

#line 145
    }



    int3 _S16 = int3(min(int((&kernelContext_2)->u_0->gradient_0.x) - int(1), int(_S14 * (&kernelContext_2)->u_0->gradient_0.x)), min(int((&kernelContext_2)->u_0->gradient_0.y) - int(1), int(uv_0.y * (&kernelContext_2)->u_0->gradient_0.y)), int(0));

#line 149
    float coverage_2 = (((&kernelContext_2)->coverageTexture_0).read(vec<uint,2>(((_S16)).xy), uint(((_S16)).z)).x);
    if(coverage_2 <= 0.0f)
    {

#line 150
        discard_fragment();

#line 150
    }

#line 150
    float coverage_3;

    if(((&kernelContext_2)->u_0->flags_0.z) > 0.5f)
    {

        float coverage_4 = coverage_2 * clamp(0.5f - sdRoundedRect_0(pixel_1 - (&kernelContext_2)->u_0->clipRect_0.xy, (&kernelContext_2)->u_0->clipRect_0.zw, (&kernelContext_2)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_4 <= 0.0f)
        {

#line 156
            discard_fragment();

#line 156
        }

#line 156
        coverage_3 = coverage_4;

#line 152
    }
    else
    {

#line 152
        coverage_3 = coverage_2;

#line 152
    }

#line 164
    float4 _S17 = (&kernelContext_2)->u_0->colorA_0;

#line 164
    float dx_1;

#line 164
    float4 srgb_1;
    if(((&kernelContext_2)->u_0->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_2)->u_0->radii_0.z) <= 0.0f)
        {

#line 168
            dx_1 = 0.0f;

#line 168
        }
        else
        {

#line 168
            dx_1 = (_S12 - (&kernelContext_2)->u_0->radii_0.x) / (&kernelContext_2)->u_0->radii_0.z;

#line 168
        }

#line 168
        float dy_1;
        if(((&kernelContext_2)->u_0->radii_0.w) <= 0.0f)
        {

#line 169
            dy_1 = 0.0f;

#line 169
        }
        else
        {

#line 169
            dy_1 = (_S13 - (&kernelContext_2)->u_0->radii_0.y) / (&kernelContext_2)->u_0->radii_0.w;

#line 169
        }

#line 169
        srgb_1 = mix((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, float4(clamp(sqrt(dx_1 * dx_1 + dy_1 * dy_1), 0.0f, 1.0f)) );

#line 165
    }
    else
    {

#line 173
        if(((&kernelContext_2)->u_0->flags_0.y) > 0.5f)
        {
            float2 axis_1 = (&kernelContext_2)->u_0->radii_0.zw - (&kernelContext_2)->u_0->radii_0.xy;
            float len2_1 = dot(axis_1, axis_1);
            if(len2_1 <= 0.0f)
            {

#line 177
                dx_1 = 0.0f;

#line 177
            }
            else
            {

#line 177
                dx_1 = clamp(dot(local_1 - (&kernelContext_2)->u_0->radii_0.xy, axis_1) / len2_1, 0.0f, 1.0f);

#line 177
            }

#line 177
            srgb_1 = mix((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, float4(dx_1) );

#line 173
        }
        else
        {

#line 173
            srgb_1 = _S17;

#line 173
        }

#line 165
    }

#line 181
    float a_1 = srgb_1.w * coverage_3;

#line 181
    pixelOutput_1 _S18 = { float4(float3(srgbToLinear_0(srgb_1.x), srgbToLinear_0(srgb_1.y), srgbToLinear_0(srgb_1.z)) * float3(a_1) , a_1) };

    return _S18;
}


#line 183
struct pixelOutput_2
{
    float4 output_3 [[color(0)]];
};


#line 193
[[fragment]] pixelOutput_2 blur_down(float4 position_2 [[position]], DrawUniforms_0 constant* u_4 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_4 [[texture(0)]], texture2d<float, access::sample> colorTexture_4 [[texture(1)]], sampler textureSampler_4 [[sampler(0)]])
{

#line 193
    thread KernelContext_0 kernelContext_3;

#line 193
    (&kernelContext_3)->u_0 = u_4;

#line 193
    (&kernelContext_3)->coverageTexture_0 = coverageTexture_4;

#line 193
    (&kernelContext_3)->colorTexture_0 = colorTexture_4;

#line 193
    (&kernelContext_3)->textureSampler_0 = textureSampler_4;

    float2 uv_1 = position_2.xy / (u_4->rect_0.zw * float2(2.0f) );
    float2 half_0 = u_4->gradient_0.xy;

#line 201
    float2 _S19 = float2(half_0.x, - half_0.y);

#line 201
    pixelOutput_2 _S20 = { (((colorTexture_4).sample((textureSampler_4), (uv_1))) * float4(4.0f)  + ((colorTexture_4).sample((textureSampler_4), (uv_1 - half_0))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 + half_0))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 + _S19))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 - _S19)))) / float4(8.0f)  };

    return _S20;
}


#line 203
struct pixelOutput_3
{
    float4 output_4 [[color(0)]];
};


#line 207
[[fragment]] pixelOutput_3 blur_up(float4 position_3 [[position]], DrawUniforms_0 constant* u_5 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_5 [[texture(0)]], texture2d<float, access::sample> colorTexture_5 [[texture(1)]], sampler textureSampler_5 [[sampler(0)]])
{

#line 207
    thread KernelContext_0 kernelContext_4;

#line 207
    (&kernelContext_4)->u_0 = u_5;

#line 207
    (&kernelContext_4)->coverageTexture_0 = coverageTexture_5;

#line 207
    (&kernelContext_4)->colorTexture_0 = colorTexture_5;

#line 207
    (&kernelContext_4)->textureSampler_0 = textureSampler_5;

    float2 uv_2 = position_3.xy / (u_5->rect_0.zw * float2(2.0f) );
    float2 half_1 = u_5->gradient_0.xy;

    float _S21 = half_1.x;

#line 212
    float _S22 = - _S21;
    float _S23 = half_1.y;

#line 213
    float4 _S24 = float4(2.0f) ;



    float _S25 = - _S23;

#line 217
    pixelOutput_3 _S26 = { (((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S22 * 2.0f, 0.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S22, _S23)))) * _S24 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(0.0f, _S23 * 2.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S21, _S23)))) * _S24 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S21 * 2.0f, 0.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S21, _S25)))) * _S24 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(0.0f, _S25 * 2.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S22, _S25)))) * _S24) / float4(12.0f)  };


    return _S26;
}


#line 220
struct pixelOutput_4
{
    float4 output_5 [[color(0)]];
};


#line 230
[[fragment]] pixelOutput_4 layer_composite(float4 position_4 [[position]], DrawUniforms_0 constant* u_6 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_6 [[texture(0)]], texture2d<float, access::sample> colorTexture_6 [[texture(1)]], sampler textureSampler_6 [[sampler(0)]])
{

#line 230
    thread KernelContext_0 kernelContext_5;

#line 230
    (&kernelContext_5)->u_0 = u_6;

#line 230
    (&kernelContext_5)->coverageTexture_0 = coverageTexture_6;

#line 230
    (&kernelContext_5)->colorTexture_0 = colorTexture_6;

#line 230
    (&kernelContext_5)->textureSampler_0 = textureSampler_6;

    float2 pixel_2 = position_4.xy;

    float _S27 = pixel_2.x;

#line 234
    float _S28 = pixel_2.y;



    float2 uv_3 = (float2(_S27 * u_6->inv0_0.x + _S28 * u_6->inv0_0.y + u_6->inv0_0.z, _S27 * u_6->inv1_0.x + _S28 * u_6->inv1_0.y + u_6->inv1_0.z) - (u_6->rect_0.xy - u_6->rect_0.zw)) / (u_6->rect_0.zw * float2(2.0f) );
    float _S29 = uv_3.x;

#line 239
    bool _S30;

#line 239
    if(_S29 < 0.0f)
    {

#line 239
        _S30 = true;

#line 239
    }
    else
    {

#line 239
        _S30 = _S29 >= 1.0f;

#line 239
    }

#line 239
    if(_S30)
    {

#line 239
        _S30 = true;

#line 239
    }
    else
    {

#line 239
        _S30 = (uv_3.y) < 0.0f;

#line 239
    }

#line 239
    if(_S30)
    {

#line 239
        _S30 = true;

#line 239
    }
    else
    {

#line 239
        _S30 = (uv_3.y) >= 1.0f;

#line 239
    }

#line 239
    if(_S30)
    {

#line 239
        discard_fragment();

#line 239
    }



    int3 _S31 = int3(min(int((&kernelContext_5)->u_0->gradient_0.x) - int(1), int(_S29 * (&kernelContext_5)->u_0->gradient_0.x)), min(int((&kernelContext_5)->u_0->gradient_0.y) - int(1), int(uv_3.y * (&kernelContext_5)->u_0->gradient_0.y)), int(0));

#line 243
    float4 texel_0 = (((&kernelContext_5)->colorTexture_0).read(vec<uint,2>(((_S31)).xy), uint(((_S31)).z)));

    float alpha_0 = (&kernelContext_5)->u_0->colorA_0.w;

#line 245
    float alpha_1;
    if(((&kernelContext_5)->u_0->flags_0.z) > 0.5f)
    {

#line 246
        alpha_1 = alpha_0 * clamp(0.5f - sdRoundedRect_0(pixel_2 - (&kernelContext_5)->u_0->clipRect_0.xy, (&kernelContext_5)->u_0->clipRect_0.zw, (&kernelContext_5)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 246
    }
    else
    {

#line 246
        alpha_1 = alpha_0;

#line 246
    }

#line 251
    if(alpha_1 <= 0.0f)
    {

#line 251
        discard_fragment();

#line 251
    }

#line 251
    pixelOutput_4 _S32 = { texel_0 * float4(alpha_1)  };

    return _S32;
}


#line 253
struct pixelOutput_5
{
    float4 output_6 [[color(0)]];
};



[[fragment]] pixelOutput_5 textured_rgba_fragment(float4 position_5 [[position]], DrawUniforms_0 constant* u_7 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_7 [[texture(0)]], texture2d<float, access::sample> colorTexture_7 [[texture(1)]], sampler textureSampler_7 [[sampler(0)]])
{

#line 260
    thread KernelContext_0 kernelContext_6;

#line 260
    (&kernelContext_6)->u_0 = u_7;

#line 260
    (&kernelContext_6)->coverageTexture_0 = coverageTexture_7;

#line 260
    (&kernelContext_6)->colorTexture_0 = colorTexture_7;

#line 260
    (&kernelContext_6)->textureSampler_0 = textureSampler_7;

    float2 pixel_3 = position_5.xy;

    float _S33 = pixel_3.x;

#line 264
    float _S34 = pixel_3.y;



    float2 uv_4 = (float2(_S33 * u_7->inv0_0.x + _S34 * u_7->inv0_0.y + u_7->inv0_0.z, _S33 * u_7->inv1_0.x + _S34 * u_7->inv1_0.y + u_7->inv1_0.z) - (u_7->rect_0.xy - u_7->rect_0.zw)) / (u_7->rect_0.zw * float2(2.0f) );
    float _S35 = uv_4.x;

#line 269
    bool _S36;

#line 269
    if(_S35 < 0.0f)
    {

#line 269
        _S36 = true;

#line 269
    }
    else
    {

#line 269
        _S36 = _S35 >= 1.0f;

#line 269
    }

#line 269
    if(_S36)
    {

#line 269
        _S36 = true;

#line 269
    }
    else
    {

#line 269
        _S36 = (uv_4.y) < 0.0f;

#line 269
    }

#line 269
    if(_S36)
    {

#line 269
        _S36 = true;

#line 269
    }
    else
    {

#line 269
        _S36 = (uv_4.y) >= 1.0f;

#line 269
    }

#line 269
    if(_S36)
    {

#line 269
        discard_fragment();

#line 269
    }



    int3 _S37 = int3(min(int((&kernelContext_6)->u_0->gradient_0.x) - int(1), int(_S35 * (&kernelContext_6)->u_0->gradient_0.x)), min(int((&kernelContext_6)->u_0->gradient_0.y) - int(1), int(uv_4.y * (&kernelContext_6)->u_0->gradient_0.y)), int(0));

#line 273
    float4 texel_1 = (((&kernelContext_6)->colorTexture_0).read(vec<uint,2>(((_S37)).xy), uint(((_S37)).z)));

#line 273
    float coverage_5;


    if(((&kernelContext_6)->u_0->flags_0.z) > 0.5f)
    {

#line 276
        coverage_5 = clamp(0.5f - sdRoundedRect_0(pixel_3 - (&kernelContext_6)->u_0->clipRect_0.xy, (&kernelContext_6)->u_0->clipRect_0.zw, (&kernelContext_6)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 276
    }
    else
    {

#line 276
        coverage_5 = 1.0f;

#line 276
    }

#line 282
    float a_2 = texel_1.w * coverage_5;
    if(a_2 <= 0.0f)
    {

#line 283
        discard_fragment();

#line 283
    }

#line 283
    pixelOutput_5 _S38 = { float4(texel_1.xyz * float3(a_2) , a_2) };
    return _S38;
}

