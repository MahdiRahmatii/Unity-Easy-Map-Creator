///Developed By Mahdi Rahmati
///https://github.com/MahdiRahmatii/Unity-Easy-Map-Creator

using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Linq;

public class EasyMapCreator : MonoBehaviour
{
    [Serializable]
    public struct MapElement
    {
        [Header("General")]
        public GameObject Object;

        [Header("Texturing")]
        public bool useDefaultTexture;
        public Color ColorOnMap;

        [Header("Outline")]
        public int OutlineWidth;
        public Color OutlineColor;

        public Texture2D Texture { get; set; }
    }

    [Serializable]
    public struct MapSettings
    {
        [Tooltip("Path: Assets/'MapName'.png")]
        public string Name;
        public Vector2Int Size;
    }

    [Serializable]
    public struct MapCorners
    {
        public float Height;
        public Transform RightTop;
        public Transform LeftTop;
        public Transform RightButtom;
        public Transform LeftButtom;
    }

    [Header("Step 1 : Set 'name' and 'size'")]
    [SerializeField] private MapSettings m_mapSettings;

    [Header("Step 2 : Add elements to capture")]
    [SerializeField] private MapElement[] m_mapElements;

    [Header("Step 3 : Add the points to identify the capture area")]
    [SerializeField] private MapCorners m_mapCorners;

    private Camera m_camera;
    public bool isCapturing { get; private set; }

    [ContextMenu("Create Map")]
    public void Create()
    {
        if (m_mapElements.Length == 0)
        {
            Debug.LogError("Please add at least one element");
            return;
        }

        if (!m_mapCorners.RightTop || !m_mapCorners.LeftTop || !m_mapCorners.RightButtom || !m_mapCorners.LeftButtom)
        {
            Debug.LogError("Please Insert All Map Corners");
            return;
        }

        if (isCapturing)
            return;

        isCapturing = true;
        CreateCamera();
        StartCoroutine(Capture());
    }

    private void CreateCamera()
    {
        float cameraHeight = m_mapCorners.Height;
        m_camera = new GameObject("Creating The map... (Dont Delete This)").AddComponent<Camera>();
        m_camera.clearFlags = CameraClearFlags.Nothing;
        m_camera.orthographic = true;
        m_camera.nearClipPlane = 0.1f;
        m_camera.farClipPlane = cameraHeight * 2;
        m_camera.targetDisplay = 1;

        Transform[] objectsToLookAt = { m_mapCorners.RightTop, m_mapCorners.LeftTop, m_mapCorners.LeftButtom, m_mapCorners.RightButtom };

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
    }

    private IEnumerator Capture()
    {
        Debug.Log("Start Capturing...");

        for (int i = 0; i < m_mapElements.Length; i++)
        {
            for (int j = 0; j < m_mapElements.Length; j++)
                m_mapElements[j].Object.SetActive(j == i);

            CaptureObject(i);
            yield return null;
        }

        ApplyColors();
        Combine();
        DestroyImmediate(m_camera.gameObject);

        for (int i = 0; i < m_mapElements.Length; i++)
            m_mapElements[i].Object.SetActive(true);

        Debug.Log($"Done. {m_mapSettings.Name}.png was created in the Assets folder.");
        isCapturing = false;

        AssetDatabase.Refresh();
    }

    private void CaptureObject(int index)
    {
        MapElement element = m_mapElements[index];

        Debug.Log($"Capturing {element.Object.name}...");

        RenderTexture rt = CreateRenderTexture();
        m_camera.targetTexture = rt;
        m_camera.Render();
        m_camera.targetTexture = null;

        RenderTexture.active = rt;

        Texture2D tex2D = new Texture2D(rt.width, rt.height);
        tex2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex2D.Apply();
        m_mapElements[index].Texture = tex2D;

        RenderTexture.active = null;
    }

    private void ApplyColors()
    {
        Debug.Log("Apply Colors...");

        for (int i = 0; i < m_mapElements.Length; i++)
        {
            var element = m_mapElements[i];
            Texture2D texture = element.Texture;

            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    Color originalColor = texture.GetPixel(x, y);

                    if (originalColor.a > 0)
                    {
                        if (!element.useDefaultTexture)
                            texture.SetPixel(x, y, element.ColorOnMap);
                    }
                    else
                        texture.SetPixel(x, y, Color.clear);
                }
            }

