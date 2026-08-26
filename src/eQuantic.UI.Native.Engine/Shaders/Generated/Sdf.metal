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


#line 98 "/Users/admin.edgar.a.mesquita/projects/equantic/equantic-ui/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
[[vertex]] vertexOutput_0 fullscreen_vertex(uint vid_0 [[vertex_id]], DrawUniforms_0 constant* u_1 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_1 [[texture(0)]], texture2d<float, access::sample> colorTexture_1 [[texture(1)]], sampler textureSampler_1 [[sampler(0)]])
{

#line 98
    thread KernelContext_0 kernelContext_0;

#line 98
    (&kernelContext_0)->u_0 = u_1;

#line 98
    (&kernelContext_0)->coverageTexture_0 = coverageTexture_1;

#line 98
    (&kernelContext_0)->colorTexture_0 = colorTexture_1;

#line 98
    (&kernelContext_0)->textureSampler_0 = textureSampler_1;


    array<float2, int(3)> positions_0 = { float2(-1.0f, -1.0f), float2(3.0f, -1.0f), float2(-1.0f, 3.0f) };

#line 101
    vertexOutput_0 _S1 = { float4(positions_0[vid_0], 0.0f, 1.0f) };
    return _S1;
}


#line 60
float sdAnnularSector_0(float2 p_0, float innerRadius_0, float outerRadius_0, float startAngle_0, float endAngle_0, float rounding_0)
{

    float rIn_0 = innerRadius_0 + rounding_0;
    float rOut_0 = outerRadius_0 - rounding_0;
    float mid_0 = (startAngle_0 + endAngle_0) / 2.0f;
    float half_0 = (endAngle_0 - startAngle_0) / 2.0f;

    float cosMid_0 = cos(mid_0);
    float sinMid_0 = sin(mid_0);
    float _S2 = p_0.x;

#line 70
    float _S3 = p_0.y;

#line 70
    float qx_0 = cosMid_0 * _S2 + sinMid_0 * _S3;
    float qy_0 = abs(- sinMid_0 * _S2 + cosMid_0 * _S3);
    float r_0 = sqrt(qx_0 * qx_0 + qy_0 * qy_0);

    float sinHalf_0 = sin(half_0);
    float cosHalf_0 = cos(half_0);
    float w_0 = qx_0 * sinHalf_0 - qy_0 * cosHalf_0;

#line 76
    float d_0;


    if(w_0 >= rounding_0)
    {

#line 79
        d_0 = max(max(rIn_0 - r_0, r_0 - rOut_0), rounding_0 - w_0);

#line 79
    }
    else
    {

#line 86
        float _S4 = rounding_0 * rounding_0;

#line 85
        float s_0 = clamp(qx_0 * cosHalf_0 + qy_0 * sinHalf_0, sqrt(max(rIn_0 * rIn_0 - _S4, 0.0f)), sqrt(max(rOut_0 * rOut_0 - _S4, 0.0f)));

#line 90
        float dx_0 = qx_0 - (s_0 * cosHalf_0 + rounding_0 * sinHalf_0);
        float dy_0 = qy_0 - (s_0 * sinHalf_0 - rounding_0 * cosHalf_0);

#line 91
        d_0 = sqrt(dx_0 * dx_0 + dy_0 * dy_0);

#line 79
    }

#line 94
    return d_0 - rounding_0;
}


#line 44
float sdRoundedRect_0(float2 p_1, float2 halfSize_0, float4 radii_1)
{

#line 44
    float r_1;


    if((p_1.x) >= 0.0f)
    {

#line 47
        if((p_1.y) >= 0.0f)
        {

#line 47
            r_1 = radii_1.z;

#line 47
        }
        else
        {

#line 47
            r_1 = radii_1.y;

#line 47
        }

#line 47
    }
    else
    {

#line 48
        if((p_1.y) >= 0.0f)
        {

#line 48
            r_1 = radii_1.w;

#line 48
        }
        else
        {

#line 48
            r_1 = radii_1.x;

#line 48
        }

#line 47
    }

    float2 q_0 = abs(p_1) - (halfSize_0 - float2(r_1) );


    return length(max(q_0, float2(0.0f) )) + min(max(q_0.x, q_0.y), 0.0f) - r_1;
}


