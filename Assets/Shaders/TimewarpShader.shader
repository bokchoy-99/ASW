Shader "Custom/TimewarpShader"
{
    Properties
    {
        _PreviousColorTex("Previous Color Texture", 2D) = "white" {}
        _PreviousMTex("Previous M Texture", 2D) = "white" {}
        _PreviousDTex("Previous D Texture", 2D) = "white" {}
        _TransformMatrix0("Transform Matrix Row 0", Vector) = (1, 0, 0, 0)
        _TransformMatrix1("Transform Matrix Row 1", Vector) = (0, 1, 0, 0)
        _TransformMatrix2("Transform Matrix Row 2", Vector) = (0, 0, 1, 0)
        _TransformMatrix3("Transform Matrix Row 3", Vector) = (0, 0, 0, 1)

        _TransformMatrix10("Transform Matrix Row 10", Vector) = (1, 0, 0, 0)
        _TransformMatrix11("Transform Matrix Row 11", Vector) = (0, 1, 0, 0)
        _TransformMatrix12("Transform Matrix Row 12", Vector) = (0, 0, 1, 0)
        _TransformMatrix13("Transform Matrix Row 13", Vector) = (0, 0, 0, 1)

        _Width("Per Mesh Width", Float) = 0.01
        _Height("Per Mesh Height", Float) = 0.01
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            Pass
            {
                Cull Off
                ZTest LEqual
                ZWrite On

                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma enable_cbuffer

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

                // Uniforms
                

                SamplerState my_point_clamp_sampler;
                SamplerState my_linear_clamp_sampler;

                CBUFFER_START(UnityPerMaterial)
                    Texture2D _PreviousColorTex;
                    Texture2D _PreviousMTex;
                    Texture2D _PreviousDTex;

                    float4 _TransformMatrix0;
                    float4 _TransformMatrix1;
                    float4 _TransformMatrix2;
                    float4 _TransformMatrix3;

                    float4 _TransformMatrix10;
                    float4 _TransformMatrix11;
                    float4 _TransformMatrix12;
                    float4 _TransformMatrix13;

                    float _Width;
                    float _Height;
                CBUFFER_END

                float _far;
                float _near;

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 xyzw : TEXCOORD1;
                };

                float4 GetDepth(float2 uv)
                {
                    float _DepthThreshold = 0.01;
                    float4 depth;
                    depth.xyz = _PreviousMTex.SampleLevel(my_point_clamp_sampler, uv, 0).xyz;
                    depth.w = _PreviousDTex.SampleLevel(my_point_clamp_sampler, uv, 0).x;
                    //return depth;
                    float4 maxDepth = depth;
                    float4 near_depth;
                    float2 near_uv;
                    float2 inv;
                    inv.x = _Width;
                    inv.y = _Height;
                    float2 offsets[8] = {
                        float2(-inv.x, 0),      // 左
                        float2(inv.x, 0),       // 右
                        float2(0, -inv.y),      // 下
                        float2(0, inv.y),       // 上
                        float2(-inv.x, -inv.y), // 左下
                        float2(inv.x, -inv.y),  // 右下
                        float2(-inv.x, inv.y),  // 左上
                        float2(inv.x, inv.y)    // 右上
                    };

                    for (int i = 0; i < 8; i++)
                    {
                        near_uv = uv + offsets[i];
                        near_depth.xyz = _PreviousMTex.SampleLevel(my_point_clamp_sampler, near_uv, 0).xyz;
                        near_depth.w = _PreviousDTex.SampleLevel(my_point_clamp_sampler, near_uv, 0).x;

                        if (near_depth.w > maxDepth.w)
                        {
                            maxDepth = near_depth;
                        }
                    }

                    return maxDepth;
                }

                float4 GetDepth1(float2 uv)
                {
                    float _DepthThreshold = 0.01;
                    float4 depth;
                    depth.xyz = _PreviousMTex.SampleLevel(my_point_clamp_sampler, uv, 0).xyz;
                    depth.w = _PreviousDTex.SampleLevel(my_point_clamp_sampler, uv, 0).x;
                    return depth;
                }

                v2f vert(appdata v)
                {
                    v2f o;
                    float4 d = GetDepth1(v.uv);

                    float4x4 transformMatrix = float4x4(_TransformMatrix0, _TransformMatrix1, _TransformMatrix2, _TransformMatrix3);
                    float4x4 transformMatrix1 = float4x4(_TransformMatrix10, _TransformMatrix11, _TransformMatrix12, _TransformMatrix13);
                    float4 p = float4(v.vertex.x + 2 * d.x, v.vertex.y + 2 * d.y, d.w + d.z, 1.0);
                    //float4 p = float4(v.vertex.x +  d.x, v.vertex.y +  d.y, d.w + d.z, 1.0);
                    //p.z = 2 * p.z - 1;
                    //p.z = Linear01Depth(p.z);
                    //float w = (_far - _near)/(_far + _near - 2 * p.z * (_far - _near));
                    //p = p * w;
                    //float4 p = float4(v.vertex.x, v.vertex.y, 0.2, 1.0);
                    //o.vertex = p;
                    o.vertex = mul(transformMatrix, p);
                    o.vertex.xyzw = o.vertex.xyzw / o.vertex.w;
                    o.vertex = TransformWorldToHClip(o.vertex.xyz);
                    
                    o.vertex.xyzw = o.vertex.xyzw / o.vertex.w;
                    o.vertex.z = o.vertex.z > 0.00001 ? o.vertex.z : 0.00001;
                    o.vertex.z = o.vertex.z < 0.99999 ? o.vertex.z : 0.99999;

                    if (v.uv.x == 0 || v.uv.x == 1) o.vertex.x = v.vertex.x;
                    if (v.uv.y == 0 || v.uv.y == 1) o.vertex.y = v.vertex.y;

                    o.xyzw = o.vertex;
                    //o.vertex = p;
                    o.uv = v.uv;
                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    //return float4(i.vertex.z - 0.5, 0, 0, 1);
                    //return float4(i.xyzw.z - 0.5, 0, 0, 1);
                    //if (i.vertex.z > 0.99)
                    //    return (1,1,1,0);
                    i.xyzw = i.xyzw / i.xyzw.w;
                    float4 previousColor = _PreviousColorTex.Sample(my_linear_clamp_sampler, i.uv);
                    
                    return float4(previousColor.x, previousColor.y, previousColor.z, i.xyzw.z);
                    //return half4(i.vertex.z, 0, 0, 0);
                    //return previousColor;
                }
                ENDHLSL
            }
        }
        FallBack "Diffuse"
}
