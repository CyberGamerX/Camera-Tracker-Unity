using UnityEngine;
using UnityEngine.UI;
using EDSDKLib;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;

public class Camera_test : MonoBehaviour
{
    public RawImage displayImage;

    private IntPtr camera;
    private IntPtr cameraList;

    private Texture2D liveTexture;

    public RenderTexture displayTexture;
    private bool isLiveViewRunning = false;

    public Material alterLook;

    public Camera mirrorCamera;

    // public Material blitMaterial; // Material to do shader bullshit

    void Start()
    {
        InitializeCamera();
        Debug.Log("started");
        StartCoroutine(LiveViewLoop());
    }

    void OnDestroy()
    {
        StopLiveView();
        Cleanup();
    }

    void InitializeCamera()
    {
        // Initialize EDSDK
        Debug.Log(EDSDK.EdsInitializeSDK() + " - Initialize SDK");

        // Get camera list
        Debug.Log(EDSDK.EdsGetCameraList(out cameraList) + " - Get Camera List");

        // Get first camera
        Debug.Log(EDSDK.EdsGetChildAtIndex(cameraList, 0, out camera) + " - Get Camera");

        // Open session
        Debug.Log(EDSDK.EdsOpenSession(camera) + " - Open Session");

        // Set Live View mode
        int liveViewMode = 1; // Enable Live View
        EDSDK.EdsSetPropertyData(camera, EDSDK.PropID_Evf_Mode, 0, sizeof(int), liveViewMode);

        // Set output device to PC
        int outputDevice = 3; // 1 = PC
        EDSDK.EdsSetPropertyData(camera, EDSDK.PropID_Evf_OutputDevice, 0, sizeof(int), outputDevice);

        // displayTexture = new RenderTexture(960, 640, 0);
        // displayTexture.Create();
    }

    IEnumerator LiveViewLoop()
    {
        isLiveViewRunning = true;

        // yield return new WaitForSeconds(5);

        while (isLiveViewRunning)
        {
            GetCameraDetails();
            DownloadAndDisplayLiveView();
            yield return new WaitForSeconds(1f/30f); // Aim for 15 FPS
        }
    }

    void DownloadAndDisplayLiveView()
    {
        // Create memory stream and image ref
        EDSDK.EdsCreateMemoryStream(0, out IntPtr stream);
        EDSDK.EdsCreateEvfImageRef(stream, out IntPtr evfImage);

        // Download Live View image
        uint err = EDSDK.EdsDownloadEvfImage(camera, evfImage);
        Debug.Log(err + " - Download Error");

        if (err == EDSDK.EDS_ERR_OK)
        {
            // Get pointer and size
            EDSDK.EdsGetPointer(stream, out IntPtr buffer);
            EDSDK.EdsGetLength(stream, out ulong length);

            // Copy into byte array
            byte[] data = new byte[(int)length]; // Cast to int
            Marshal.Copy(buffer, data, 0, (int)length);

            if (liveTexture == null)
                liveTexture = new Texture2D(1, 1, TextureFormat.RGB24, false);

            if (liveTexture.LoadImage(data))
            {
                // RenderTextToFile("LiveTexture.png", liveTexture);
                alterLook.mainTexture = liveTexture;

                displayImage.texture = displayTexture;

                // RenderTextToFile("Final Render.png", displayTexture);
            }
        }

        // Release native objects
        if (evfImage != IntPtr.Zero) EDSDK.EdsRelease(evfImage);
        if (stream != IntPtr.Zero) EDSDK.EdsRelease(stream);
    }

    void GetCameraDetails()
    {
        EdsLensInfo lensInfo = new EdsLensInfo();
        int size = Marshal.SizeOf(typeof(EdsLensInfo));
        IntPtr ptr = Marshal.AllocHGlobal(size);
        uint err = EDSDK.EdsGetPropertyData(camera, EDSDK.PropID_LensName, 0, size, ptr);

        if (err == EDSDK.EDS_ERR_OK)
        {
            lensInfo = Marshal.PtrToStructure<EdsLensInfo>(ptr);

            Debug.Log("📷 Lens Info:");
            Debug.Log($"- Min Focal Length: {lensInfo.MinFocalLength / 100.0f}mm");
            Debug.Log($"- Max Focal Length: {lensInfo.MaxFocalLength / 100.0f}mm");
            Debug.Log($"- Max Aperture @ Min Focal: f/{lensInfo.MaxApertureAtMinFocal / 100.0f}");
            Debug.Log($"- Max Aperture @ Max Focal: f/{lensInfo.MaxApertureAtMaxFocal / 100.0f}");
        }
        else
        {
            Debug.LogWarning($"Failed to get current focal length (may not be supported on this model). Error: 0x{err:X}");
        }

        Marshal.FreeHGlobal(ptr);
    }

    void StopLiveView()
    {
        isLiveViewRunning = false;

        // Disable Live View
        int off = 0;
        EDSDK.EdsSetPropertyData(camera, EDSDK.PropID_Evf_OutputDevice, 0, sizeof(int), off);
        EDSDK.EdsSetPropertyData(camera, EDSDK.PropID_Evf_Mode, 0, sizeof(int), off);
    }

    void Cleanup()
    {
        if (camera != IntPtr.Zero)
        {
            EDSDK.EdsCloseSession(camera);
            EDSDK.EdsRelease(camera);
        }

        if (cameraList != IntPtr.Zero)
            EDSDK.EdsRelease(cameraList);

        EDSDK.EdsTerminateSDK();
    }

    void RenderTextToFile(string filePath, RenderTexture image)
    {
        // Set the active RenderTexture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = image;

        // Create a new Texture2D with the same dimensions and format
        Texture2D tex = new Texture2D(displayTexture.width, displayTexture.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, displayTexture.width, displayTexture.height), 0, 0);
        tex.Apply();

        // Encode to PNG
        byte[] bytes = tex.EncodeToPNG();

        // Save to file
        File.WriteAllBytes(filePath, bytes);

        // Cleanup
        RenderTexture.active = currentRT;
        Destroy(tex); // Or DestroyImmediate if you're in editor scripts

        Debug.Log($"RenderTexture saved to {filePath}");
    }

    void RenderTextToFile(string filePath, Texture2D image)
    {
        // Encode to PNG
        byte[] bytes = image.EncodeToPNG();

        // Save to file
        File.WriteAllBytes(filePath, bytes);

        Debug.Log($"RenderTexture saved to {filePath}");
    }
}