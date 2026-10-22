using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform camera;

    public NetworkController networkController;

    public ulong delayedReading = 0;
    void Start()
    {
        
    }

    void FixedUpdate()
    {
        Transformation p = networkController.getDelayedTransform(delayedReading);
        Vector3 position = new Vector3(p.x, p.y, p.z);

        //camera.transform.position = position;

        // Orientation as quaternion (Madgwick gives a right-handed quaternion)
        Quaternion q = new Quaternion(p.quatX, p.quatY, p.quatZ, p.quatW);

        // Convert from right-handed to Unity's left-handed system (flip Z)
        Quaternion unityQuat = new Quaternion(-q.y, -q.z, q.x, q.w);

        camera.transform.rotation = unityQuat;
    }
}
