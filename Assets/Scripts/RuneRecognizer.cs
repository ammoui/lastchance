using System.Collections.Generic;
using UnityEngine;

// Распознаёт руну по точкам жеста алгоритмом $1 Unistroke.
// Подписывается на RuneCaster.GestureFinished, переводит жест в 2D
// на плоскости перед игроком и сравнивает с шаблоном.
public class RuneRecognizer : MonoBehaviour
{
    [SerializeField] private RuneCaster runeCaster;

    [Header("Распознавание")]
    [SerializeField] private int sampleCount = 64;
    [SerializeField] private float threshold = 0.75f;

    [Header("Цвет следа после жеста")]
    [SerializeField] private bool colorFeedback = true;
    [SerializeField] private Color successColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color failColor = new Color(1f, 0.25f, 0.2f);

    // Шаблон руны «Искра» — молния одним штрихом, вершины ломаной.
    // Зигзаг специально широкий: узкий почти не отличить от прямой линии.
    private static readonly Vector2[] sparkTemplate =
    {
        new Vector2(0f, 1f),
        new Vector2(0.6f, 0.55f),
        new Vector2(0.15f, 0.5f),
        new Vector2(0.75f, 0f),
    };

    private List<Vector2> template;
    private List<Vector2> templateReversed;

    private void Awake()
    {
        InitTemplates();
        FindCaster();
    }

    private void OnEnable()
    {
        FindCaster();
        if (runeCaster != null)
            runeCaster.GestureFinished += OnGestureFinished;
    }

    private void OnDisable()
    {
        if (runeCaster != null)
            runeCaster.GestureFinished -= OnGestureFinished;
    }

    private void Update()
    {
        // контроллер с палочкой может включаться после загрузки сцены,
        // поэтому ищем его, пока не нашли
        if (runeCaster != null)
            return;
        FindCaster();
        if (runeCaster != null)
        {
            runeCaster.GestureFinished += OnGestureFinished;
            Debug.Log("Распознаватель подключился к палочке");
        }
    }

    private void FindCaster()
    {
        // Inactive тоже ищем: палочка в XR-сетапе может лежать
        // в пока выключенной ветке контроллера
        if (runeCaster == null)
            runeCaster = FindFirstObjectByType<RuneCaster>(FindObjectsInactive.Include);
    }

    private void InitTemplates()
    {
        template = Prepare(new List<Vector2>(sparkTemplate));

        var reversed = new List<Vector2>(sparkTemplate);
        reversed.Reverse();
        templateReversed = Prepare(reversed);
    }

    private void OnGestureFinished(List<Vector3> gesture3d)
    {
        if (gesture3d == null || gesture3d.Count < 2)
            return;
        Recognize(To2D(gesture3d));
    }

