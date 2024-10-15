///Developed By Mahdi Rahmati
///https://github.com/MahdiRahmatii/Unity-Easy-Map-Creator

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EasyMapCreator))]
public class EasyMapCreatorEditor : Editor
{
    private GUIStyle m_headerStyle;
    private GUIStyle m_boldLabelStyle;


    #region On Inspector GUI
    public override void OnInspectorGUI()
    {
        DrawHeader("Easy Map Creator");
        base.OnInspectorGUI();
        DrawButton();
        GUI.color = Color.gray;
        GUILayout.Label("Please deactivate the objects you don't want to render until the process is complete.");
    }
    #endregion


    #region Header
    private void DrawHeader(string title)
    {
        m_headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 20
        };

        GUI.color = Color.cyan;
        GUILayout.BeginVertical("Box");
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // Add space to the left
        GUILayout.Label(title, m_headerStyle);
        if (GUILayout.Button("?", GUILayout.Width(18), GUILayout.Height(25)))
            Application.OpenURL("https://github.com/MahdiRahmatii/Unity-Easy-Map-Creator");
        GUILayout.FlexibleSpace(); // Add space to the right
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUI.color = Color.white;
        GUILayout.Space(10);
    }
    #endregion


    #region Create Button
    private void DrawButton()
    {
        EasyMapCreator mapCreator = (EasyMapCreator)target;

        GUILayout.Space(15);
        m_boldLabelStyle = new GUIStyle(EditorStyles.boldLabel);
        GUILayout.Label("Step 4 : Create The Map", m_boldLabelStyle);
        GUI.color = Color.cyan;

        if (mapCreator.isCapturing) GUILayout.Label("Please Wait...", m_boldLabelStyle);
        else
        {
            if (GUILayout.Button("Create Map", GUILayout.Height(30)))
                mapCreator.StartCapturing();
        }
    }
    #endregion
}
#endif