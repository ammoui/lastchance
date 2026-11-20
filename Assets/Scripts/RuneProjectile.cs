using UnityEngine;

// Снаряд заклинания «Искра»: летит по направлению выстрела и доводится
// к цели (ближайший EnemyDummy) с ограниченной скоростью поворота.
// Без цели летит прямо, живёт ограниченное время.
public class RuneProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float turnSpeed = 180f;   // град/с, доводка к цели
    [SerializeField] private float hitRadius = 0.25f;
    [SerializeField] private float lifetime = 4f;

    private Transform target;
    private Vector3 direction;
    private float age;
    private Material material;

    public void Init(Vector3 origin, Vector3 dir, Transform homingTarget, Material mat,
                     float projSpeed, float projTurnSpeed, float projHitRadius, float projLifetime)
    {
        transform.position = origin;
        direction = dir.normalized;
        target = homingTarget;
        material = mat;
        speed = projSpeed;
        turnSpeed = projTurnSpeed;
        hitRadius = projHitRadius;
        lifetime = projLifetime;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            // доводка: поворачиваем текущее направление к цели,
            // но не мгновенно — снаряд может быть сбит движением врага
            Vector3 rotated = Vector3.RotateTowards(
                direction, toTarget.normalized,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            if (rotated.sqrMagnitude > 0.0001f)
                direction = rotated;

            if (toTarget.sqrMagnitude <= hitRadius * hitRadius)
            {
                Debug.Log("Искра попала по " + target.name);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += direction * (speed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
