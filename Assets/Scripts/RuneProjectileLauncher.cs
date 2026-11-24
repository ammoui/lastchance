using UnityEngine;

// Пускает снаряд «Искра» из кончика палочки, когда распознаватель
// подтвердил руну. Цель — ближайший EnemyDummy; если врагов нет
// в радиусе поиска, снаряд летит по направлению взгляда камеры.
public class RuneProjectileLauncher : MonoBehaviour
{
    [SerializeField] private RuneRecognizer recognizer;

    [Header("Снаряд")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float turnSpeed = 180f;
    [SerializeField] private float hitRadius = 0.25f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float size = 0.05f;
    [SerializeField] private int damage = 2;

    [Header("Цель")]
    [SerializeField] private float targetSearchRadius = 30f;
    [SerializeField] private string targetTag = ""; // пусто = искать по типу EnemyDummy
    [SerializeField] private bool homing = false; // автонаводка выключена: летит прямо

    [Header("Внешний вид")]
    [SerializeField] private Color projectileColor = new Color(1f, 0.7f, 0.2f);

    private RuneCaster cachedCaster;

    private void OnEnable()
    {
        if (recognizer != null)
            recognizer.RuneRecognized += OnRuneRecognized;
    }

    private void OnDisable()
    {
        if (recognizer != null)
            recognizer.RuneRecognized -= OnRuneRecognized;
    }

    private void Update()
    {
        // распознаватель находит палочку с задержкой — держим ссылку актуальной
        if (recognizer == null)
        {
            recognizer = FindFirstObjectByType<RuneRecognizer>(FindObjectsInactive.Include);
            if (recognizer != null)
                recognizer.RuneRecognized += OnRuneRecognized;
        }
    }

    private void OnRuneRecognized(string runeName, float score)
    {
        RuneCaster caster = FindCaster();
        if (caster == null)
        {
            Debug.LogWarning("Снаряд не запущен: палочка не найдена");
            return;
        }

        Vector3 origin = caster.WandTipPosition;
        // цель только при включённой автонаводке; по умолчанию снаряд летит прямо
        Transform target = homing ? FindNearestEnemy(origin) : null;
        Vector3 dir = Camera.main != null
            ? Camera.main.transform.forward
            : caster.transform.forward;

        Spawn(origin, dir, target);
        Debug.Log("Искра выпущена" + (target != null ? ", цель: " + target.name : " без цели"));
    }

    private RuneCaster FindCaster()
    {
        if (cachedCaster == null)
            cachedCaster = FindFirstObjectByType<RuneCaster>(FindObjectsInactive.Include);
        return cachedCaster;
    }

    private Transform FindNearestEnemy(Vector3 from)
    {
        EnemyDummy[] enemies = FindObjectsByType<EnemyDummy>(FindObjectsSortMode.None);
        Transform best = null;
        float bestDist = targetSearchRadius * targetSearchRadius;
        foreach (EnemyDummy e in enemies)
        {
            if (!string.IsNullOrEmpty(targetTag) && !e.CompareTag(targetTag))
                continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = e.transform;
            }
        }
        return best;
    }

    private void Spawn(Vector3 origin, Vector3 dir, Transform target)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "IskraProjectile";
        go.transform.localScale = Vector3.one * size;
        // физика не нужна, но Collider от примитива мешает — убираем
        Destroy(go.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", projectileColor);
        mat.SetColor("_EmissionColor", projectileColor * 2f);
        mat.EnableKeyword("_EMISSION");
        go.GetComponent<Renderer>().material = mat;

        var proj = go.AddComponent<RuneProjectile>();
        proj.Init(origin, dir, target, mat, speed, turnSpeed, hitRadius, lifetime, damage);
    }
}