#line 38
float srgbToLinear_0(float c_0)
{

#line 38
    float _S5;

    if(c_0 <= 0.04044999927282333f)
    {

#line 40
        _S5 = c_0 / 12.92000007629394531f;

#line 40
    }
    else
    {

#line 40
        _S5 = pow((c_0 + 0.05499999970197678f) / 1.0549999475479126f, 2.40000009536743164f);

#line 40
    }

#line 40
    return _S5;
}


#line 40
struct pixelOutput_0
{
    float4 output_1 [[color(0)]];
};


#line 106
[[fragment]] pixelOutput_0 sdf_fragment(float4 position_0 [[position]], DrawUniforms_0 constant* u_2 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_2 [[texture(0)]], texture2d<float, access::sample> colorTexture_2 [[texture(1)]], sampler textureSampler_2 [[sampler(0)]])
{

#line 106
    thread KernelContext_0 kernelContext_1;

#line 106
    (&kernelContext_1)->u_0 = u_2;

#line 106
    (&kernelContext_1)->coverageTexture_0 = coverageTexture_2;

#line 106
    (&kernelContext_1)->colorTexture_0 = colorTexture_2;

#line 106
    (&kernelContext_1)->textureSampler_0 = textureSampler_2;


    float2 pixel_0 = position_0.xy;

    float _S6 = pixel_0.x;

#line 111
    float _S7 = pixel_0.y;

#line 111
    float _S8 = _S6 * u_2->inv0_0.x + _S7 * u_2->inv0_0.y + u_2->inv0_0.z;
    float _S9 = _S6 * u_2->inv1_0.x + _S7 * u_2->inv1_0.y + u_2->inv1_0.z;

#line 110
    float2 local_0 = float2(_S8, _S9);

#line 110
    bool _S10;

#line 116
    if((u_2->flags_0.x) > 2.5f)
    {

#line 116
        _S10 = (u_2->flags_0.x) < 3.5f;

#line 116
    }
    else
    {

#line 116
        _S10 = false;

#line 116
    }

#line 116
    float d_1;

#line 116
    if(_S10)
    {

#line 116
        d_1 = sdAnnularSector_0(local_0 - (&kernelContext_1)->u_0->rect_0.xy, (&kernelContext_1)->u_0->radii_0.x, (&kernelContext_1)->u_0->rect_0.z, (&kernelContext_1)->u_0->radii_0.y, (&kernelContext_1)->u_0->radii_0.z, (&kernelContext_1)->u_0->radii_0.w);

#line 116
    }
    else
    {



        float d_2 = sdRoundedRect_0(local_0 - (&kernelContext_1)->u_0->rect_0.xy, (&kernelContext_1)->u_0->rect_0.zw, (&kernelContext_1)->u_0->radii_0);
        if((u_2->flags_0.x) > 0.5f)
        {

#line 123
            _S10 = (u_2->flags_0.x) < 1.5f;

#line 123
        }
        else
        {

#line 123
            _S10 = false;

#line 123
        }

#line 123
        if(_S10)
        {

#line 123
            d_1 = abs(d_2) - u_2->inv1_0.w / 2.0f;

#line 123
        }
        else
        {

#line 123
            d_1 = d_2;

#line 123
        }

#line 116
    }

#line 128
    if((u_2->flags_0.x) > 1.5f)
    {

#line 128
        _S10 = (u_2->flags_0.x) < 2.5f;

#line 128
    }
    else
    {

#line 128
        _S10 = false;

#line 128
    }

#line 128
    float coverage_0;

#line 128
    if(_S10)
    {

        float sigma_0 = u_2->inv1_0.w * u_2->inv0_0.w / 2.0f;
        if(sigma_0 <= 0.0f)
        {

#line 132
            coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 132
        }
        else
        {



            float t_0 = clamp((d_1 * u_2->inv0_0.w + 1.5f * sigma_0) / (3.0f * sigma_0), 0.0f, 1.0f);

#line 138
            coverage_0 = 1.0f - t_0 * t_0 * (3.0f - 2.0f * t_0);

#line 132
        }

#line 128
    }
    else
    {

#line 128
        coverage_0 = clamp(0.5f - d_1 * u_2->inv0_0.w, 0.0f, 1.0f);

#line 128
    }

#line 146
    if(coverage_0 <= 0.0f)
    {

#line 146
        discard_fragment();

#line 146
    }



    if((u_2->flags_0.z) > 0.5f)
    {

        float coverage_1 = coverage_0 * clamp(0.5f - sdRoundedRect_0(pixel_0 - (&kernelContext_1)->u_0->clipRect_0.xy, (&kernelContext_1)->u_0->clipRect_0.zw, (&kernelContext_1)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_1 <= 0.0f)
        {

#line 154
            discard_fragment();

#line 154
        }

#line 154
        coverage_0 = coverage_1;

#line 150
    }

#line 159
    float4 _S11 = (&kernelContext_1)->u_0->colorA_0;

#line 159
    float dx_1;

#line 159
    float4 srgb_0;
    if((u_2->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_1)->u_0->gradient_0.z) <= 0.0f)
        {

#line 163
            dx_1 = 0.0f;

#line 163
        }
        else
        {

#line 163
            dx_1 = (_S8 - (&kernelContext_1)->u_0->gradient_0.x) / (&kernelContext_1)->u_0->gradient_0.z;

#line 163
        }

#line 163
        float dy_1;
        if(((&kernelContext_1)->u_0->gradient_0.w) <= 0.0f)
        {

#line 164
            dy_1 = 0.0f;

#line 164
        }
        else
        {

#line 164
            dy_1 = (_S9 - (&kernelContext_1)->u_0->gradient_0.y) / (&kernelContext_1)->u_0->gradient_0.w;

#line 164
        }

#line 164
        srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(clamp(sqrt(dx_1 * dx_1 + dy_1 * dy_1), 0.0f, 1.0f)) );

