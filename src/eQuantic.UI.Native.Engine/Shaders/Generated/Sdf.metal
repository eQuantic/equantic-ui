#include <metal_stdlib>
#include <metal_math>
#include <metal_texture>
using namespace metal;

#line 90 "core"
struct vertexOutput_0
{
    float4 output_0 [[position]];
};


#line 8 "Sdf.slang"
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


#line 108 "Sdf.slang"
[[vertex]] vertexOutput_0 fullscreen_vertex(uint vid_0 [[vertex_id]], DrawUniforms_0 constant* u_1 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_1 [[texture(0)]], texture2d<float, access::sample> colorTexture_1 [[texture(1)]], sampler textureSampler_1 [[sampler(0)]])
{

#line 108
    thread KernelContext_0 kernelContext_0;

#line 108
    (&kernelContext_0)->u_0 = u_1;

#line 108
    (&kernelContext_0)->coverageTexture_0 = coverageTexture_1;

#line 108
    (&kernelContext_0)->colorTexture_0 = colorTexture_1;

#line 108
    (&kernelContext_0)->textureSampler_0 = textureSampler_1;


    array<float2, int(3)> positions_0 = { float2(-1.0f, -1.0f), float2(3.0f, -1.0f), float2(-1.0f, 3.0f) };

#line 111
    vertexOutput_0 _S1 = { float4(positions_0[vid_0], 0.0f, 1.0f) };
    return _S1;
}


#line 70
float sdAnnularSector_0(float2 p_0, float innerRadius_0, float outerRadius_0, float startAngle_0, float endAngle_0, float rounding_0)
{

    float rIn_0 = innerRadius_0 + rounding_0;
    float rOut_0 = outerRadius_0 - rounding_0;
    float mid_0 = (startAngle_0 + endAngle_0) / 2.0f;
    float half_0 = (endAngle_0 - startAngle_0) / 2.0f;

    float cosMid_0 = cos(mid_0);
    float sinMid_0 = sin(mid_0);
    float _S2 = p_0.x;

#line 80
    float _S3 = p_0.y;

#line 80
    float qx_0 = cosMid_0 * _S2 + sinMid_0 * _S3;
    float qy_0 = abs(- sinMid_0 * _S2 + cosMid_0 * _S3);
    float r_0 = sqrt(qx_0 * qx_0 + qy_0 * qy_0);

    float sinHalf_0 = sin(half_0);
    float cosHalf_0 = cos(half_0);
    float w_0 = qx_0 * sinHalf_0 - qy_0 * cosHalf_0;

#line 86
    float d_0;


    if(w_0 >= rounding_0)
    {

#line 89
        d_0 = max(max(rIn_0 - r_0, r_0 - rOut_0), rounding_0 - w_0);

#line 89
    }
    else
    {

#line 96
        float _S4 = rounding_0 * rounding_0;

#line 95
        float s_0 = clamp(qx_0 * cosHalf_0 + qy_0 * sinHalf_0, sqrt(max(rIn_0 * rIn_0 - _S4, 0.0f)), sqrt(max(rOut_0 * rOut_0 - _S4, 0.0f)));

#line 100
        float dx_0 = qx_0 - (s_0 * cosHalf_0 + rounding_0 * sinHalf_0);
        float dy_0 = qy_0 - (s_0 * sinHalf_0 - rounding_0 * cosHalf_0);

#line 101
        d_0 = sqrt(dx_0 * dx_0 + dy_0 * dy_0);

#line 89
    }

#line 104
    return d_0 - rounding_0;
}


#line 54
float sdRoundedRect_0(float2 p_1, float2 halfSize_0, float4 radii_1)
{

#line 54
    float r_1;


    if((p_1.x) >= 0.0f)
    {

#line 57
        if((p_1.y) >= 0.0f)
        {

#line 57
            r_1 = radii_1.z;

#line 57
        }
        else
        {

#line 57
            r_1 = radii_1.y;

#line 57
        }

#line 57
    }
    else
    {

#line 58
        if((p_1.y) >= 0.0f)
        {

#line 58
            r_1 = radii_1.w;

#line 58
        }
        else
        {

#line 58
            r_1 = radii_1.x;

#line 58
        }

#line 57
    }

    float2 q_0 = abs(p_1) - (halfSize_0 - float2(r_1) );


    return length(max(q_0, float2(0.0f) )) + min(max(q_0.x, q_0.y), 0.0f) - r_1;
}


