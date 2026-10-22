using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TrackingData))]
class TrackingDataInspector : Editor
{

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TrackingData trackingData = (TrackingData)target;

        if (GUILayout.Button("Load Tracked Data"))
        {
            trackingData.loadJson();
        }

        if (GUILayout.Button("Load Raw"))
        {
            trackingData.loadRawData();
        }
    }
}