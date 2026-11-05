using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

// Симулятор нужен только без шлема: если оставить его со шлемом, он отключает настоящее устройство.
public class SimulatorFallback : MonoBehaviour
{
    [SerializeField] private GameObject simulatorPrefab;

    private void Awake()
    {
        var loader = XRGeneralSettings.Instance.Manager.activeLoader;
        if (!(loader is OpenXRLoaderBase))
            Instantiate(simulatorPrefab);
    }
}
