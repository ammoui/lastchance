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
    [SerializeField] private int damage = 2;

    private Transform target;
    private Collider targetCollider;
    private Vector3 direction;
    private float age;
    private Material material;

    public void Init(Vector3 origin, Vector3 dir, Transform homingTarget, Material mat,
                     float projSpeed, float projTurnSpeed, float projHitRadius, float projLifetime,
                     int projDamage = 2)
    {
        transform.position = origin;
        direction = dir.normalized;
        target = homingTarget;
        targetCollider = homingTarget != null ? homingTarget.GetComponent<Collider>() : null;
        material = mat;
        speed = projSpeed;
        turnSpeed = projTurnSpeed;
        hitRadius = projHitRadius;
        lifetime = projLifetime;
        damage = projDamage;
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
            // наводимся на центр коллайдера (у цилиндра-болванчика
            // transform.position в основании, в ноги целиться нельзя)
            Vector3 aimPoint = targetCollider != null
                ? targetCollider.bounds.center
                : target.position;
            Vector3 toTarget = aimPoint - transform.position;
            // доводка: поворачиваем текущее направление к цели,
            // но не мгновенно — снаряд может быть сбит движением врага
            Vector3 rotated = Vector3.RotateTowards(
                direction, toTarget.normalized,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            if (rotated.sqrMagnitude > 0.0001f)
                direction = rotated;
        }

        // попадание: луч по траектории полёта за этот кадр.
        // работает и без автонаводки: снаряд просто летит прямо
        // и врезается в первый коллайдер на пути
        float step = speed * Time.deltaTime;
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, step + hitRadius))
        {
            var enemy = hit.collider.GetComponentInParent<EnemyDummy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Debug.Log("Искра попала по " + enemy.name + " (урон " + damage + ")");
            }
            else
            {
                Debug.Log("Искра разбилась о " + hit.collider.name);
            }
            Destroy(gameObject);
            return;
        }

        transform.position += direction * step;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
