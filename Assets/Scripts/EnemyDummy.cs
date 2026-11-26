using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Болванчик-враг: бежит к позиции камеры игрока (Camera.main)
/// и останавливается на заданной дистанции. Атака — отдельной задачей.
/// Здоровье: 4 HP (диздок), урон наносит снаряд Искры — 2 ед.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyDummy : MonoBehaviour
{
    [SerializeField, Tooltip("Скорость движения, м/с")]
    private float speed = 2.5f;

    [SerializeField, Tooltip("Дистанция остановки от игрока, м")]
    private float stoppingDistance = 1.3f;

    [Header("Зрение")]
    [SerializeField, Tooltip("Бежать к игроку только при прямой видимости")]
    private bool requireLineOfSight = true;

    [SerializeField, Tooltip("Высота 'глаз' над опорной точкой моба, м")]
    private float eyeHeight = 1.6f;

    [SerializeField, Tooltip("Дальность зрения, м")]
    private float viewDistance = 30f;

    [SerializeField, Tooltip("Слои, которые заслоняют обзор (стены, колонны)")]
    private LayerMask occlusionMask = ~0; // всё

    [Header("Здоровье")]
    [SerializeField] private int maxHealth = 4;
    [SerializeField] private float healthBarHeight = 2.2f;

    private NavMeshAgent agent;
    private int health;
    private Transform healthFill;
    private Material healthFillMaterial;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public event Action Died;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        ApplySettings();
        health = maxHealth;
        CreateHealthBar();
    }

    public void TakeDamage(int amount)
    {
        if (health <= 0)
            return;
        health = Mathf.Max(0, health - amount);
        Debug.Log(name + ": урон " + amount + ", HP " + health + "/" + maxHealth);
        UpdateHealthBar();
        if (health <= 0)
        {
            Died?.Invoke();
            Debug.Log(name + " уничтожен");
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 target = cam.transform.position;

        if (requireLineOfSight && !CanSee(target))
        {
            // Игрока не видно — стоим на месте.
            agent.isStopped = true;
            return;
        }

        // Позицию игрока берём с камеры, а не с корня рига:
        // при движении головы риг остаётся на месте.
        target.y = transform.position.y; // цель на уровне пола, а не на высоте головы

        agent.isStopped = false;
        agent.SetDestination(target);
    }

    /// <summary>
    /// Прямая видимость: луч от "глаз" моба к камере не должен упираться
    /// в геометрию сцены. Попадание в самого игрока (его риг)
    /// помехой не считается — значит, мы его видим.
    /// </summary>
    private bool CanSee(Vector3 target)
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toTarget = target - eye;

        if (toTarget.magnitude > viewDistance)
            return false;

        if (!Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, toTarget.magnitude, occlusionMask))
            return true; // на пути луча вообще ничего нет

        Camera cam = Camera.main;
        if (cam != null && hit.transform.IsChildOf(cam.transform.root))
            return true; // луч упёрся в самого игрока

        return false; // между мобом и игроком стена или колонна
    }

    private void OnValidate()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        ApplySettings();
    }

    private void ApplySettings()
    {
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
    }

    // Простой HP-бар над головой: два квада (фон + заливка), повёрнут к камере.
    private void CreateHealthBar()
    {
        var bar = new GameObject("HealthBar");
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = new Vector3(0f, healthBarHeight, 0f);

        var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
        back.name = "Back";
        back.transform.SetParent(bar.transform, false);
        back.transform.localScale = new Vector3(0.42f, 0.06f, 1f);
        Destroy(back.GetComponent<Collider>());
        back.GetComponent<Renderer>().material = MakeUnlit(new Color(0.1f, 0.1f, 0.1f));

        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        fill.transform.SetParent(bar.transform, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        fill.transform.localScale = new Vector3(0.4f, 0.04f, 1f);
        Destroy(fill.GetComponent<Collider>());
        healthFillMaterial = MakeUnlit(Color.green);
        fill.GetComponent<Renderer>().material = healthFillMaterial;
        healthFill = fill.transform;
    }

    private static Material MakeUnlit(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    private void UpdateHealthBar()
    {
        if (healthFill == null)
            return;
        float fraction = maxHealth > 0 ? (float)health / maxHealth : 0f;
        // сжимаем заливку от центра: сдвигаем, чтобы край оставался на месте
        healthFill.localScale = new Vector3(0.4f * fraction, 0.04f, 1f);
        healthFill.localPosition = new Vector3(-0.2f * (1f - fraction), 0f, -0.001f);
        healthFillMaterial.SetColor("_BaseColor",
            Color.Lerp(Color.red, Color.green, fraction));
    }

    private void LateUpdate()
    {
        // бар всегда смотрит на игрока
        if (healthFill == null)
            return;
        Camera cam = Camera.main;
        if (cam != null)
            healthFill.parent.rotation = Quaternion.LookRotation(
                healthFill.parent.position - cam.transform.position);
    }

    private void OnDestroy()
    {
        if (healthFillMaterial != null)
            Destroy(healthFillMaterial);
    }
}