#line 160
    }
    else
    {

#line 168
        if((u_2->flags_0.y) > 0.5f)
        {
            float2 axis_0 = (&kernelContext_1)->u_0->gradient_0.zw - (&kernelContext_1)->u_0->gradient_0.xy;
            float len2_0 = dot(axis_0, axis_0);
            if(len2_0 <= 0.0f)
            {

#line 172
                dx_1 = 0.0f;

#line 172
            }
            else
            {

#line 172
                dx_1 = clamp(dot(local_0 - (&kernelContext_1)->u_0->gradient_0.xy, axis_0) / len2_0, 0.0f, 1.0f);

#line 172
            }

#line 172
            srgb_0 = mix((&kernelContext_1)->u_0->colorA_0, (&kernelContext_1)->u_0->colorB_0, float4(dx_1) );

#line 168
        }
        else
        {

#line 168
            srgb_0 = _S11;

#line 168
        }

#line 160
    }

#line 177
    float a_0 = srgb_0.w * coverage_0;

#line 177
    pixelOutput_0 _S12 = { float4(float3(srgbToLinear_0(srgb_0.x), srgbToLinear_0(srgb_0.y), srgbToLinear_0(srgb_0.z)) * float3(a_0) , a_0) };

    return _S12;
}


#line 179
struct pixelOutput_1
{
    float4 output_2 [[color(0)]];
};



