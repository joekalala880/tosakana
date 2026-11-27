// Building walls with windows. The windows line up with the world, so tall and short buildings
// all get the right number of floors without making a new material for each one.
Shader "Tosakana/Building"
{
	Properties
	{
		_Color ("Wall Color", Color) = (1,1,1,1)
		_MainTex ("Windows (alpha = window)", 2D) = "white" {}
		_Tile ("Window Size (x = 1/width, y = 1/height)", Vector) = (0.2857, 0.303, 0, 0)
		_Night ("Window Glow", Range(0,1)) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		float4 _Tile;
		half _Night;

		struct Input
		{
			float3 worldPos;
			float3 worldNormal;
		};

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float3 n = abs(IN.worldNormal);
			float2 uv = n.x > n.z ? IN.worldPos.zy : IN.worldPos.xy;
			fixed4 w = tex2D(_MainTex, uv * _Tile.xy);
			float wall = step(n.y, 0.5);
			float win = w.a * wall;
			o.Albedo = lerp(_Color.rgb * lerp(0.85, 1.0, wall), w.rgb, win);
			o.Smoothness = win * 0.75;
			o.Metallic = 0;
			o.Emission = w.rgb * win * _Night;
			o.Alpha = 1;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
