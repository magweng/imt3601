﻿Shader "Custom/BunnyAlert" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
		_Color("Color", Color) = (1, 1, 1, 1)
	}
	SubShader{
		Tags{ "RenderType" = "Transparent" "Queue" = "Transparent-5" }
		Blend SrcAlpha OneMinusSrcAlpha

		Pass{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "utils.hlsl"

			struct appdata {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
			};

			struct v2f {
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;

			v2f vert(appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target{
				float seed = _Time * 145.3f;
				float3 samplePos = { i.uv.x + 17.3, i.uv.y + 17.3, 0.3 };
				samplePos.z += seed;
				samplePos *= 30.3;
				float n = noise(samplePos);
				n = noise(samplePos*n*12.5);

				fixed3 black = { 0, 0, 0 };
				fixed3 red = { 1, 0, 0 };

				fixed4 col = { 1, 1, 1, pow(distance(i.uv.xy, float2(0.5, 0.5)) * 2, 4) };
				col = _Color * col;
				col.rgb *= lerp(red, black, n);				
				return col;
			}
			ENDCG
		}
	}
}