[[fragment]] pixelOutput_1 textured_fragment(float4 position_1 [[position]], DrawUniforms_0 constant* u_3 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_3 [[texture(0)]], texture2d<float, access::sample> colorTexture_3 [[texture(1)]], sampler textureSampler_3 [[sampler(0)]])
{

#line 186
    thread KernelContext_0 kernelContext_2;

#line 186
    (&kernelContext_2)->u_0 = u_3;

#line 186
    (&kernelContext_2)->coverageTexture_0 = coverageTexture_3;

#line 186
    (&kernelContext_2)->colorTexture_0 = colorTexture_3;

#line 186
    (&kernelContext_2)->textureSampler_0 = textureSampler_3;

    float2 pixel_1 = position_1.xy;

    float _S13 = pixel_1.x;

#line 190
    float _S14 = pixel_1.y;

#line 190
    float _S15 = _S13 * u_3->inv0_0.x + _S14 * u_3->inv0_0.y + u_3->inv0_0.z;
    float _S16 = _S13 * u_3->inv1_0.x + _S14 * u_3->inv1_0.y + u_3->inv1_0.z;

#line 189
    float2 local_1 = float2(_S15, _S16);

#line 195
    float2 uv_0 = (local_1 - (u_3->rect_0.xy - u_3->rect_0.zw)) / (u_3->rect_0.zw * float2(2.0f) );
    float _S17 = uv_0.x;

#line 196
    bool _S18;

#line 196
    if(_S17 < 0.0f)
    {

#line 196
        _S18 = true;

#line 196
    }
    else
    {

#line 196
        _S18 = _S17 >= 1.0f;

#line 196
    }

#line 196
    if(_S18)
    {

#line 196
        _S18 = true;

#line 196
    }
    else
    {

#line 196
        _S18 = (uv_0.y) < 0.0f;

#line 196
    }

#line 196
    if(_S18)
    {

#line 196
        _S18 = true;

#line 196
    }
    else
    {

#line 196
        _S18 = (uv_0.y) >= 1.0f;

#line 196
    }

#line 196
    if(_S18)
    {

#line 196
        discard_fragment();

#line 196
    }



    int3 _S19 = int3(min(int((&kernelContext_2)->u_0->gradient_0.x) - int(1), int(_S17 * (&kernelContext_2)->u_0->gradient_0.x)), min(int((&kernelContext_2)->u_0->gradient_0.y) - int(1), int(uv_0.y * (&kernelContext_2)->u_0->gradient_0.y)), int(0));

#line 200
    float coverage_2 = (((&kernelContext_2)->coverageTexture_0).read(vec<uint,2>(((_S19)).xy), uint(((_S19)).z)).x);
    if(coverage_2 <= 0.0f)
    {

#line 201
        discard_fragment();

#line 201
    }

#line 201
    float coverage_3;

    if(((&kernelContext_2)->u_0->flags_0.z) > 0.5f)
    {

        float coverage_4 = coverage_2 * clamp(0.5f - sdRoundedRect_0(pixel_1 - (&kernelContext_2)->u_0->clipRect_0.xy, (&kernelContext_2)->u_0->clipRect_0.zw, (&kernelContext_2)->u_0->clipRadii_0), 0.0f, 1.0f);
        if(coverage_4 <= 0.0f)
        {

#line 207
            discard_fragment();

#line 207
        }

#line 207
        coverage_3 = coverage_4;

#line 203
    }
    else
    {

#line 203
        coverage_3 = coverage_2;

#line 203
    }

#line 215
    float4 _S20 = (&kernelContext_2)->u_0->colorA_0;

#line 215
    float dx_2;

#line 215
    float4 srgb_1;
    if(((&kernelContext_2)->u_0->flags_0.y) > 1.5f)
    {

        if(((&kernelContext_2)->u_0->radii_0.z) <= 0.0f)
        {

#line 219
            dx_2 = 0.0f;

#line 219
        }
        else
        {

#line 219
            dx_2 = (_S15 - (&kernelContext_2)->u_0->radii_0.x) / (&kernelContext_2)->u_0->radii_0.z;

#line 219
        }

#line 219
        float dy_2;
        if(((&kernelContext_2)->u_0->radii_0.w) <= 0.0f)
        {

#line 220
            dy_2 = 0.0f;

#line 220
        }
        else
        {

#line 220
            dy_2 = (_S16 - (&kernelContext_2)->u_0->radii_0.y) / (&kernelContext_2)->u_0->radii_0.w;

#line 220
        }

#line 220
        srgb_1 = mix((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, float4(clamp(sqrt(dx_2 * dx_2 + dy_2 * dy_2), 0.0f, 1.0f)) );

#line 216
    }
    else
    {

#line 224
        if(((&kernelContext_2)->u_0->flags_0.y) > 0.5f)
        {
            float2 axis_1 = (&kernelContext_2)->u_0->radii_0.zw - (&kernelContext_2)->u_0->radii_0.xy;
            float len2_1 = dot(axis_1, axis_1);
            if(len2_1 <= 0.0f)
            {

#line 228
                dx_2 = 0.0f;

#line 228
            }
            else
            {

#line 228
                dx_2 = clamp(dot(local_1 - (&kernelContext_2)->u_0->radii_0.xy, axis_1) / len2_1, 0.0f, 1.0f);

#line 228
            }

#line 228
            srgb_1 = mix((&kernelContext_2)->u_0->colorA_0, (&kernelContext_2)->u_0->colorB_0, float4(dx_2) );

#line 224
        }
        else
        {

#line 224
            srgb_1 = _S20;

#line 224
        }

#line 216
    }

#line 232
    float a_1 = srgb_1.w * coverage_3;

#line 232
    pixelOutput_1 _S21 = { float4(float3(srgbToLinear_0(srgb_1.x), srgbToLinear_0(srgb_1.y), srgbToLinear_0(srgb_1.z)) * float3(a_1) , a_1) };

    return _S21;
}


#line 234
struct pixelOutput_2
{
    float4 output_3 [[color(0)]];
};


#line 244
[[fragment]] pixelOutput_2 blur_down(float4 position_2 [[position]], DrawUniforms_0 constant* u_4 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_4 [[texture(0)]], texture2d<float, access::sample> colorTexture_4 [[texture(1)]], sampler textureSampler_4 [[sampler(0)]])
{

#line 244
    thread KernelContext_0 kernelContext_3;

#line 244
    (&kernelContext_3)->u_0 = u_4;

#line 244
    (&kernelContext_3)->coverageTexture_0 = coverageTexture_4;

#line 244
    (&kernelContext_3)->colorTexture_0 = colorTexture_4;

#line 244
    (&kernelContext_3)->textureSampler_0 = textureSampler_4;

    float2 uv_1 = position_2.xy / (u_4->rect_0.zw * float2(2.0f) );
    float2 half_1 = u_4->gradient_0.xy;

#line 252
    float2 _S22 = float2(half_1.x, - half_1.y);

#line 252
    pixelOutput_2 _S23 = { (((colorTexture_4).sample((textureSampler_4), (uv_1))) * float4(4.0f)  + ((colorTexture_4).sample((textureSampler_4), (uv_1 - half_1))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 + half_1))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 + _S22))) + ((colorTexture_4).sample((textureSampler_4), (uv_1 - _S22)))) / float4(8.0f)  };

    return _S23;
}


