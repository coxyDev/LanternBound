using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Unity6CameraValidator : MonoBehaviour
{
    [ContextMenu("Validate Camera Setup")]
    public void ValidateCameraSetup()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("? No Camera component found!");
            return;
        }

        Debug.Log("=== UNITY 6 CAMERA VALIDATION ===");

        // Check camera projection
        Debug.Log($"Projection: {cam.orthographic} {(cam.orthographic ? "? Orthographic" : "? Should be Orthographic for 2D")}");

        // Check URP renderer
        var pipelineAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (pipelineAsset != null)
        {
            Debug.Log("? URP Pipeline Asset assigned");

            // Check camera renderer override
            var cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                Debug.Log($"Camera Renderer: {(cameraData.scriptableRenderer != null ? "? Custom Renderer" : "?? Default Renderer")}");
                Debug.Log($"Render Type: {cameraData.renderType}");
            }
            else
            {
                Debug.LogWarning("?? No UniversalAdditionalCameraData found - this might be the issue!");
            }
        }
        else
        {
            Debug.LogError("? No URP Pipeline Asset found!");
        }

        // Check Cinemachine
        var brain = GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            Debug.Log("? CinemachineBrain found");
            Debug.Log($"Live Camera: {(brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "None")}");
        }
        else
        {
            Debug.LogWarning("?? No CinemachineBrain found");
        }
    }
}