    // Проекция на плоскость перед игроком: вправо — от камеры, вверх — мировая
    private List<Vector2> To2D(List<Vector3> gesture3d)
    {
        Vector3 right = Camera.main.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
            right = Camera.main.transform.right; // камера смотрит вертикально, берём как есть
        right.Normalize();

        Vector3 start = gesture3d[0];
        var result = new List<Vector2>(gesture3d.Count);
        foreach (Vector3 p in gesture3d)
        {
            Vector3 d = p - start;
            result.Add(new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, Vector3.up)));
        }
        return result;
    }

    // Возвращает оценку 0..1 и пишет результат в консоль
    public float Recognize(List<Vector2> gesture)
    {
        if (template == null)
            InitTemplates();
        if (gesture == null || gesture.Count < 2)
            return 0f;

        List<Vector2> prepared = Prepare(gesture);

        // Прямая линия после поворота ложится горизонтально и почти
        // не имеет высоты: отсекаем её до сравнения с шаблоном
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 p in prepared)
        {
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        if (maxY - minY < 0.1f)
        {
            Debug.Log("Не распознано: почти прямая линия");
            if (colorFeedback && runeCaster != null)
                runeCaster.SetTrailColor(failColor);
            return 0f;
        }

        // руну могут нарисовать с любого конца, берём лучшую оценку
        float score = Mathf.Max(Score(prepared, template), Score(prepared, templateReversed));

        if (score >= threshold)
            Debug.Log("Руна распознана: Искра (" + score.ToString("0.00") + ")");
        else
            Debug.Log("Не распознано (" + score.ToString("0.00") + ")");

        if (colorFeedback && runeCaster != null)
            runeCaster.SetTrailColor(score >= threshold ? successColor : failColor);

        return score;
    }

    // Приведение фигуры к виду, в котором сравниваем: 64 точки, поворот, масштаб, сдвиг
    private List<Vector2> Prepare(List<Vector2> points)
    {
        List<Vector2> result = Resample(points, sampleCount);
        result = RotateToZero(result);
        result = ScaleUniform(result, 1f);
        return TranslateToOrigin(result);
    }

    // Ровно n точек на равных расстояниях вдоль линии
    private static List<Vector2> Resample(List<Vector2> points, int n)
    {
        float step = PathLength(points) / (n - 1);
        float accumulated = 0f;
        var src = new List<Vector2>(points);
        var result = new List<Vector2> { src[0] };

        for (int i = 1; i < src.Count; i++)
        {
            float d = Vector2.Distance(src[i - 1], src[i]);
            if (accumulated + d >= step)
            {
                Vector2 p = src[i - 1] + (src[i] - src[i - 1]) * ((step - accumulated) / d);
                result.Add(p);
                src.Insert(i, p);
                accumulated = 0f;
            }
            else
            {
                accumulated += d;
            }
        }

        while (result.Count < n)
            result.Add(src[src.Count - 1]);
        return result;
    }

    // Поворот так, чтобы первая точка лежала правее центра фигуры
    private static List<Vector2> RotateToZero(List<Vector2> points)
    {
        Vector2 c = Centroid(points);
        float angle = Mathf.Atan2(c.y - points[0].y, c.x - points[0].x);
        float cos = Mathf.Cos(-angle);
        float sin = Mathf.Sin(-angle);

        var result = new List<Vector2>(points.Count);
        foreach (Vector2 p in points)
        {
            Vector2 d = p - c;
            result.Add(new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + c);
        }
        return result;
    }

    // Одинаковый масштаб по обеим осям, по большей стороне:
    // у линии почти нулевая высота, иначе дрожание руки раздуется в зигзаг
    private static List<Vector2> ScaleUniform(List<Vector2> points, float size)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (Vector2 p in points)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }

        float maxSide = Mathf.Max(maxX - minX, maxY - minY);
        if (maxSide < 0.0001f)
            maxSide = 1f;
        float scale = size / maxSide;

        var result = new List<Vector2>(points.Count);
        foreach (Vector2 p in points)
            result.Add(p * scale);
        return result;
    }

    private static List<Vector2> TranslateToOrigin(List<Vector2> points)
    {
        Vector2 c = Centroid(points);
        var result = new List<Vector2>(points.Count);
        foreach (Vector2 p in points)
            result.Add(p - c);
        return result;
    }

    private static float PathLength(List<Vector2> points)
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++)
            length += Vector2.Distance(points[i - 1], points[i]);
        return length;
    }

    private static Vector2 Centroid(List<Vector2> points)
    {
        Vector2 sum = Vector2.zero;
        foreach (Vector2 p in points)
            sum += p;
        return sum / points.Count;
    }

    // Среднее расстояние между соответствующими точками, оценка 0..1
    public static float Score(List<Vector2> candidate, List<Vector2> templatePoints)
    {
        float distance = 0f;
        for (int i = 0; i < candidate.Count; i++)
            distance += Vector2.Distance(candidate[i], templatePoints[i]);
        distance /= candidate.Count;
        return 1f - distance / (0.5f * Mathf.Sqrt(2f));
    }

    [ContextMenu("Тест: все фигуры")]
    public void RunAllTests()
    {
        Debug.Log("Тест: идеальная молния");
        Recognize(new List<Vector2>(sparkTemplate));

        var noisy = new List<Vector2>();
        foreach (Vector2 p in Resample(new List<Vector2>(sparkTemplate), 16))
            noisy.Add(p + new Vector2(Random.Range(-0.06f, 0.06f), Random.Range(-0.06f, 0.06f)));
        Debug.Log("Тест: кривая молния");
        Recognize(noisy);

        var reversed = new List<Vector2>(sparkTemplate);
        reversed.Reverse();
        Debug.Log("Тест: молния с другого конца");
        Recognize(reversed);

        var linePoints = new List<Vector2>();
        for (int i = 0; i <= 10; i++)
            linePoints.Add(new Vector2(i * 0.1f, 0f));
        Debug.Log("Тест: прямая линия (не должна пройти)");
        Recognize(linePoints);

        var circle = new List<Vector2>();
        for (int i = 0; i <= 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            circle.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f);
        }
        Debug.Log("Тест: круг (не должен пройти)");
        Recognize(circle);
    }
}
