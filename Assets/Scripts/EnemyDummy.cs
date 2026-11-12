using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Болванчик-враг: бежит к позиции камеры игрока (Camera.main)
/// и останавливается на заданной дистанции. Атака — отдельной задачей.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyDummy : MonoBehaviour
{
    [SerializeField, Tooltip("Скорость движения, м/с")]
    private float speed = 2.5f;

    [SerializeField, Tooltip("Дистанция остановки от игрока, м")]
    private float stoppingDistance = 1.3f;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        ApplySettings();
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        // Позицию игрока берём с камеры, а не с корня рига:
        // при движении головы риг остаётся на месте.
        Vector3 target = cam.transform.position;
        target.y = transform.position.y; // цель на уровне пола, а не на высоте головы

        agent.SetDestination(target);
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
}
