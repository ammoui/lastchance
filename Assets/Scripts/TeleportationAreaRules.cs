using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Зона телепорта на полу с правилами из диздока:
// дальность, пауза между прыжками, запрет при рисовании, затемнение и вибрация.
public class TeleportationAreaRules : TeleportationArea
{
    [SerializeField] private float maxRange = 4f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private float fadeTime = 0.16f; // всё затемнение, туда и обратно
    [SerializeField] private float hapticStrength = 0.5f;
    [SerializeField] private float hapticTime = 0.1f;
    [SerializeField] private InputActionReference drawAction; // тот же курок, что у палочки

    private CanvasGroup fade;
    private float nextTeleportTime;

    private void Start()
    {
        if (teleportationProvider == null)
            teleportationProvider = FindFirstObjectByType<TeleportationProvider>();

        // Переносим игрока, когда экран уже тёмный
        teleportationProvider.delayTime = fadeTime / 2f;

        CreateFade();
        teleporting.AddListener(OnTeleporting);
    }

    protected override void OnDestroy()
    {
        teleporting.RemoveListener(OnTeleporting);
        base.OnDestroy();
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        return base.IsSelectableBy(interactor) && CanTeleport(interactor);
    }

    protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
    {
        if (!CanTeleport(interactor))
            return false;

        return base.GenerateTeleportRequest(interactor, raycastHit, ref teleportRequest);
    }

    private bool CanTeleport(IXRInteractor interactor)
    {
        if (Time.time < nextTeleportTime || drawAction.action.IsPressed())
            return false;

        var ray = interactor as XRRayInteractor;
        if (ray == null || !ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            return false;

        Vector3 offset = hit.point - Camera.main.transform.position;
        offset.y = 0f;
        return offset.magnitude <= maxRange;
    }

    private void OnTeleporting(TeleportingEventArgs args)
    {
        nextTeleportTime = Time.time + cooldown;

        var haptics = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
        if (haptics != null)
            haptics.SendHapticImpulse(hapticStrength, hapticTime);

        StartCoroutine(FadeScreen());
    }

    private IEnumerator FadeScreen()
    {
        yield return FadeTo(1f);
        yield return FadeTo(0f);
    }

    private IEnumerator FadeTo(float target)
    {
        float start = fade.alpha;
        float duration = fadeTime / 2f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            fade.alpha = Mathf.Lerp(start, target, time / duration);
            yield return null;
        }

        fade.alpha = target;
    }

    // Чёрная панель прямо перед камерой. Экранный канвас в шлеме не виден, поэтому World Space.
    private void CreateFade()
    {
        var panel = new GameObject("Teleport Fade", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        panel.transform.SetParent(Camera.main.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.2f);

        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = short.MaxValue;
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 2f);

        var image = panel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        fade = panel.GetComponent<CanvasGroup>();
        fade.alpha = 0f;
        fade.blocksRaycasts = false;
    }
}
