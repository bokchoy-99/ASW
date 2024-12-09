using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TimewarpRenderPassFeature : ScriptableRendererFeature
{
    class TimewarpRenderPass : ScriptableRenderPass
    {
        private int mSizex;
        private int mSizey;
        private int mWidth = 256;
        private int mHeight = 256;
        private RenderTargetIdentifier currentColorBuffer;

        private RenderTexture previousColorBuffer;
        private RenderTexture _previousColorBuffer;

        private RenderTexture previousMBuffer;
        private RenderTexture previousDBuffer;

        private Matrix4x4 previousViewMatrix;
        private Matrix4x4 previousProjectionMatrix;

        private Material material;
        private List<Mesh> meshes1 = new List<Mesh>();


        //---需整合代码---
        //mSize表示网格密度，1时网格密度等同于分辨率，需要保持网格密度和运动矢量大小一致
        public TimewarpRenderPass()
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            this.mSizex = (int)Math.Ceiling(screenWidth / (double)mWidth);
            this.mSizey = (int)Math.Ceiling(screenHeight / (double)mHeight);
            this.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            Shader shader = Shader.Find("Custom/TimewarpShader");
            if (shader == null)
            {
                Debug.LogError("Shader 'Custom/TimewarpShader' not found.");
                return;
            }
            material = new Material(shader);

            CreateFullscreenMesh();
        }

        private void CreateFullscreenMesh()
        {
            int gridWidth = mWidth;
            int gridHeight = mHeight;
            float quadSizeX = 2.0f / gridWidth;  // Scale from -1 to 1 in NDC
            float quadSizeY = 2.0f / gridHeight;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();

            int y = 0;
            int dy = 0;
            int sum = 0;
            Mesh mesh = new Mesh();

            while (y <= gridHeight + 1)
            {
                dy++;
                sum += gridWidth + 2;
                float y0;
                if (y == 0) y0 = -1;
                else if (y == gridHeight + 1) y0 = 1;
                else y0 = y * quadSizeY - 1 - 0.5f * quadSizeY;

                for (int x = 0; x <= gridWidth + 1; x++)
                {
                    float x0;
                    if (x == 0) x0 = -1;
                    else if (x == gridWidth + 1) x0 = 1;
                    else x0 = x * quadSizeX - 1 - 0.5f * quadSizeX;
                    // Add vertices for the quad
                    vertices.Add(new Vector3(x0, y0, 0));
                    uvs.Add(new Vector2((x0 + 1.0f) / 2, 1.0f - (y0 + 1.0f) / 2));
                }

                if (sum + gridWidth > 65000)
                {
                    for (int yy = 0; yy < dy - 1; yy++)
                    {
                        for (int x = 0; x <= gridWidth; x++)
                        {
                            int i = x + yy * (gridWidth + 2);
                            indices.Add(i + 1);
                            indices.Add(i);
                            indices.Add(i + gridWidth + 2);

                            indices.Add(i + 1);
                            indices.Add(i + gridWidth + 2);
                            indices.Add(i + gridWidth + 3);
                        }
                    }
                    mesh.vertices = vertices.ToArray();
                    mesh.uv = uvs.ToArray();
                    mesh.triangles = indices.ToArray();
                    meshes1.Add(mesh);
                    mesh = new Mesh();
                    vertices.Clear();
                    uvs.Clear();
                    indices.Clear();
                    sum = 0;
                    dy = 0;
                }
                else
                    y++;
            }
            if (dy > 1)
            {
                for (int yy = 0; yy < dy - 1; yy++)
                {
                    for (int x = 0; x <= gridWidth; x++)
                    {
                        int i = x + yy * (gridWidth + 2);
                        indices.Add(i + 1);
                        indices.Add(i);
                        indices.Add(i + gridWidth + 2);

                        indices.Add(i + 1);
                        indices.Add(i + gridWidth + 2);
                        indices.Add(i + gridWidth + 3);
                    }
                }
                mesh.vertices = vertices.ToArray();
                mesh.uv = uvs.ToArray();
                mesh.triangles = indices.ToArray();
                meshes1.Add(mesh);
            }
        }
        //---需整合代码---



        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (previousColorBuffer == null)
            {
                //RenderingUtils.ReAllocateIfNeeded();
                previousColorBuffer = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height, 0, RenderTextureFormat.ARGB32);
                //_previousColorBuffer = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height, 32, RenderTextureFormat.ARGB32);
                _previousColorBuffer = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height, 0, RenderTextureFormat.ARGB32);
                previousMBuffer = RenderTexture.GetTemporary(mWidth, mHeight, 0, RenderTextureFormat.ARGBHalf);
                previousDBuffer = RenderTexture.GetTemporary(mWidth, mHeight, 0, RenderTextureFormat.ARGBHalf);
                Debug.Log("O Try to create");
                if (previousColorBuffer == null)
                    Debug.Log("O Why NULL???");
            }
        }



        //---需整合代码---
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("TimewarpPass");

            var cameraTarget = renderingData.cameraData.renderer.cameraColorTarget;
            Camera camera = renderingData.cameraData.camera;
            //此处获取上一个原始帧的最终渲染纹理和运动矢量纹理，以及VP矩阵。
            if (Time.frameCount % 2 == 1)
            {
                //如果已经保存了上一个原始帧的最终渲染纹理和运动矢量纹理，跳过这一段

                currentColorBuffer = renderingData.cameraData.renderer.cameraColorTarget;
                //cmd.CopyTexture(currentColorBuffer, previousColorBuffer);
                cmd.Blit(currentColorBuffer, previousColorBuffer);
                var currentMBuffer = Shader.GetGlobalTexture("_MotionVectorTexture");
                var currentDBuffer = Shader.GetGlobalTexture("_CameraDepthTexture");
                cmd.Blit(currentMBuffer, previousMBuffer);
                cmd.Blit(currentDBuffer, previousDBuffer);

                //获取VP矩阵。如果通过GetProjectionMatrix获取P矩阵，需要一些额外变换
                previousViewMatrix = renderingData.cameraData.GetViewMatrix();
                previousProjectionMatrix = renderingData.cameraData.GetProjectionMatrix();
                Matrix4x4 flipYMatrix = Matrix4x4.Scale(new Vector3(1, -1, -1));
                previousProjectionMatrix = flipYMatrix * previousProjectionMatrix;
                previousProjectionMatrix[2, 2] = (previousProjectionMatrix[2, 2] - 1) / 2;
                previousProjectionMatrix[2, 3] = previousProjectionMatrix[2, 3] / 2;
            }
            else
            { }

            //生成帧。需要提供previousProjectionMatrix、previousViewMatrix、previousColorBuffer、previousMDBuffer(运动矢量纹理)，结果被保存在_previousColorBuffer
            Matrix4x4 previoustransformMatrix;
            previoustransformMatrix = previousProjectionMatrix * previousViewMatrix;
            previoustransformMatrix = previoustransformMatrix.inverse;

            Matrix4x4 transformMatrix;
            Matrix4x4 ViewMatrix = renderingData.cameraData.GetViewMatrix();
            Matrix4x4 ProjectionMatrix = renderingData.cameraData.GetProjectionMatrix();
            Matrix4x4 flipYMatrix1 = Matrix4x4.Scale(new Vector3(1, -1, -1));
            ProjectionMatrix = flipYMatrix1 * previousProjectionMatrix;
            ProjectionMatrix[2, 2] = (previousProjectionMatrix[2, 2] - 1) / 2;
            ProjectionMatrix[2, 3] = previousProjectionMatrix[2, 3] / 2;
            transformMatrix = ProjectionMatrix * ViewMatrix;


            material.SetTexture("_PreviousColorTex", previousColorBuffer);
            material.SetTexture("_PreviousMTex", previousMBuffer);
            material.SetTexture("_PreviousDTex", previousDBuffer);
            material.SetVector("_TransformMatrix0", previoustransformMatrix.GetRow(0));
            material.SetVector("_TransformMatrix1", previoustransformMatrix.GetRow(1));
            material.SetVector("_TransformMatrix2", previoustransformMatrix.GetRow(2));
            material.SetVector("_TransformMatrix3", previoustransformMatrix.GetRow(3));

            material.SetVector("_TransformMatrix10", transformMatrix.GetRow(0));
            material.SetVector("_TransformMatrix11", transformMatrix.GetRow(1));
            material.SetVector("_TransformMatrix12", transformMatrix.GetRow(2));
            material.SetVector("_TransformMatrix13", transformMatrix.GetRow(3));

            material.SetFloat("_Width", 1.0f / (mWidth));
            material.SetFloat("_Height", 1.0f / (mHeight));

            material.SetFloat("_near", camera.nearClipPlane);
            material.SetFloat("_far", camera.farClipPlane);

            cmd.SetRenderTarget(_previousColorBuffer, _previousColorBuffer);
            cmd.ClearRenderTarget(true, true, Color.clear);
            foreach (Mesh mesh in meshes1)
            {
                cmd.DrawMesh(mesh, Matrix4x4.identity, material);
            }

            Shader.SetGlobalTexture("_ExtraFrame", _previousColorBuffer);

            if (Time.frameCount % 2 != 1)
            {
                cmd.Blit(_previousColorBuffer, cameraTarget);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        //---需整合代码---



        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
        }
    }

    TimewarpRenderPass m_ScriptablePass;
    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new TimewarpRenderPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (camera.CompareTag("MainCamera"))
            renderer.EnqueuePass(m_ScriptablePass);
    }
}


