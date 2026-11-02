using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Adds project-specific range, rune-input, cooldown, comfort-fade, and haptic rules
/// to the Room scene's teleport surface.
/// </summary>
public sealed class TeleportationAreaRules : TeleportationArea
{
    [SerializeField, Min(0.1f)] float m_MaxRange = 4f;
    [SerializeField, Min(0f)] float m_Cooldown = 0.5f;
    [SerializeField, Range(0.05f, 0.2f)] float m_FadeDuration = 0.12f;
    [SerializeField, Range(0f, 1f)] float m_TriggerThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] float m_HapticAmplitude = 0.5f;
    [SerializeField, Min(0f)] float m_HapticDuration = 0.1f;

    InputAction m_RuneDrawingAction;
    TeleportationProvider m_Provider;
    CanvasGroup m_FadeGroup;
    float m_NextTeleportTime;

    void Start()
    {
        m_Provider = teleportationProvider != null
            ? teleportationProvider
            : FindFirstObjectByType<TeleportationProvider>();

        if (m_Provider != null)
        {
            teleportationProvider = m_Provider;
            m_Provider.delayTime = Mathf.Max(m_Provider.delayTime, m_FadeDuration + 0.04f);
        }
        else
        {
            Debug.LogError("TeleportationAreaRules couldn't find a Teleportation Provider.", this);
        }

        m_RuneDrawingAction = FindAction("XRI Right Interaction/Activate");
        if (m_RuneDrawingAction == null)
            Debug.LogWarning("Right controller Activate input was not found; rune drawing won't block teleportation.", this);

        teleporting.AddListener(OnTeleporting);
    }

    protected override void OnDestroy()
    {
        if (teleporting != null)
            teleporting.RemoveListener(OnTeleporting);

        base.OnDestroy();
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        return base.IsSelectableBy(interactor) && IsDestinationAllowed(interactor);
    }

    protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
    {
        if (!IsDestinationAllowed(interactor))
            return false;

        return base.GenerateTeleportRequest(interactor, raycastHit, ref teleportRequest);
    }

    bool IsDestinationAllowed(IXRInteractor interactor)
    {
        if (Time.time < m_NextTeleportTime)
            return false;

        if (m_RuneDrawingAction != null && m_RuneDrawingAction.ReadValue<float>() >= m_TriggerThreshold)
            return false;

        if (interactor is not XRRayInteractor rayInteractor ||
            !rayInteractor.TryGetCurrent3DRaycastHit(out var hit))
            return false;

        var cameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform == null)
            return false;

        var horizontalOffset = Vector3.ProjectOnPlane(hit.point - cameraTransform.position, Vector3.up);
        return horizontalOffset.sqrMagnitude <= m_MaxRange * m_MaxRange;
    }

    static InputAction FindAction(string actionPath)
    {
        var managers = FindObjectsByType<InputActionManager>(FindObjectsSortMode.None);
        foreach (var manager in managers)
        {
            if (manager.actionAssets == null)
                continue;

            foreach (var asset in manager.actionAssets)
            {
                if (asset == null)
                    continue;

                var action = asset.FindAction(actionPath, false);
                if (action != null)
                    return action;
            }
        }

        return null;
    }

    void OnTeleporting(TeleportingEventArgs _)
    {
        m_NextTeleportTime = Time.time + m_Cooldown;
        SendLeftControllerHaptic();

        if (isActiveAndEnabled)
            StartCoroutine(FadeAroundTeleport());
    }

    IEnumerator FadeAroundTeleport()
    {
        EnsureFadeOverlay();
        if (m_FadeGroup == null)
            yield break;

        yield return FadeTo(1f);
        yield return new WaitForSecondsRealtime(m_Provider != null ? m_Provider.delayTime : m_FadeDuration);
        yield return FadeTo(0f);
    }

    IEnumerator FadeTo(float targetAlpha)
    {
        var startAlpha = m_FadeGroup.alpha;
        var elapsed = 0f;
        while (elapsed < m_FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_FadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / m_FadeDuration));
            yield return null;
        }

        m_FadeGroup.alpha = targetAlpha;
    }

    void EnsureFadeOverlay()
    {
        if (m_FadeGroup != null)
            return;

        var canvasObject = new GameObject("Teleport Fade Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        m_FadeGroup = canvasObject.GetComponent<CanvasGroup>();
        m_FadeGroup.alpha = 0f;
        m_FadeGroup.blocksRaycasts = false;
        m_FadeGroup.interactable = false;

        var image = canvasObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void SendLeftControllerHaptic()
    {
        var devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, devices);

        foreach (var device in devices)
        {
            if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, m_HapticAmplitude, m_HapticDuration);
        }
    }
}
