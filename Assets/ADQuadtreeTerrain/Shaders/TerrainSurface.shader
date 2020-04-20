Shader "ADQuadtreeTerrain/TerrainSurface"
{
    Properties
    {
		_mainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows nolightmap vertex:vsMain

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 5.0

		struct TRNVERTEX
		{
			float3	pos;
			float3	normal;
			float2	uv;
		};

#ifdef SHADER_API_D3D11
		StructuredBuffer<TRNVERTEX> vertBuffer;
		StructuredBuffer<int> indexBuffer;
#endif

		sampler2D _mainTex;
		float4x4 localToWorld;
		float4x4 worldToLocal;
		float4 lodColor = float4(1, 1, 1, 1);

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		// struct for vertex input data
		struct appdata
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 color : COLOR;
			float4 texcoord : TEXCOORD0;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			uint vid : SV_VertexID;
		};

		struct Input
		{
			float2 uv_mainTex;
			float4 color : COLOR;
		};

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

		void vsMain(inout appdata v)
		{
#ifdef SHADER_API_D3D11
			int index = indexBuffer[v.vid];
			TRNVERTEX vert = vertBuffer[index];

			v.vertex = float4(vert.pos, 1.0f);
			v.normal = vert.normal;
			v.texcoord.xy = vert.uv;
			v.color = lodColor;
#endif
			// Transform modification
			unity_ObjectToWorld = localToWorld;
			unity_WorldToObject = worldToLocal;
		}

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
			half3 colorTex = tex2D(_mainTex, IN.uv_mainTex).rgb * 0.6f;
			o.Albedo = colorTex * IN.color;
            o.Metallic = 0.0f;
            o.Smoothness = 0.3f;
			o.Alpha = 1.0f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
