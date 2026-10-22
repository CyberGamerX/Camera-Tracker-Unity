using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using AHRS;
using UnityEngine.UIElements;


public class MadwickSimple : transformAlgorithm
{
    public override List<PositionData> ProcessIMU(List<RawData> input, out int syncFrame)
    {
        float frequency = 104;

        MadgwickAHRS AHRS = new MadgwickAHRS(frequency, 10f);

        List<Quaternion> rotations = new List<Quaternion>();
        List<Vector3> positions = new List<Vector3>();

        Vector3 speed = new Vector3();
        Vector3 CurrentPosition = new Vector3();

        int calibrateCycle = 0;
        Vector3 fixedOffset = Vector3.zero;
        Vector3 fixedRotationOffset = Vector3.zero;
        float calibrationsRequired = 200;

        foreach (RawData p in input)
        {
            Vector3 accel = new Vector3(p.accel.x, p.accel.y, p.accel.z);
            Vector3 gyro = new Vector3(p.gyro.x, p.gyro.y, p.gyro.z);

            ApplyGate(ref accel, 0.01f, 0.01f, 0.01f);
            ApplyGate(ref gyro, 0.00f, 0.00f, 0.01f);

            if (calibrateCycle > calibrationsRequired)
            {
                gyro -= fixedRotationOffset;
            }

            AHRS.Update(gyro.x * Mathf.Deg2Rad, gyro.y * Mathf.Deg2Rad, gyro.z * Mathf.Deg2Rad, accel.x, accel.y, accel.z);
            float[] q = AHRS.Quaternion;

            Quaternion newRotation = Quaternion.Inverse(new Quaternion(q[0], q[1], q[2], q[3]));
            rotations.Add(newRotation);

            if (calibrateCycle < calibrationsRequired)
            {
                fixedOffset += p.accel;
                fixedRotationOffset += p.gyro;
            }
            else if (calibrateCycle == calibrationsRequired)
            {
                fixedOffset /= calibrationsRequired;
                fixedOffset -= newRotation * new Vector3(0, -1, 0);
                fixedRotationOffset /= calibrationsRequired;
            }
            else
            {
                Vector3 correctedAccel = p.accel - fixedOffset;
                Vector3 gravityCorrected = (newRotation * new Vector3(0, 0, 1));

                Debug.Log($"Accel {p.accel.ToString("F8")}  - fixedOffset {fixedOffset.ToString("F8")}");
                Debug.Log($"correctedAccel {correctedAccel.ToString("F8")}  -  gravity {gravityCorrected.ToString("F8")}");
                Debug.Log($"speed {(correctedAccel - gravityCorrected).ToString("F8")}");

                Vector3 FrameAccel = ((correctedAccel - gravityCorrected) / frequency) / 9.81f;

                Debug.Log(FrameAccel.ToString("F8") + "  Frame Accel");

                speed += FrameAccel;

                if ((correctedAccel - gravityCorrected).magnitude < 1)
                {
                    speed = Vector3.zero;
                }

                CurrentPosition += speed / frequency;

                // if (p.start)
                // {
                //     speed = Vector3.zero;
                // }
            }

            positions.Add(CurrentPosition);
            calibrateCycle += 1;
        }

        List<PositionData> output = new List<PositionData>();
        ulong nextFrame = input[0].timecode;
        int currentFrame = 0;
        syncFrame = 0;
        for (int i = 0; i < input.Count; i++)
        {
            if (input[i].timecode > nextFrame)
            {
                PositionData frameData = new PositionData();
                frameData.rotation = rotations[i];
                frameData.position = positions[i];
                if (input[i].start)
                {
                    syncFrame = currentFrame;
                }

                nextFrame += 33;
                currentFrame += 1;

                output.Add(frameData);
            }
        }

        return output;
    }

    private void ApplyGate(ref Vector3 input, float xGate, float yGate, float zGate)
    {
        if (Mathf.Abs(input.x) < xGate)
        {
            input.x = 0;
        }
        if (Mathf.Abs(input.y) < yGate)
        {
            input.y = 0;
        }
        if (Mathf.Abs(input.z) < zGate)
        {
            input.z = 0;
        }
    }
}