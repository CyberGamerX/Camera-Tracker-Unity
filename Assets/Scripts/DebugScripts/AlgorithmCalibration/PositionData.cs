using UnityEngine;
using System;
using System.Data;
using UnityEngine.UIElements;

[Serializable]
public struct PositionData
{
    public Vector3 position;
    public Quaternion rotation;

    public int frame;
}