#line 46
float4 gradientColor_0(float4 from_0, float4 to_0, float t_0, float premultiplied_0)
{
    if(premultiplied_0 < 0.5f)
    {

#line 48
        return mix(from_0, to_0, float4(t_0) );
    }

#line 49
    float _S5 = from_0.w;

#line 49
    float _S6 = to_0.w;

#line 49
    float4 mixed_0 = mix(float4(from_0.xyz * float3(_S5) , _S5), float4(to_0.xyz * float3(_S6) , _S6), float4(t_0) );
    float _S7 = mixed_0.w;

#line 50
    float4 _S8;

#line 50
    if(_S7 > 0.0f)
    {

#line 50
        _S8 = float4(mixed_0.xyz / float3(_S7) , _S7);

#line 50
    }
    else
    {

#line 50
        _S8 = float4(0.0f, 0.0f, 0.0f, 0.0f);

#line 50
    }

#line 50
    return _S8;
}


#line 38
float srgbToLinear_0(float c_0)
{

#line 38
    float _S9;

    if(c_0 <= 0.04044999927282333f)
    {

#line 40
        _S9 = c_0 / 12.92000007629394531f;

#line 40
    }
    else
    {

#line 40
        _S9 = pow((c_0 + 0.05499999970197678f) / 1.0549999475479126f, 2.40000009536743164f);

#line 40
    }

#line 40
    return _S9;
}


#line 40
struct pixelOutput_0
{
    float4 output_1 [[color(0)]];
};


