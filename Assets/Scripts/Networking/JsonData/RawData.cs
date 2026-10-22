using UnityEngine;
using System;
using System.Data;
using UnityEngine.UIElements;

[Serializable]
public struct RawData
{
    public Vector3 accel;
    public Vector3 gyro;

    public ulong timecode;
    public bool start;
}