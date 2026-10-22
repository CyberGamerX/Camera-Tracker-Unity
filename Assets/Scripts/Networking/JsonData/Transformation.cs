using UnityEngine;
using System;
using System.Data;
using UnityEngine.UIElements;

[Serializable]
public struct Transformation
{
    public float x, y, z;
    public float quatX, quatY, quatZ, quatW;

    public ulong timecode; // in Milliseconds
}