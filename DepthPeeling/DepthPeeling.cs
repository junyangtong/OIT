using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SocialPlatforms;

[RequireComponent(typeof(Camera))]  //挂载在相机
public class DepthPeeling : MonoBehaviour
{
    public enum RT  //在UI上创建一个可选项
    {
        Depth = 0,
        Color = 1,
    }

    [Range(1, 6)] public int depthMax = 3;
    public RT rt;
    public Shader MRTShader;
    public Shader finalClipsShader;

    private Camera sourceCamera;
    private Camera tempCamera;
 
    public RenderTexture[] rts;
    public RenderTexture rtTemp;
    private RenderBuffer[] colorBuffers; //RenderBuffer:RT 的颜色或深度缓冲区部分。
    private RenderTexture depthBuffer;
    public RenderTexture finalClips; // 这里显存占用比较大 如果要优化 可以考虑 从前往后叠加 在一张RT上做累积
    private Material finalClipsMat;
    [Range(0f, 1f)]
    public float DepthPeelingAlpha = 1.0f;

    void Start()
    {   //创建临时相机
        this.sourceCamera = this.GetComponent<Camera>();
        tempCamera = new GameObject().AddComponent<Camera>();   
        tempCamera.enabled = false;
 
        finalClipsMat = new Material(finalClipsShader);
        //创建RT
        this.rts = new RenderTexture[2]     
        {
            new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 0, RenderTextureFormat.RFloat),
            new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 0, RenderTextureFormat.Default)
        };
         
        rts[0].Create();    //.Create 实际创建 RenderTexture
        rts[1].Create();	
        finalClips = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 0, RenderTextureFormat.Default);

        finalClips.dimension = TextureDimension.Tex2DArray; //渲染纹理的维度（类型）
        finalClips.volumeDepth = 6; //3D 渲染纹理的体积范围或数组纹理的切片数
        finalClips.Create();

        Shader.SetGlobalTexture("FinalClips", finalClips);
        rtTemp = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 0, RenderTextureFormat.RFloat);
        rtTemp.Create();

        Shader.SetGlobalTexture("DepthRendered", rtTemp);
        colorBuffers = new RenderBuffer[2] {rts[0].colorBuffer, rts[1].colorBuffer};    //colorBuffer:渲染纹理的颜色缓冲区（只读）

        depthBuffer = new RenderTexture(sourceCamera.pixelWidth, sourceCamera.pixelHeight, 16, RenderTextureFormat.Depth);
        depthBuffer.Create();
    }
    private void Update() {
        DepthPeelingAlpha += Time.deltaTime * 0.1f;
        if(DepthPeelingAlpha > 1.0f)
        {
            DepthPeelingAlpha = 0.0f;
        }
        
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination) //Unity 在摄像机完成渲染后调用的事件函数，用于修改摄像机的最终图像
    {
        tempCamera.CopyFrom(sourceCamera);
        tempCamera.clearFlags = CameraClearFlags.SolidColor;
        tempCamera.backgroundColor = Color.clear;
        tempCamera.SetTargetBuffers(colorBuffers, depthBuffer.depthBuffer);
        tempCamera.cullingMask = 1 << LayerMask.NameToLayer("clipRender");  //设置目标透明物体的Layer

        //遍历5层depth写入MRT结果
        for (int i = 0; i < depthMax; i++)
        {
            Graphics.Blit(rts[0], rtTemp);// 这里不知道为什么需要复制出来 不能直接用rts【0】 当时我判断是不可同时读写所以复制一份就可以了
            Shader.SetGlobalFloat("DepthPeelingAlpha",DepthPeelingAlpha);
            Shader.SetGlobalInt("DepthRenderedIndex", i);
            tempCamera.RenderWithShader(MRTShader, "");
            Graphics.CopyTexture(rts[1], 0, 0, finalClips, i, 0);
        }
            Graphics.Blit(source, destination, finalClipsMat);
    }

    void OnDestroy()
    {
        rts[0].Release();
        rts[1].Release();
        finalClips.Release();
        rtTemp.Release();

        depthBuffer.Release();
    }
}