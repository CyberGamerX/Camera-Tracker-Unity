using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class TrackingData : MonoBehaviour
{
    public String CameraDataPath;
    public String RawDataPath;
    public float normalSize = 0.1f;

    public transformAlgorithm processor;
    public List<PositionData> positions;
    public List<PositionData> imuPositions;
    public List<RawData> rawDatas;
    public int IMUSyncFrame;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void OnDrawGizmos()
    {
        if (positions == null || positions.Count < 2) return;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(positions[0].position * 10, 0.025f);

        for (int i = 0; i < positions.Count - 1; i++)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(positions[i].position * 10, positions[i + 1].position * 10);
            Gizmos.color = Color.white;
            Vector3 normal = positions[i].rotation * Vector3.forward;
            Gizmos.DrawLine(positions[i].position * 10, positions[i].position * 10 + normal.normalized * normalSize);
            Gizmos.color = Color.red;
            normal = positions[i].rotation * Vector3.right;
            Gizmos.DrawLine(positions[i].position * 10, positions[i].position * 10 + normal.normalized * normalSize);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(imuPositions[0].position * 10, 0.025f);

        for (int i = 0; i < imuPositions.Count - 1; i++)
        {
            if (i < IMUSyncFrame)
            {
                Gizmos.color = new Color(0.4f, 0, 0.4f);
            }
            else
            {
                Gizmos.color = new Color(1, 0, 1);
            }
            
            Gizmos.DrawLine(imuPositions[i].position * 10, imuPositions[i + 1].position * 10);

            Vector3 DrawPosition = imuPositions[i].position;
            //Vector3 DrawPosition = new Vector3(0, 0, 0.01f * i);

            Gizmos.color = Color.white;
            Vector3 normal = imuPositions[i].rotation * Vector3.forward;
            Gizmos.DrawLine(DrawPosition * 10, DrawPosition * 10 + normal.normalized * normalSize);
            Gizmos.color = Color.yellow;
            normal = imuPositions[i].rotation * Vector3.right;
            Gizmos.DrawLine(DrawPosition * 10, DrawPosition * 10 + normal.normalized * normalSize);
            Gizmos.color = Color.green;
            normal = imuPositions[i].rotation * Vector3.up;
            Gizmos.DrawLine(DrawPosition * 10, DrawPosition * 10 + normal.normalized * normalSize);
        }
    }

    public void loadJson()
    {
        if (string.IsNullOrEmpty(CameraDataPath))
        {
            Debug.LogWarning("CameraDataPath is not set.");
            return;
        }

        if (!File.Exists(CameraDataPath))
        {
            Debug.LogError($"File not found at path: {CameraDataPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(CameraDataPath);
            PositionList cameraData = JsonUtility.FromJson<PositionList>(json);
            positions = cameraData.positionDatas;
            Debug.Log("Camera data loaded successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading or parsing JSON: {e.Message}");
            return;
        }

        ConvertBlenderToUnity(positions);
    }

    public void loadRawData()
    {
        if (string.IsNullOrEmpty(RawDataPath))
        {
            Debug.LogWarning("RawDataPath is not set.");
            return;
        }

        if (!File.Exists(RawDataPath))
        {
            Debug.LogError($"File not found at path: {RawDataPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(RawDataPath);
            DataList rawData;
            rawData = JsonUtility.FromJson<DataList>(json);
            rawDatas = rawData.data;
            Debug.Log("Raw data loaded successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading or parsing JSON: {e.Message}");
            return;
        }

        int syncFrame;
        imuPositions = processor.ProcessIMU(rawDatas, out syncFrame);
        IMUSyncFrame = syncFrame;
        ConvertIMUToUnity(imuPositions);

        ApplyIMUOffset(positions, imuPositions, IMUSyncFrame);
    }

    public static void ApplyIMUOffset(List<PositionData> referencePositions, List<PositionData> imuPositions, int imuSyncFrame)
{
    if (referencePositions == null || imuPositions == null ||
        referencePositions.Count == 0 || imuPositions.Count <= imuSyncFrame)
    {
        Debug.LogError("Invalid input to ApplyIMUOffset");
        return;
    }

    // Reference position and rotation
    Vector3 referencePosition = referencePositions[0].position;
    Quaternion referenceRotation = referencePositions[0].rotation;

    Vector3 imuReferencePosition = imuPositions[imuSyncFrame].position;
    Quaternion imuReferenceRotation = imuPositions[imuSyncFrame].rotation;

    // Compute rotation and position offsets
    Quaternion offsetRotation = referenceRotation * Quaternion.Inverse(imuReferenceRotation);
    Vector3 offsetPosition = referencePosition - (offsetRotation * imuReferencePosition);

    // Apply the transformation to all IMU data
    for (int i = 0; i < imuPositions.Count; i++)
    {
        PositionData original = imuPositions[i];

        PositionData transformed = new PositionData
        {
            rotation = offsetRotation * original.rotation,
            position = offsetRotation * original.position + offsetPosition
        };

        imuPositions[i] = transformed;
    }
}

    public static void ConvertBlenderToUnity(List<PositionData> dataList)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            PositionData data = dataList[i];

            // Flip position: (x, y, z) -> (x, z, -y)
            Vector3 p = data.position;
            data.position = new Vector3(p.x, p.z, p.y);

            // Flip rotation: (x, y, z, w) -> (x, z, -y, w)
            Quaternion q = data.rotation;
            data.rotation = Quaternion.Euler(-90, 0, 180) * new Quaternion(q.x, q.z, q.y, q.w);

            // Save the modified struct back into the list
            dataList[i] = data;
        }
    }

    public static void ConvertIMUToUnity(List<PositionData> dataList)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            PositionData data = dataList[i];

            Vector3 p = data.position;
            //data.position = new Vector3(p.z, -p.x, -p.y);
            //data.position = new Vector3(-p.x, p.z, -p.y);
            data.position = p;

            Quaternion q = data.rotation;
            //data.rotation = new Quaternion(-q.y, -q.z, q.x, q.w);

            Quaternion m = new Quaternion(-0.5f, 0.5f, -0.5f, 0.5f);

            data.rotation = Quaternion.Euler(180f, 0f, 0f) * (m * q * Quaternion.Inverse(m));

            // Save the modified struct back into the list
            dataList[i] = data;
        }
    }
}