#line 116
[[fragment]] pixelOutput_0 sdf_fragment(float4 position_0 [[position]], DrawUniforms_0 constant* u_2 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_2 [[texture(0)]], texture2d<float, access::sample> colorTexture_2 [[texture(1)]], sampler textureSampler_2 [[sampler(0)]])
{

#line 116
    thread KernelContext_0 kernelContext_1;

#line 116
    (&kernelContext_1)->u_0 = u_2;

#line 116
    (&kernelContext_1)->coverageTexture_0 = coverageTexture_2;

#line 116
    (&kernelContext_1)->colorTexture_0 = colorTexture_2;

#line 116
    (&kernelContext_1)->textureSampler_0 = textureSampler_2;


    float2 pixel_0 = position_0.xy;

    float _S10 = pixel_0.x;

#line 121
    float _S11 = pixel_0.y;

#line 121
    float _S12 = _S10 * u_2->inv0_0.x + _S11 * u_2->inv0_0.y + u_2->inv0_0.z;
    float _S13 = _S10 * u_2->inv1_0.x + _S11 * u_2->inv1_0.y + u_2->inv1_0.z;

#line 120
    float2 local_0 = float2(_S12, _S13);

#line 120
    bool _S14;

#line 126
    if((u_2->flags_0.x) > 2.5f)
    {

#line 126
        _S14 = (u_2->flags_0.x) < 3.5f;

#line 126
    }
    else
    {

#line 126
        _S14 = false;

#line 126
    }

#line 126
    float d_1;

#line 126
    if(_S14)
    {

#line 126
        d_1 = sdAnnularSector_0(local_0 - (&kernelContext_1)->u_0->rect_0.xy, (&kernelContext_1)->u_0->radii_0.x, (&kernelContext_1)->u_0->rect_0.z, (&kernelContext_1)->u_0->radii_0.y, (&kernelContext_1)->u_0->radii_0.z, (&kernelContext_1)->u_0->radii_0.w);

#line 126
    }
    else
    {



        float d_2 = sdRoundedRect_0(local_0 - (&kernelContext_1)->u_0->rect_0.xy, (&kernelContext_1)->u_0->rect_0.zw, (&kernelContext_1)->u_0->radii_0);
        if((u_2->flags_0.x) > 0.5f)
        {

#line 133
            _S14 = (u_2->flags_0.x) < 1.5f;

#line 133
        }
        else
        {

#line 133
            _S14 = false;

#line 133
        }

#line 133
        if(_S14)
        {

#line 133
            d_1 = abs(d_2) - u_2->inv1_0.w / 2.0f;

#line 133
        }
        else
        {

#line 133
            d_1 = d_2;

#line 133
        }

#line 126
    }

#line 138
    if((u_2->flags_0.x) > 1.5f)
    {

#line 138
        _S14 = (u_2->flags_0.x) < 2.5f;

#line 138
    }
    else
    {

#line 138
        _S14 = false;

#line 138
    }

#line 138
    float coverage_0;

#line 138
    if(_S14)
    {

        float sigma_0 = u_2->inv1_0.w * u_2->inv0_0.w / 2.0f;
        if(sigma_0 <= 0.0f)
        {

#line 142
            coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 142
        }
        else
        {



            float t_1 = clamp((d_1 * u_2->inv0_0.w + 1.5f * sigma_0) / (3.0f * sigma_0), 0.0f, 1.0f);

#line 148
            coverage_0 = 1.0f - t_1 * t_1 * (3.0f - 2.0f * t_1);

#line 142
        }

#line 138
    }
    else
    {

#line 138
        coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 138
    }

#line 156
    if(coverage_0 <= 0.0f)
    {

#line 156
        discard_fragment();

#line 156
    }



    if((u_2->flags_0.z) > 0.5f)
    {

        float coverage_1 = coverage_0 * clamp(0.5f - sdRoundedRect_0(pixel_0 - (&kernelContext_1)->u_0->clipRect_0.xy, (&kernelContext_1)->u_0->clipRect_0.zw, (&kernelContext_1)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_1 <= 0.0f)
        {

#line 164
            discard_fragment();

#line 164
        }

#line 164
        coverage_0 = coverage_1;

#line 160
    }

#line 169
    float4 _S15 = (&kernelContext_1)->u_0->colorA_0;

#line 169
    float dx_1;

#line 169
    float4 srgb_0;
    if((u_2->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_1)->u_0->gradient_0.z) <= 0.0f)
        {

#line 173
            dx_1 = 0.0f;

#line 173
        }
        else
        {

#line 173
            dx_1 = (_S12 - (&kernelContext_1)->u_0->gradient_0.x) / (&kernelContext_1)->u_0->gradient_0.z;

#line 173
        }

#line 173
        float dy_1;
        if(((&kernelContext_1)->u_0->gradient_0.w) <= 0.0f)
        {

#line 174
            dy_1 = 0.0f;

#line 174
        }
        else
        {

#line 174
            dy_1 = (_S13 - (&kernelContext_1)->u_0->gradient_0.y) / (&kernelContext_1)->u_0->gradient_0.w;

#line 174
        }

#line 174
        srgb_0 = gradientColor_0((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, clamp(sqrt(dx_1 * dx_1 + dy_1 * dy_1), 0.0f, 1.0f), u_2->flags_0.w);

#line 170
    }
    else
    {

#line 178
        if((u_2->flags_0.y) > 0.5f)
        {
            float2 axis_0 = (&kernelContext_1)->u_0->gradient_0.zw - (&kernelContext_1)->u_0->gradient_0.xy;
            float len2_0 = dot(axis_0, axis_0);
            if(len2_0 <= 0.0f)
            {

#line 182
                dx_1 = 0.0f;

#line 182
            }
            else
            {

#line 182
                dx_1 = clamp(dot(local_0 - (&kernelContext_1)->u_0->gradient_0.xy, axis_0) / len2_0, 0.0f, 1.0f);

#line 182
            }

#line 182
            srgb_0 = gradientColor_0((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, dx_1, u_2->flags_0.w);

#line 178
        }
        else
        {

#line 178
            srgb_0 = _S15;

#line 178
        }

#line 170
    }

#line 187
    float a_0 = srgb_0.w * coverage_0;

#line 187
    pixelOutput_0 _S16 = { float4(float3(srgbToLinear_0(srgb_0.x), srgbToLinear_0(srgb_0.y), srgbToLinear_0(srgb_0.z)) * float3(a_0) , a_0) };

    return _S16;
}


#line 189
struct pixelOutput_1
{
    float4 output_2 [[color(0)]];
};



[[fragment]] pixelOutput_1 textured_fragment(float4 position_1 [[position]], DrawUniforms_0 constant* u_3 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_3 [[texture(0)]], texture2d<float, access::sample> colorTexture_3 [[texture(1)]], sampler textureSampler_3 [[sampler(0)]])
{

#line 196
    thread KernelContext_0 kernelContext_2;

#line 196
    (&kernelContext_2)->u_0 = u_3;

#line 196
    (&kernelContext_2)->coverageTexture_0 = coverageTexture_3;

#line 196
    (&kernelContext_2)->colorTexture_0 = colorTexture_3;

#line 196
    (&kernelContext_2)->textureSampler_0 = textureSampler_3;

    float2 pixel_1 = position_1.xy;

    float _S17 = pixel_1.x;

#line 200
    float _S18 = pixel_1.y;

#line 200
    float _S19 = _S17 * u_3->inv0_0.x + _S18 * u_3->inv0_0.y + u_3->inv0_0.z;
    float _S20 = _S17 * u_3->inv1_0.x + _S18 * u_3->inv1_0.y + u_3->inv1_0.z;

#line 199
    float2 local_1 = float2(_S19, _S20);

#line 205
    float2 uv_0 = (local_1 - (u_3->rect_0.xy - u_3->rect_0.zw)) / (u_3->rect_0.zw * float2(2.0f) );
    float _S21 = uv_0.x;

#line 206
    bool _S22;

#line 206
    if(_S21 < 0.0f)
    {

#line 206
        _S22 = true;

#line 206
    }
    else
    {

#line 206
        _S22 = _S21 >= 1.0f;

#line 206
    }

#line 206
    if(_S22)
    {

#line 206
        _S22 = true;

#line 206
    }
    else
    {

#line 206
        _S22 = (uv_0.y) < 0.0f;

#line 206
    }

#line 206
    if(_S22)
    {

#line 206
        _S22 = true;

#line 206
    }
    else
    {

#line 206
        _S22 = (uv_0.y) >= 1.0f;

#line 206
    }

#line 206
    if(_S22)
    {

#line 206
        discard_fragment();

#line 206
    }



    int3 _S23 = int3(min(int((&kernelContext_2)->u_0->gradient_0.x) - int(1), int(_S21 * (&kernelContext_2)->u_0->gradient_0.x)), min(int((&kernelContext_2)->u_0->gradient_0.y) - int(1), int(uv_0.y * (&kernelContext_2)->u_0->gradient_0.y)), int(0));

#line 210
    float coverage_2 = (((&kernelContext_2)->coverageTexture_0).read(vec<uint,2>(((_S23)).xy), uint(((_S23)).z)).x);
    if(coverage_2 <= 0.0f)
    {

#line 211
        discard_fragment();

#line 211
    }

#line 211
    float coverage_3;

    if(((&kernelContext_2)->u_0->flags_0.z) > 0.5f)
    {

        float coverage_4 = coverage_2 * clamp(0.5f - sdRoundedRect_0(pixel_1 - (&kernelContext_2)->u_0->clipRect_0.xy, (&kernelContext_2)->u_0->clipRect_0.zw, (&kernelContext_2)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_4 <= 0.0f)
        {

#line 217
            discard_fragment();

#line 217
        }

#line 217
        coverage_3 = coverage_4;

#line 213
    }
    else
    {

#line 213
        coverage_3 = coverage_2;

#line 213
    }

#line 225
    float4 _S24 = (&kernelContext_2)->u_0->colorA_0;

#line 225
    float dx_2;

#line 225
    float4 srgb_1;
    if(((&kernelContext_2)->u_0->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_2)->u_0->radii_0.z) <= 0.0f)
        {

#line 229
            dx_2 = 0.0f;

#line 229
        }
        else
        {

#line 229
            dx_2 = (_S19 - (&kernelContext_2)->u_0->radii_0.x) / (&kernelContext_2)->u_0->radii_0.z;

#line 229
        }

#line 229
        float dy_2;
        if(((&kernelContext_2)->u_0->radii_0.w) <= 0.0f)
        {

#line 230
            dy_2 = 0.0f;

#line 230
        }
        else
        {

#line 230
            dy_2 = (_S20 - (&kernelContext_2)->u_0->radii_0.y) / (&kernelContext_2)->u_0->radii_0.w;

#line 230
        }

#line 230
        srgb_1 = gradientColor_0((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, clamp(sqrt(dx_2 * dx_2 + dy_2 * dy_2), 0.0f, 1.0f), (&kernelContext_2)->u_0->flags_0.w);

#line 226
    }
    else
    {

#line 234
        if(((&kernelContext_2)->u_0->flags_0.y) > 0.5f)
        {
            float2 axis_1 = (&kernelContext_2)->u_0->radii_0.zw - (&kernelContext_2)->u_0->radii_0.xy;
            float len2_1 = dot(axis_1, axis_1);
            if(len2_1 <= 0.0f)
            {

#line 238
                dx_2 = 0.0f;

#line 238
            }
            else
            {

#line 238
                dx_2 = clamp(dot(local_1 - (&kernelContext_2)->u_0->radii_0.xy, axis_1) / len2_1, 0.0f, 1.0f);

#line 238
            }

#line 238
            srgb_1 = gradientColor_0((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, dx_2, (&kernelContext_2)->u_0->flags_0.w);

#line 234
        }
        else
        {

#line 234
            srgb_1 = _S24;

#line 234
        }

#line 226
    }

#line 242
    float a_1 = srgb_1.w * coverage_3;

#line 242
    pixelOutput_1 _S25 = { float4(float3(srgbToLinear_0(srgb_1.x), srgbToLinear_0(srgb_1.y), srgbToLinear_0(srgb_1.z)) * float3(a_1) , a_1) };

    return _S25;
}


#line 244
struct pixelOutput_2
{
    float4 output_3 [[color(0)]];
};


#line 254
[[fragment]] pixelOutput_2 blur_down(float4 position_2 [[position]], DrawUniforms_0 constant* u_4 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_4 [[texture(0)]], texture2d<float, access::sample> colorTexture_4 [[texture(1)]], sampler textureSampler_4 [[sampler(0)]])
{

#line 254
    thread KernelContext_0 kernelContext_3;

#line 254
    (&kernelContext_3)->u_0 = u_4;

#line 254
    (&kernelContext_3)->coverageTexture_0 = coverageTexture_4;

#line 254
    (&kernelContext_3)->colorTexture_0 = colorTexture_4;

#line 254
    (&kernelContext_3)->textureSampler_0 = textureSampler_4;

    float2 uv_1 = position_2.xy / (u_4->rect_0.zw * float2(2.0f) );
    float2 half_1 = u_4->gradient_0.xy;

#line 262
    float2 _S26 = float2(half_1.x, - half_1.y);

#line 262
    pixelOutput_2 _S27 = { (((colorTexture_4).sample((textureSampler_4), (uv_1))) * float4(4.0f)  + ((colorTexture_4).sample((textureSampler_4), (uv_1 - half_1))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 + half_1))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 + _S26))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 - _S26)))) / float4(8.0f)  };

    return _S27;
}


