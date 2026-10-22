using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;

public abstract class transformAlgorithm : MonoBehaviour
{
    public abstract List<PositionData> ProcessIMU(List<RawData> input, out int syncFrame);
}