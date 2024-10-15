///Developed By Mahdi Rahmati
///https://github.com/MahdiRahmatii/Unity-Easy-Map-Creator

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class EasyMapCreator : MonoBehaviour
{
    #region Parameters
    [Serializable]
    public struct MapCorners
    {
        public Transform RightTop;
        public Transform LeftTop;
        public Transform RightBottom;
        public Transform LeftBottom;
        public float RenderHeight;
    }


    public enum TextureMode
    {
        UseDefaultTexture,
        UseColor
    }
    [Serializable]
    public struct MapElement
    {
        [Header("Renderer")]
        public GameObject Object;

        [Header("Outline")]
        [Range(0, 10)] public int OutlineWidth;
        public Color OutlineColor;

        [Header("Texturing")]
        public TextureMode TexturingMode;
        public Color ElementColorOnMap;

        public Texture2D Texture { get; set; }
    }


    [Serializable]
    public struct ExportSettings
    {
        public enum Texture2DSize
        {
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048
        }

        [Tooltip("Path: Assets/ 'MapName' .png")]
        public string FileNameInAssets;
        public Texture2DSize TextureSize;
    }


    public enum ColorMode
    {
        BlendColors,
        OverlapColors_OrderIsImportant
    }


    public bool isCapturing { get; private set; }
    [Header("Step1 : Assign corner points for capturing area"), SerializeField] private MapCorners m_mapCorners;
    [Space, Header("Step2 : Assign elements to set color and outline"), SerializeField] private MapElement[] m_mapElements;
    [SerializeField] private ColorMode m_colorMode = ColorMode.OverlapColors_OrderIsImportant;
    [Space, Header("Step3 : Set the map texture name and size"), SerializeField] private ExportSettings m_exportSettings;
    private Camera m_camera;
    #endregion


    #region 0 : Start Capturing Process
    private bool IsParametersChecked()
    {
        if (!m_mapCorners.RightTop || !m_mapCorners.LeftTop || !m_mapCorners.RightBottom || !m_mapCorners.LeftBottom)
        {
            Log("Please assign all corners of the map", isError: true);
            return false;
        }

        if (m_mapElements.Length == 0)
        {
            Log("Please add at least one element", isError: true);
            return false;
        }

        if (string.IsNullOrEmpty(m_exportSettings.FileNameInAssets))
        {
            Log("Please enter a name for the texture file in the Export Settings", isError: true);
            return false;
        }

        if (m_exportSettings.TextureSize == null)
        {
            Log("Please enter the texture size in the Export Settings", isError: true);
            return false;
        }

        return true;
    }

    public void StartCapturing()
    {
        if (isCapturing) return;
        if (!IsParametersChecked()) return;
        isCapturing = true;
        CreateCamera();
    }
    #endregion


    #region 1 : Create Camera
    private void CreateCamera()
    {
        float cameraHeight = m_mapCorners.RenderHeight;
        m_camera = new GameObject("Capturing the map... (Don't Delete This)").AddComponent<Camera>();
        m_camera.clearFlags = CameraClearFlags.Nothing;
        m_camera.orthographic = true;
        m_camera.nearClipPlane = 0.1f;
        m_camera.farClipPlane = cameraHeight * 2;
        m_camera.targetDisplay = 1;

        Transform[] objectsToLookAt = { m_mapCorners.RightTop, m_mapCorners.LeftTop, m_mapCorners.LeftBottom, m_mapCorners.RightBottom };

        Bounds bounds = new Bounds(objectsToLookAt[0].position, Vector3.zero);
        foreach (Transform objTransform in objectsToLookAt)
            bounds.Encapsulate(objTransform.position);

        float margin = 1.0f;
        float cameraSize = Mathf.Max(bounds.size.x / 2, bounds.size.y / 2) + margin;
        m_camera.orthographicSize = cameraSize;

        Vector3 centerPoint = Vector3.zero;
        foreach (Transform objTransform in objectsToLookAt)
            centerPoint += objTransform.position;

        centerPoint /= objectsToLookAt.Length;

        m_camera.transform.position = new Vector3(centerPoint.x, centerPoint.y + cameraHeight, centerPoint.z);
        m_camera.transform.LookAt(centerPoint, Vector3.up);

        StartCoroutine(Capture());
    }
    #endregion


    #region 2 : Start Capturing The Map
    private IEnumerator Capture()
    {
        Log("Start Capturing...");

        foreach (var element in m_mapElements)
            element.Object.SetActive(false);

        for (int i = 0; i < m_mapElements.Length; i++)
        {
            m_mapElements[i].Object.SetActive(true);
            CaptureObject(i);
            m_mapElements[i].Object.SetActive(false);
            yield return null;
        }

        ApplyColors();
    }
    #endregion


    #region 3 : Capture Objects
    private void CaptureObject(int index)
    {
        Log($"Capturing {m_mapElements[index].Object.name}...");

        RenderTexture renderTex = CreateRenderTexture();
        
        m_camera.targetTexture = renderTex;
        m_camera.Render();
        m_camera.targetTexture = null;

        RenderTexture.active = renderTex;
        Texture2D texture2D = new Texture2D(renderTex.width, renderTex.height);
        texture2D.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;

        m_mapElements[index].Texture = texture2D;
    }

    private RenderTexture CreateRenderTexture()
    {
        RenderTexture renderTex = new RenderTexture((int)m_exportSettings.TextureSize, (int)m_exportSettings.TextureSize, 1);
        renderTex.dimension = TextureDimension.Tex2D;
        renderTex.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        renderTex.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
        renderTex.autoGenerateMips = false;
        return renderTex;
    }
    #endregion


    #region 4 : Apply Colors And Outline
    private void ApplyColors()
    {
        Log("Apply Colors...");

        for (int i = 0; i < m_mapElements.Length; i++)
        {
            MapElement element = m_mapElements[i];
            Texture2D texture = element.Texture;

            if (element.TexturingMode == TextureMode.UseColor)
            {
                for (int x = 0; x < texture.width; x++)
                    for (int y = 0; y < texture.height; y++)
                    {
                        bool haveColor = texture.GetPixel(x, y).a > 0;
                        texture.SetPixel(x, y, haveColor ? element.ElementColorOnMap : Color.clear);
                    }
            }

            Texture2D outlinedTexture = element.OutlineWidth > 0 ? ApplyOutline(texture, element.OutlineColor, element.OutlineWidth) : texture;
            m_mapElements[i].Texture = outlinedTexture;
        }

        Combine();
    }

    private Texture2D ApplyOutline(Texture2D inputImage, Color outlineColor, int outlineWidth)
    {
        int width = inputImage.width;
        int height = inputImage.height;
        Color[] originalPixels = inputImage.GetPixels();
        Color[] outlinedPixels = new Color[originalPixels.Length];
        Array.Copy(originalPixels, outlinedPixels, originalPixels.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (originalPixels[y * width + x].a == 0)
                {
                    bool shouldOutline = false;
                    for (int i = -outlineWidth; i <= outlineWidth && !shouldOutline; i++)
                    {
                        for (int j = -outlineWidth; j <= outlineWidth; j++)
                        {
                            int neighborX = x + i;
                            int neighborY = y + j;

                            if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                            {
                                if (originalPixels[neighborY * width + neighborX].a > 0)
                                {
                                    shouldOutline = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldOutline)
                        outlinedPixels[y * width + x] = outlineColor;
                }
            }
        }

        Texture2D outputImage = new Texture2D(width, height);
        outputImage.SetPixels(outlinedPixels);
        outputImage.Apply();
        return outputImage;
    }
    #endregion


    #region 5 : Combine Texture
    private void Combine()
    {
        Texture2D resultTexture = new Texture2D((int)m_exportSettings.TextureSize, (int)m_exportSettings.TextureSize);

        for (int x = 0; x < resultTexture.width; x++)
        {
            for (int y = 0; y < resultTexture.height; y++)
            {
                if (m_colorMode == ColorMode.BlendColors)
                {
                    Color blendedColor = m_mapElements.Select(element => element.Texture.GetPixel(x, y)).Aggregate(Color.clear, (acc, color) => acc + color);
                    resultTexture.SetPixel(x, y, blendedColor);
                }
                else
                {
                    Color overrideColor = Color.clear;
                    foreach (var element in m_mapElements)
                    {
                        Color elementColor = element.Texture.GetPixel(x, y);
                        if (elementColor.a > 0)
                        {
                            overrideColor = elementColor;
                            break;
                        }
                    }
                    resultTexture.SetPixel(x, y, overrideColor);
                }
            }
        }

        resultTexture.Apply();
        byte[] bytes = resultTexture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(Application.dataPath, $"{m_exportSettings.FileNameInAssets}.png"), bytes);

        DestroyImmediate(m_camera.gameObject);
        FinishCapturing();
    }
    #endregion


    #region 6 : Finish
    private void FinishCapturing()
    {
        foreach (var element in m_mapElements)
            element.Object.SetActive(true);

        Log($"{m_exportSettings.FileNameInAssets}.png was created in the Assets folder.");
        isCapturing = false;

        AssetDatabase.Refresh();
    }
    #endregion


    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        if (!m_mapCorners.RightTop || !m_mapCorners.LeftTop || !m_mapCorners.RightBottom || !m_mapCorners.LeftBottom)
            return;

        Vector3 highestPoint = Vector3.up * m_mapCorners.RenderHeight;
        Handles.color = Color.cyan * 2;

        Vector3[] corners = {
            m_mapCorners.RightTop.position,
            m_mapCorners.LeftTop.position,
            m_mapCorners.LeftBottom.position,
            m_mapCorners.RightBottom.position
        };

        Vector3[] topCorners = new Vector3[4];

        for (int i = 0; i < 4; i++)
            topCorners[i] = corners[i] + highestPoint;

        for (int i = 0; i < 4; i++)
        {
            Handles.DrawAAPolyLine(5, corners[i], corners[(i + 1) % 4]);
            Handles.DrawDottedLine(topCorners[i], topCorners[(i + 1) % 4], 10);
            Handles.DrawDottedLine(corners[i], topCorners[i], 10);
        }
    }
    #endregion


    #region Console log handler
    private void Log(string message, bool isError = false)
    {
        if (isError) Debug.LogError($"<color=cyan><b>EasyMapCreator:</b></color> {message}");
        else Debug.Log($"<color=cyan><b>EasyMapCreator:</b></color> {message}");
    }
    #endregion
}
#endif