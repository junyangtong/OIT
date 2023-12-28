Shader "Hidden/LinkedListRendering"
{
	Properties{
		_MainTex("BackgroundTex", 2D) = "white" {}
		_Alpha("Alpha", float) = 1.0
	}
	SubShader
	{
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			// #pragma enable_d3d11_debug_symbols
			#pragma multi_compile_fragment BUILT_IN POST_PROCESSING
			#define MAX_SORTED_PIXELS 24

			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};
			struct v2f {
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			struct FragmentAndLinkBuffer_STRUCT
			{
				float4 pixelColor;
				float depth;
				uint next;
				uint uCoverage;
			};

			StructuredBuffer<FragmentAndLinkBuffer_STRUCT> FLBuffer : register(t0);		//结构化缓冲区可以说是缓冲区的复合形式，它允许模板类型T是用户自定义的类型，即缓冲区存放的内容可以被解释为结构体数组。
			ByteAddressBuffer StartOffsetBuffer : register(t1); 						//只读字节地址缓冲区为HLSL程序提供了一种更为原始的内存块。从资源的开头获取一个字节偏移量，并将从该偏移量开始的四个字节作为32位无符号整数返回。由于返回的数据总数以4字节增量进行检索，

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Alpha;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			//Pixel function returns a solid color for each point.
			fixed4 frag(v2f i, uint uSampleIndex : SV_SampleIndex) : SV_Target			//采样频率索引数据。仅可由像素着色器读取或写入。
			{	//https://blog.csdn.net/fengya1/article/details/105576068
				// 获取屏幕颜色
				float4 col = tex2D(_MainTex, i.uv);

				// 获取当前像素的 ‘第一个’ 片段的偏移量
				uint uStartOffsetAddress;
#if POST_PROCESSING
				uStartOffsetAddress = 4 * (( s.x * (i.vertex.y - 0.5)) + (i.vertex.x - 0.5));
#else
				uStartOffsetAddress = 4 * ((_ScreenParams.x * (_ScreenParams.y - i.vertex.y - 0.5)) + (i.vertex.x - 0.5));
#endif
				
				uint uOffset = StartOffsetBuffer.Load(uStartOffsetAddress);				//使用一个链表头读取StartOffsetBuffer

				FragmentAndLinkBuffer_STRUCT SortedPixels[MAX_SORTED_PIXELS];

				// 分析此位置所有像素的链表
				// 并将它们存储到临时数组中以供稍后排序
				int nNumPixels = 0;
				while (uOffset != 0)
				{
					//检索当前偏移处的像素
					FragmentAndLinkBuffer_STRUCT Element = FLBuffer[uOffset];
					if (Element.uCoverage & (1 << uSampleIndex))
					{
						SortedPixels[nNumPixels] = Element;		//SortedPixels依次读取 FLBuffer
						nNumPixels += 1;
					}

					uOffset = (nNumPixels >= MAX_SORTED_PIXELS) ? 0 : FLBuffer[uOffset].next;
				}

				//对像素按深度排序
				//with insertion sort插入排序
				for (int i = 0; i < nNumPixels - 1; i++)		//遍历nNumPixels
				{
					for (int j = i + 1; j > 0; j--)
					{
						if (SortedPixels[j - 1].depth < SortedPixels[j].depth)
						{
							FragmentAndLinkBuffer_STRUCT temp = SortedPixels[j - 1];
							SortedPixels[j - 1] = SortedPixels[j];
							SortedPixels[j] = temp;
						}
					}
				}

				//渲染像素
				for (int k = 0; k < nNumPixels; k++)			//遍历nNumPixels
				{
					//检索下一个未遮挡的最远像素
					float4 vPixColor = SortedPixels[k].pixelColor;

					//手动混合当前片段和上一个片段
					col.rgb = lerp(col.rgb, vPixColor.rgb, _Alpha);//vPixColor.a
				}

				return col;
			}
			ENDCG
		}
	}
}