            Texture2D outlinedTexture = ApplyOutline(texture, element.OutlineColor, element.OutlineWidth);
            m_mapElements[i].Texture = outlinedTexture;
        }
    }

    private void Combine()
    {
        Debug.Log("Creating Final Map...");

        Texture2D resultTexture = new Texture2D(m_mapSettings.Size.x, m_mapSettings.Size.y);

        for (int x = 0; x < resultTexture.width; x++)
        {
            for (int y = 0; y < resultTexture.height; y++)
            {
                Color blendedColor = m_mapElements.Select(element => element.Texture.GetPixel(x, y)).Aggregate(Color.clear, (acc, color) => acc + color);
                resultTexture.SetPixel(x, y, blendedColor);
            }
        }

        resultTexture.Apply();
        byte[] bytes = resultTexture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(Application.dataPath, $"{m_mapSettings.Name}.png"), bytes);
    }

    private RenderTexture CreateRenderTexture()
    {
        RenderTexture r = new RenderTexture(m_mapSettings.Size.x, m_mapSettings.Size.y, 1);
        r.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        r.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
        r.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat_S8_UInt;
        r.autoGenerateMips = false;
        return r;
    }

    static Texture2D ApplyOutline(Texture2D inputImage, Color outlineColor, int outlineWidth)
    {
        Texture2D outputImage = new Texture2D(inputImage.width, inputImage.height);
        Color[] ps = outputImage.GetPixels();

        for (int i = 0; i < ps.Length; i++)
            ps[i] = new Color(0, 0, 0, 0);

        outputImage.SetPixels(ps);
        outputImage.Apply();

        for (int y = 0; y < inputImage.height; y++)
        {
            for (int x = 0; x < inputImage.width; x++)
            {
                Color pixelColor = inputImage.GetPixel(x, y);

                if (pixelColor.a == 0)
                {
                    for (int i = -outlineWidth; i <= outlineWidth; i++)
                    {
                        for (int j = -outlineWidth; j <= outlineWidth; j++)
                        {
                            int newX = x + i;
                            int newY = y + j;

                            if (newX >= 0 && newX < inputImage.width && newY >= 0 && newY < inputImage.height)
                            {
                                Color neighborColor = inputImage.GetPixel(newX, newY);

                                if (neighborColor.a > 0)
                                {
                                    outputImage.SetPixel(x, y, outlineColor);
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                    outputImage.SetPixel(x, y, pixelColor);
            }
        }

        return outputImage;
    }

    private void OnDrawGizmos()
    {
        if (!m_mapCorners.RightTop || !m_mapCorners.LeftTop || !m_mapCorners.RightButtom || !m_mapCorners.LeftButtom)
            return;

        Vector3 highestPoint = Vector3.up * m_mapCorners.Height;
        Handles.color = Color.cyan * 2;

        Vector3[] corners = {
        m_mapCorners.RightTop.position,
        m_mapCorners.LeftTop.position,
        m_mapCorners.LeftButtom.position,
        m_mapCorners.RightButtom.position
        };

        Vector3[] topCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
            topCorners[i] = corners[i] + highestPoint;

        for (int i = 0; i < 4; i++)
        {
            Handles.DrawAAPolyLine(5, corners[i], corners[(i + 1) % 4]);
            Handles.DrawAAPolyLine(5, topCorners[i], topCorners[(i + 1) % 4]);
            Handles.DrawAAPolyLine(5, corners[i], topCorners[i]);
        }
    }
}


[CustomEditor(typeof(EasyMapCreator))]
public class MMapCreatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        #region Header
        GUI.color = Color.green;
        GUILayout.BeginVertical("Box");
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.fontSize = 20;
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // Add space to the left
        GUILayout.Label("Easy Map Creator", headerStyle, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace(); // Add space to the right
        GUILayout.EndHorizontal();
        GUI.color = Color.white;
        GUILayout.EndVertical();
        GUILayout.Space(10);
        #endregion

        base.OnInspectorGUI();

        EasyMapCreator mapCreator = (EasyMapCreator)target;

        GUILayout.Space(10);

        GUIStyle boldLabelStyle = new GUIStyle(EditorStyles.boldLabel);
        GUILayout.Label("Step 4 : Create Map", boldLabelStyle);

        if (GUILayout.Button(mapCreator.isCapturing ? "Please Wait..." : "Create Map",GUILayout.Height(30)))
            mapCreator.Create();
    }
}