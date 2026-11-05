using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// Рисование руны: пока зажат курок, пишем точки кончика палочки и показываем след.
[RequireComponent(typeof(LineRenderer))]
public class RuneCaster : MonoBehaviour
{
    [SerializeField] private InputActionReference drawAction;
    [SerializeField] private Transform wandTip;

    [Header("Жест")]
    [SerializeField] private float minPointStep = 0.01f;
    [SerializeField] private float minGestureTime = 0.2f;
    [SerializeField] private float maxGestureTime = 2f;

    [Header("След")]
    [SerializeField] private float fadeTime = 0.4f;
    [SerializeField] private float trailWidth = 0.01f;
    [SerializeField] private Color trailColor = new Color(0.1f, 0.9f, 0.85f);

    [Header("Вибрация")]
    [SerializeField] private float hapticStrength = 0.2f;
    [SerializeField] private float hapticTime = 0.05f;

    // Сюда подпишется распознаватель рун
    public event Action<List<Vector3>> GestureFinished;
    public List<Vector3> LastGesture { get; private set; } = new List<Vector3>();

    private readonly List<Vector3> points = new List<Vector3>();
    private LineRenderer line;
    private Material lineMaterial;
    private HapticImpulsePlayer haptics;
    private Coroutine fadeCoroutine;
    private float startTime;
    private bool drawing;
    private bool waitRelease; // жест закрылся сам, а курок ещё держат

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = trailWidth;
        line.positionCount = 0;
        lineMaterial = line.material;

        haptics = GetComponentInParent<HapticImpulsePlayer>(true);
    }

    private void OnEnable()
    {
        drawAction.action.Enable();
    }

    private void OnDisable()
    {
        drawing = false;
        fadeCoroutine = null;
        line.positionCount = 0;
    }

    private void OnDestroy()
    {
        Destroy(lineMaterial);
    }

    private void Update()
    {
        bool pressed = drawAction.action.IsPressed();
        if (!pressed)
            waitRelease = false;

        if (drawing)
        {
            if (!pressed || Time.time - startTime >= maxGestureTime)
                FinishGesture();
            else if (Vector3.Distance(wandTip.position, points[points.Count - 1]) > minPointStep)
                AddPoint(wandTip.position);
        }
        else if (pressed && !waitRelease)
        {
            StartGesture();
        }
    }

    private void StartGesture()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        drawing = true;
        startTime = Time.time;
        points.Clear();
        line.positionCount = 0;
        SetTrailAlpha(1f);
        AddPoint(wandTip.position);

        if (haptics != null)
            haptics.SendHapticImpulse(hapticStrength, hapticTime);
    }

    private void AddPoint(Vector3 point)
    {
        points.Add(point);
        line.positionCount = points.Count;
        line.SetPosition(points.Count - 1, point);
    }

    private void FinishGesture()
    {
        drawing = false;
        waitRelease = true;

        if (Time.time - startTime < minGestureTime)
        {
            line.positionCount = 0;
            Debug.Log("Жест слишком короткий, точек: " + points.Count);
            return;
        }

        LastGesture = new List<Vector3>(points);
        Debug.Log("Жест закончен, точек: " + points.Count);
        GestureFinished?.Invoke(LastGesture);
        fadeCoroutine = StartCoroutine(FadeTrail());
    }

    private IEnumerator FadeTrail()
    {
        float time = 0f;
        while (time < fadeTime)
        {
            time += Time.deltaTime;
            SetTrailAlpha(1f - time / fadeTime);
            yield return null;
        }

        line.positionCount = 0;
        fadeCoroutine = null;
    }

    private void SetTrailAlpha(float alpha)
    {
        Color color = trailColor;
        color.a = alpha;
        lineMaterial.SetColor("_BaseColor", color);
    }
}