#line 264
struct pixelOutput_3
{
    float4 output_4 [[color(0)]];
};


#line 268
[[fragment]] pixelOutput_3 blur_up(float4 position_3 [[position]], DrawUniforms_0 constant* u_5 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_5 [[texture(0)]], texture2d<float, access::sample> colorTexture_5 [[texture(1)]], sampler textureSampler_5 [[sampler(0)]])
{

#line 268
    thread KernelContext_0 kernelContext_4;

#line 268
    (&kernelContext_4)->u_0 = u_5;

#line 268
    (&kernelContext_4)->coverageTexture_0 = coverageTexture_5;

#line 268
    (&kernelContext_4)->colorTexture_0 = colorTexture_5;

#line 268
    (&kernelContext_4)->textureSampler_0 = textureSampler_5;

    float2 uv_2 = position_3.xy / (u_5->rect_0.zw * float2(2.0f) );
    float2 half_2 = u_5->gradient_0.xy;

    float _S28 = half_2.x;

#line 273
    float _S29 = - _S28;
    float _S30 = half_2.y;

#line 274
    float4 _S31 = float4(2.0f) ;



    float _S32 = - _S30;

#line 278
    pixelOutput_3 _S33 = { (((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S29 * 2.0f, 0.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S29, _S30)))) * _S31 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(0.0f, _S30 * 2.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S28, _S30)))) * _S31 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S28 * 2.0f, 0.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S28, _S32)))) * _S31 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(0.0f, _S32 * 2.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S29, _S32)))) * _S31) / float4(12.0f)  };


    return _S33;
}