#line 254
struct pixelOutput_3
{
    float4 output_4 [[color(0)]];
};


#line 258
[[fragment]] pixelOutput_3 blur_up(float4 position_3 [[position]], DrawUniforms_0 constant* u_5 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_5 [[texture(0)]], texture2d<float, access::sample> colorTexture_5 [[texture(1)]], sampler textureSampler_5 [[sampler(0)]])
{

#line 258
    thread KernelContext_0 kernelContext_4;

#line 258
    (&kernelContext_4)->u_0 = u_5;

#line 258
    (&kernelContext_4)->coverageTexture_0 = coverageTexture_5;

#line 258
    (&kernelContext_4)->colorTexture_0 = colorTexture_5;

#line 258
    (&kernelContext_4)->textureSampler_0 = textureSampler_5;

    float2 uv_2 = position_3.xy / (u_5->rect_0.zw * float2(2.0f) );
    float2 half_2 = u_5->gradient_0.xy;

    float _S24 = half_2.x;

#line 263
    float _S25 = - _S24;
    float _S26 = half_2.y;

#line 264
    float4 _S27 = float4(2.0f) ;



    float _S28 = - _S26;

#line 268
    pixelOutput_3 _S29 = { (((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S25 * 2.0f, 0.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S25, _S26)))) * _S27 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(0.0f, _S26 * 2.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S24, _S26)))) * _S27 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S24 * 2.0f, 0.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S24, _S28)))) * _S27 + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(0.0f, _S28 * 2.0f)))) + ((colorTexture_5).sample((textureSampler_5), (uv_2 + float2(_S25, _S28)))) * _S27) / float4(12.0f)  };


    return _S29;
}


