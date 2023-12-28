#ifndef OIT_UTILS_INCLUDED
#define OIT_UTILS_INCLUDED

struct FragmentAndLinkBuffer_STRUCT
{
    float4 pixelColor;
    float depth;
    uint next;
    uint coverage;
};

RWStructuredBuffer<FragmentAndLinkBuffer_STRUCT> FLBuffer : register(u1);       //可写入片元缓冲区
RWByteAddressBuffer StartOffsetBuffer : register(u2);                           //可写入链表

void createLinkedListEntry(float4 col, float3 pos, float2 screenParams, uint uCoverage) {
    //检索当前像素计数并增加计数器
    uint uPixelCount = FLBuffer.IncrementCounter();                             //添加一个技术器，IncrementCounter()：递增对象的隐藏计数器

    //计算缓存地址
    uint uStartOffsetAddress = 4 * ((screenParams.x * (pos.y - 0.5)) + (pos.x - 0.5));
    uint uOldStartOffset;
    StartOffsetBuffer.InterlockedExchange(uStartOffsetAddress, uPixelCount, uOldStartOffset);

    //add new Fragment Entry in FragmentAndLinkBuffer
    FragmentAndLinkBuffer_STRUCT Element;
    Element.pixelColor = col;
    Element.depth = Linear01Depth(pos.z);
    Element.next = uOldStartOffset;
    Element.coverage = uCoverage;
    FLBuffer[uPixelCount] = Element;//将每个像素写入Element 写入片元缓冲区 提供给后处理计算
}

#endif // OIT_UTILS_INCLUDED