using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class OrderIndependentTransparency : MonoBehaviour
{
    [Tooltip("This can be increased if objects disappear or block artifacts appear. A lower value keeps the used video memory at a minimum.")]
    [Range(1f, 24f)]
    public int listSizeMultiplier = 5;

    private GraphicsBuffer fragmentLinkBuffer;  //声明图形缓冲区
    private int fragmentLinkBufferId;
    private GraphicsBuffer startOffsetBuffer;   //声明图形缓冲区
    private int startOffsetBufferId;
    private int bufferSize;
    private int bufferStride;                   //步幅
    private Material linkedListMaterial;
    private uint[] resetTable;                  //uint表示范围是：2^32即0到4294967295。
    [Range(0f, 1f)]
    public float alpha = 0.0f;

    private void Update() {
        alpha += Time.deltaTime * 0.1f;
        if(alpha > 1.0f)
        {
            alpha = 0.0f;
        }
        
    }
    private void OnEnable()
    {
        linkedListMaterial = new Material(Shader.Find("Hidden/LinkedListRendering"));
        int bufferWidth = Screen.width > 0 ? Screen.width : 1024;
        int bufferHeight = Screen.height > 0 ? Screen.height : 1024;                                            //设置大于屏幕大小的缓冲区
        
        int bufferSize = bufferWidth * bufferHeight * listSizeMultiplier;
        int bufferStride = sizeof(float) * 5 + sizeof(uint);
        //这个缓冲区包含了所有透明片元
        //创建一个像素链表 per pixel Linked List
        fragmentLinkBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Counter, bufferSize, bufferStride);       //（GraphicsBuffer.Target.Counter :带有内部计数器的 GraphicsBuffer。  count：缓冲区中元素的数量（只读）。stride	：缓冲区中一个元素的大小（只读）。）

        fragmentLinkBufferId = Shader.PropertyToID("FLBuffer");
        
        int bufferSizeHead = bufferWidth * bufferHeight;                                                        //设置屏幕大小的缓冲区
        int bufferStrideHead = sizeof(uint);
        //create buffer for addresses, 作为链表的表头
        startOffsetBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, bufferSizeHead, bufferStrideHead);    //GraphicsBuffer.Target.Raw ： GraphicsBuffer可用作原始字节地址缓冲区。
        startOffsetBufferId = Shader.PropertyToID("StartOffsetBuffer");

        resetTable = new uint[bufferSizeHead];
    }

    private void OnPreRender()                                                  //在摄像机开始渲染场景前，将调用 OnPreRender。仅当该脚本附加到摄像机并且启用时，才调用该函数。
    {
        if (fragmentLinkBuffer == null || startOffsetBuffer == null)
            return;

        //reset StartOffsetBuffer to zeros
        startOffsetBuffer.SetData(resetTable);

        // set buffers for rendering
        Graphics.SetRandomWriteTarget(1, fragmentLinkBuffer);                   //index	着色器中随机写入目标的索引。uav	：写入渲染目标。
        Graphics.SetRandomWriteTarget(2, startOffsetBuffer);
        linkedListMaterial.SetFloat("_Alpha",alpha);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) //Unity 在摄像机完成渲染后调用的事件函数，用于修改摄像机的最终图像。此时物体shader已经将数据写入fragmentLinkBuffer和startOffsetBuffer
    {
        
        if (fragmentLinkBuffer == null || startOffsetBuffer == null || linkedListMaterial == null)
            return;

        Graphics.ClearRandomWriteTargets();                                     //该函数将清除之前用 SetRandomWriteTarget 设置的任意“随机写”目标。
        // blend linked list
        linkedListMaterial.SetBuffer(fragmentLinkBufferId, fragmentLinkBuffer);
        linkedListMaterial.SetBuffer(startOffsetBufferId, startOffsetBuffer);
        Graphics.Blit(source, destination, linkedListMaterial);
    }

    private void OnDisable()
    {
        fragmentLinkBuffer?.Dispose();
        startOffsetBuffer?.Dispose();
    }
}