#line 271
struct pixelOutput_4
{
    float4 output_5 [[color(0)]];
};


#line 281
[[fragment]] pixelOutput_4 layer_composite(float4 position_4 [[position]], DrawUniforms_0 constant* u_6 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_6 [[texture(0)]], texture2d<float, access::sample> colorTexture_6 [[texture(1)]], sampler textureSampler_6 [[sampler(0)]])
{

#line 281
    thread KernelContext_0 kernelContext_5;

#line 281
    (&kernelContext_5)->u_0 = u_6;

#line 281
    (&kernelContext_5)->coverageTexture_0 = coverageTexture_6;

#line 281
    (&kernelContext_5)->colorTexture_0 = colorTexture_6;

#line 281
    (&kernelContext_5)->textureSampler_0 = textureSampler_6;

    float2 pixel_2 = position_4.xy;

    float _S30 = pixel_2.x;

#line 285
    float _S31 = pixel_2.y;



    float2 uv_3 = (float2(_S30 * u_6->inv0_0.x + _S31 * u_6->inv0_0.y + u_6->inv0_0.z, _S30 * u_6->inv1_0.x + _S31 * u_6->inv1_0.y + u_6->inv1_0.z) - (u_6->rect_0.xy - u_6->rect_0.zw)) / (u_6->rect_0.zw * float2(2.0f) );
    float _S32 = uv_3.x;

#line 290
    bool _S33;

#line 290
    if(_S32 < 0.0f)
    {

#line 290
        _S33 = true;

#line 290
    }
    else
    {

#line 290
        _S33 = _S32 >= 1.0f;

#line 290
    }

#line 290
    if(_S33)
    {

#line 290
        _S33 = true;

#line 290
    }
    else
    {

#line 290
        _S33 = (uv_3.y) < 0.0f;

#line 290
    }

#line 290
    if(_S33)
    {

#line 290
        _S33 = true;

#line 290
    }
    else
    {

#line 290
        _S33 = (uv_3.y) >= 1.0f;

#line 290
    }

#line 290
    if(_S33)
    {

#line 290
        discard_fragment();

#line 290
    }



    int3 _S34 = int3(min(int((&kernelContext_5)->u_0->gradient_0.x) - int(1), int(_S32 * (&kernelContext_5)->u_0->gradient_0.x)), min(int((&kernelContext_5)->u_0->gradient_0.y) - int(1), int(uv_3.y * (&kernelContext_5)->u_0->gradient_0.y)), int(0));

#line 294
    float4 texel_0 = (((&kernelContext_5)->colorTexture_0).read(vec<uint,2>(((_S34)).xy), uint(((_S34)).z)));

    float alpha_0 = (&kernelContext_5)->u_0->colorA_0.w;

#line 296
    float alpha_1;
    if(((&kernelContext_5)->u_0->flags_0.z) > 0.5f)
    {

#line 297
        alpha_1 = alpha_0 * clamp(0.5f - sdRoundedRect_0(pixel_2 - (&kernelContext_5)->u_0->clipRect_0.xy, (&kernelContext_5)->u_0->clipRect_0.zw, (&kernelContext_5)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 297
    }
    else
    {

#line 297
        alpha_1 = alpha_0;

#line 297
    }

#line 302
    if(alpha_1 <= 0.0f)
    {

#line 302
        discard_fragment();

#line 302
    }

#line 302
    pixelOutput_4 _S35 = { texel_0 * float4(alpha_1)  };

    return _S35;
}


#line 304
struct pixelOutput_5
{
    float4 output_6 [[color(0)]];
};



[[fragment]] pixelOutput_5 textured_rgba_fragment(float4 position_5 [[position]], DrawUniforms_0 constant* u_7 [[buffer(0)]], texture2d<float, access::sample> coverageTexture_7 [[texture(0)]], texture2d<float, access::sample> colorTexture_7 [[texture(1)]], sampler textureSampler_7 [[sampler(0)]])
{

#line 311
    thread KernelContext_0 kernelContext_6;

#line 311
    (&kernelContext_6)->u_0 = u_7;

#line 311
    (&kernelContext_6)->coverageTexture_0 = coverageTexture_7;

#line 311
    (&kernelContext_6)->colorTexture_0 = colorTexture_7;

#line 311
    (&kernelContext_6)->textureSampler_0 = textureSampler_7;

    float2 pixel_3 = position_5.xy;

    float _S36 = pixel_3.x;

#line 315
    float _S37 = pixel_3.y;



    float2 uv_4 = (float2(_S36 * u_7->inv0_0.x + _S37 * u_7->inv0_0.y + u_7->inv0_0.z, _S36 * u_7->inv1_0.x + _S37 * u_7->inv1_0.y + u_7->inv1_0.z) - (u_7->rect_0.xy - u_7->rect_0.zw)) / (u_7->rect_0.zw * float2(2.0f) );
    float _S38 = uv_4.x;

#line 320
    bool _S39;

#line 320
    if(_S38 < 0.0f)
    {

#line 320
        _S39 = true;

#line 320
    }
    else
    {

#line 320
        _S39 = _S38 >= 1.0f;

#line 320
    }

#line 320
    if(_S39)
    {

#line 320
        _S39 = true;

#line 320
    }
    else
    {

#line 320
        _S39 = (uv_4.y) < 0.0f;

#line 320
    }

#line 320
    if(_S39)
    {

#line 320
        _S39 = true;

#line 320
    }
    else
    {

#line 320
        _S39 = (uv_4.y) >= 1.0f;

#line 320
    }

#line 320
    if(_S39)
    {

#line 320
        discard_fragment();

#line 320
    }



    int3 _S40 = int3(min(int((&kernelContext_6)->u_0->gradient_0.x) - int(1), int(_S38 * (&kernelContext_6)->u_0->gradient_0.x)), min(int((&kernelContext_6)->u_0->gradient_0.y) - int(1), int(uv_4.y * (&kernelContext_6)->u_0->gradient_0.y)), int(0));

#line 324
    float4 texel_1 = (((&kernelContext_6)->colorTexture_0).read(vec<uint,2>(((_S40)).xy), uint(((_S40)).z)));

#line 324
    float coverage_5;


    if(((&kernelContext_6)->u_0->flags_0.z) > 0.5f)
    {

#line 327
        coverage_5 = clamp(0.5f - sdRoundedRect_0(pixel_3 - (&kernelContext_6)->u_0->clipRect_0.xy, (&kernelContext_6)->u_0->clipRect_0.zw, (&kernelContext_6)->u_0->clipRadii_0), 0.0f, 1.0f);

#line 327
    }
    else
    {

#line 327
        coverage_5 = 1.0f;

#line 327
    }

#line 333
    float a_2 = texel_1.w * coverage_5;
    if(a_2 <= 0.0f)
    {

#line 334
        discard_fragment();

#line 334
    }

#line 334
    pixelOutput_5 _S41 = { float4(texel_1.xyz * float3(a_2) , a_2) };
    return _S41;
}