#line 281
struct pixelOutput_4
{
    float4 output_5 [[color(0)]];
};


#line 291
[[fragment]] pixelOutput_4 layer_composite(float4 position_4 [[position]], DrawUniforms_0 constant* u_6 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_6 [[texture(0)]], texture2d<float, access::sample> colorTexture_6 [[texture(1)]], sampler textureSampler_6 [[sampler(0)]])
{

#line 291
    thread KernelContext_0 kernelContext_5;

#line 291
    (&kernelContext_5)->u_0 = u_6;

#line 291
    (&kernelContext_5)->coverageTexture_0 = coverageTexture_6;

#line 291
    (&kernelContext_5)->colorTexture_0 = colorTexture_6;

#line 291
    (&kernelContext_5)->textureSampler_0 = textureSampler_6;

    float2 pixel_2 = position_4.xy;

    float _S34 = pixel_2.x;

#line 295
    float _S35 = pixel_2.y;



    float2 uv_3 = (float2(_S34 * u_6->inv0_0.x + _S35 * u_6->inv0_0.y + u_6->inv0_0.z, _S34 * u_6->inv1_0.x + _S35 * u_6->inv1_0.y + u_6->inv1_0.z) - (u_6->rect_0.xy - u_6->rect_0.zw)) / (u_6->rect_0.zw * float2(2.0f) );
    float _S36 = uv_3.x;

#line 300
    bool _S37;

#line 300
    if(_S36 < 0.0f)
    {

#line 300
        _S37 = true;

#line 300
    }
    else
    {

#line 300
        _S37 = _S36 >= 1.0f;

#line 300
    }

#line 300
    if(_S37)
    {

#line 300
        _S37 = true;

#line 300
    }
    else
    {

#line 300
        _S37 = (uv_3.y) < 0.0f;

#line 300
    }

#line 300
    if(_S37)
    {

#line 300
        _S37 = true;

#line 300
    }
    else
    {

#line 300
        _S37 = (uv_3.y) >= 1.0f;

#line 300
    }

#line 300
    if(_S37)
    {

#line 300
        discard_fragment();

#line 300
    }



    int3 _S38 = int3(min(int((&kernelContext_5)->u_0->gradient_0.x) - int(1), int(_S36 * (&kernelContext_5)->u_0->gradient_0.x)), min(int((&kernelContext_5)->u_0->gradient_0.y) - int(1), int(uv_3.y * (&kernelContext_5)->u_0->gradient_0.y)), int(0));

#line 304
    float4 texel_0 = (((&kernelContext_5)->colorTexture_0).read(vec<uint,2>(((_S38)).xy), uint(((_S38)).z)));

    float alpha_0 = (&kernelContext_5)->u_0->colorA_0.w;

#line 306
    float alpha_1;
    if(((&kernelContext_5)->u_0->flags_0.z) > 0.5f)
    {

#line 307
        alpha_1 = alpha_0 * clamp(0.5f - sdRoundedRect_0(pixel_2 - (&kernelContext_5)->u_0->clipRect_0.xy, (&kernelContext_5)->u_0->clipRect_0.zw, (&kernelContext_5)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 307
    }
    else
    {

#line 307
        alpha_1 = alpha_0;

#line 307
    }

#line 312
    if(alpha_1 <= 0.0f)
    {

#line 312
        discard_fragment();

#line 312
    }

#line 312
    pixelOutput_4 _S39 = { texel_0 * float4(alpha_1)  };

    return _S39;
}


#line 314
struct pixelOutput_5
{
    float4 output_6 [[color(0)]];
};



[[fragment]] pixelOutput_5 textured_rgba_fragment(float4 position_5 [[position]], DrawUniforms_0 constant* u_7 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_7 [[texture(0)]], texture2d<float, access::sample> colorTexture_7 [[texture(1)]], sampler textureSampler_7 [[sampler(0)]])
{

#line 321
    thread KernelContext_0 kernelContext_6;

#line 321
    (&kernelContext_6)->u_0 = u_7;

#line 321
    (&kernelContext_6)->coverageTexture_0 = coverageTexture_7;

#line 321
    (&kernelContext_6)->colorTexture_0 = colorTexture_7;

#line 321
    (&kernelContext_6)->textureSampler_0 = textureSampler_7;

    float2 pixel_3 = position_5.xy;

    float _S40 = pixel_3.x;

#line 325
    float _S41 = pixel_3.y;



    float2 uv_4 = (float2(_S40 * u_7->inv0_0.x + _S41 * u_7->inv0_0.y + u_7->inv0_0.z, _S40 * u_7->inv1_0.x + _S41 * u_7->inv1_0.y + u_7->inv1_0.z) - (u_7->rect_0.xy - u_7->rect_0.zw)) / (u_7->rect_0.zw * float2(2.0f) );
    float _S42 = uv_4.x;

#line 330
    bool _S43;

#line 330
    if(_S42 < 0.0f)
    {

#line 330
        _S43 = true;

#line 330
    }
    else
    {

#line 330
        _S43 = _S42 >= 1.0f;

#line 330
    }

#line 330
    if(_S43)
    {

#line 330
        _S43 = true;

#line 330
    }
    else
    {

#line 330
        _S43 = (uv_4.y) < 0.0f;

#line 330
    }

#line 330
    if(_S43)
    {

#line 330
        _S43 = true;

#line 330
    }
    else
    {

#line 330
        _S43 = (uv_4.y) >= 1.0f;

#line 330
    }

#line 330
    if(_S43)
    {

#line 330
        discard_fragment();

#line 330
    }



    int3 _S44 = int3(min(int((&kernelContext_6)->u_0->gradient_0.x) - int(1), int(_S42 * (&kernelContext_6)->u_0->gradient_0.x)), min(int((&kernelContext_6)->u_0->gradient_0.y) - int(1), int(uv_4.y * (&kernelContext_6)->u_0->gradient_0.y)), int(0));

#line 334
    float4 texel_1 = (((&kernelContext_6)->colorTexture_0).read(vec<uint,2>(((_S44)).xy), uint(((_S44)).z)));

#line 334
    float coverage_5;


    if(((&kernelContext_6)->u_0->flags_0.z) > 0.5f)
    {

#line 337
        coverage_5 = clamp(0.5f - sdRoundedRect_0(pixel_3 - (&kernelContext_6)->u_0->clipRect_0.xy, (&kernelContext_6)->u_0->clipRect_0.zw, (&kernelContext_6)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 337
    }
    else
    {

#line 337
        coverage_5 = 1.0f;

#line 337
    }

#line 343
    float a_2 = texel_1.w * coverage_5;
    if(a_2 <= 0.0f)
    {

#line 344
        discard_fragment();

#line 344
    }

#line 344
    pixelOutput_5 _S45 = { float4(texel_1.xyz * float3(a_2) , a_2) };
    return _S45;
}


#line 352
float tileTexel_0(float offset_0, float period_0, float extent_0, float repeats_0, float size_0)
{

#line 352
    bool _S46;

    if(!(period_0 > 0.0f))
    {

#line 354
        _S46 = true;

#line 354
    }
    else
    {

#line 354
        _S46 = !(extent_0 > 0.0f);

#line 354
    }

#line 354
    if(_S46)
    {

#line 354
        _S46 = true;

#line 354
    }
    else
    {

#line 354
        _S46 = size_0 <= 0.0f;

#line 354
    }

#line 354
    if(_S46)
    {

#line 354
        return -1.0f;
    }

#line 355
    float cell_0 = floor(offset_0 / period_0);
    float within_0 = offset_0 - cell_0 * period_0;

#line 356
    float cell_1;

#line 356
    float within_1;


    if(within_0 < 0.0f)
    {

#line 359
        float within_2 = within_0 + period_0;

#line 359
        cell_1 = cell_0 - 1.0f;

#line 359
        within_1 = within_2;

#line 359
    }
    else
    {

#line 360
        if(within_0 >= period_0)
        {

#line 360
            float within_3 = within_0 - period_0;

#line 360
            cell_1 = cell_0 + 1.0f;

#line 360
            within_1 = within_3;

#line 360
        }
        else
        {

#line 360
            cell_1 = cell_0;

#line 360
            within_1 = within_0;

#line 360
        }

#line 359
    }

    if(repeats_0 < 0.5f)
    {

#line 361
        _S46 = cell_1 != 0.0f;

#line 361
    }
    else
    {

#line 361
        _S46 = false;

#line 361
    }

#line 361
    if(_S46)
    {

#line 361
        return -1.0f;
    }

#line 362
    if(within_1 >= extent_0)
    {

#line 362
        _S46 = extent_0 < period_0;

#line 362
    }
    else
    {

#line 362
        _S46 = false;

#line 362
    }

#line 362
    if(_S46)
    {

#line 362
        return -1.0f;
    }

#line 363
    return min(within_1 / extent_0, 1.0f) * size_0;
}


int tileIndex_0(int index_0, int size_1, float wraps_0)
{

#line 367
    int _S47;

    if(wraps_0 > 0.5f)
    {

#line 369
        int _S48 = index_0 % size_1;

#line 369
        int _S49 = (_S48 + size_1) % size_1;

#line 369
        _S47 = _S49;

#line 369
    }
    else
    {

#line 369
        _S47 = clamp(index_0, int(0), size_1 - int(1));

#line 369
    }

#line 369
    return _S47;
}


float4 tileTexelPremultiplied_0(int x_0, int y_0, KernelContext_0 thread* kernelContext_7)
{
    int3 _S50 = int3(x_0, y_0, int(0));

#line 375
    float4 texel_2 = ((kernelContext_7->colorTexture_0).read(vec<uint,2>(((_S50)).xy), uint(((_S50)).z)));
    float _S51 = texel_2.w;

#line 376
    return float4(texel_2.xyz * float3(_S51) , _S51);
}


#line 376
struct pixelOutput_6
{
    float4 output_7 [[color(0)]];
};


#line 387
[[fragment]] pixelOutput_6 tiled_rgba_fragment(float4 position_6 [[position]], DrawUniforms_0 constant* u_8 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_8 [[texture(0)]], texture2d<float, access::sample> colorTexture_8 [[texture(1)]], sampler textureSampler_8 [[sampler(0)]])
{

#line 387
    thread KernelContext_0 kernelContext_8;

#line 387
    (&kernelContext_8)->u_0 = u_8;

#line 387
    (&kernelContext_8)->coverageTexture_0 = coverageTexture_8;

#line 387
    (&kernelContext_8)->colorTexture_0 = colorTexture_8;

#line 387
    (&kernelContext_8)->textureSampler_0 = textureSampler_8;

    float2 pixel_4 = position_6.xy;

    float _S52 = pixel_4.x;

#line 391
    float _S53 = pixel_4.y;

#line 391
    float _S54 = _S52 * u_8->inv0_0.x + _S53 * u_8->inv0_0.y + u_8->inv0_0.z;
    float _S55 = _S52 * u_8->inv1_0.x + _S53 * u_8->inv1_0.y + u_8->inv1_0.z;



    float2 inside_0 = (float2(_S54, _S55) - (u_8->rect_0.xy - u_8->rect_0.zw)) / (u_8->rect_0.zw * float2(2.0f) );
    float _S56 = inside_0.x;

#line 397
    bool _S57;

#line 397
    if(_S56 < 0.0f)
    {

#line 397
        _S57 = true;

#line 397
    }
    else
    {

#line 397
        _S57 = _S56 >= 1.0f;

#line 397
    }

#line 397
    if(_S57)
    {

#line 397
        _S57 = true;

#line 397
    }
    else
    {

#line 397
        _S57 = (inside_0.y) < 0.0f;

#line 397
    }

#line 397
    if(_S57)
    {

#line 397
        _S57 = true;

#line 397
    }
    else
    {

#line 397
        _S57 = (inside_0.y) >= 1.0f;

#line 397
    }

#line 397
    if(_S57)
    {

#line 397
        discard_fragment();

#line 397
    }

    float x_1 = tileTexel_0(_S54 - (&kernelContext_8)->u_0->radii_0.x, (&kernelContext_8)->u_0->radii_0.z, (&kernelContext_8)->u_0->colorA_0.x, (&kernelContext_8)->u_0->colorB_0.x, (&kernelContext_8)->u_0->gradient_0.x);
    if(x_1 < 0.0f)
    {

#line 400
        discard_fragment();

#line 400
    }
    float y_1 = tileTexel_0(_S55 - (&kernelContext_8)->u_0->radii_0.y, (&kernelContext_8)->u_0->radii_0.w, (&kernelContext_8)->u_0->colorA_0.y, (&kernelContext_8)->u_0->colorB_0.y, (&kernelContext_8)->u_0->gradient_0.y);
    if(y_1 < 0.0f)
    {

#line 402
        discard_fragment();

#line 402
    }

    int width_0 = int((&kernelContext_8)->u_0->gradient_0.x);
    int height_0 = int((&kernelContext_8)->u_0->gradient_0.y);
    float fx_0 = x_1 - 0.5f;
    float fy_0 = y_1 - 0.5f;
    float x0_0 = floor(fx_0);
    float y0_0 = floor(fy_0);
    float ax_0 = fx_0 - x0_0;
    float ay_0 = fy_0 - y0_0;
    int _S58 = int(x0_0);

#line 412
    int left_0 = tileIndex_0(_S58, width_0, (&kernelContext_8)->u_0->colorB_0.z);
    int right_0 = tileIndex_0(_S58 + int(1), width_0, (&kernelContext_8)->u_0->colorB_0.z);
    int _S59 = int(y0_0);

#line 414
    int top_0 = tileIndex_0(_S59, height_0, (&kernelContext_8)->u_0->colorB_0.w);
    int bottom_0 = tileIndex_0(_S59 + int(1), height_0, (&kernelContext_8)->u_0->colorB_0.w);

#line 415
    float4 _S60 = tileTexelPremultiplied_0(left_0, top_0, &kernelContext_8);

#line 415
    float4 _S61 = tileTexelPremultiplied_0(right_0, top_0, &kernelContext_8);
    float4 _S62 = float4(ax_0) ;

#line 416
    float4 above_0 = mix(_S60, _S61, _S62);

#line 416
    float4 _S63 = tileTexelPremultiplied_0(left_0, bottom_0, &kernelContext_8);

#line 416
    float4 _S64 = tileTexelPremultiplied_0(right_0, bottom_0, &kernelContext_8);

    float4 filtered_0 = mix(above_0, mix(_S63, _S64, _S62), float4(ay_0) );

    float coverage_6 = (&kernelContext_8)->u_0->colorA_0.w;

#line 420
    float coverage_7;
    if(((&kernelContext_8)->u_0->flags_0.z) > 0.5f)
    {

#line 421
        coverage_7 = coverage_6 * clamp(0.5f - sdRoundedRect_0(pixel_4 - (&kernelContext_8)->u_0->clipRect_0.xy, (&kernelContext_8)->u_0->clipRect_0.zw, (&kernelContext_8)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 421
    }
    else
    {

#line 421
        coverage_7 = coverage_6;

#line 421
    }

#line 427
    float4 result_0 = filtered_0 * float4(coverage_7) ;
    if((result_0.w) <= 0.0f)
    {

#line 428
        discard_fragment();

#line 428
    }

#line 428
    pixelOutput_6 _S65 = { result_0 };
    return _S65;
}

