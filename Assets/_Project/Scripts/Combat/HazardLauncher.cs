using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>
    /// A room hazard that fires projectiles everybody has to dodge (Stage 29): the haywire vending
    /// machine spits soda cans across the break room. Its shots belong to no side, so they hit you
    /// and the rivals alike: drag a chasing rival through the can lane.
    /// Fires while enabled (a RoomEvent switches it on and off).
    /// </summary>
    public class HazardLauncher : MonoBehaviour
    {
        [SerializeField] StapleProjectile projectilePrefab;
        [Tooltip("Where shots come out (forward = direction). Defaults to this object.")]
        [SerializeField] Transform muzzle;
        [SerializeField] float interval = 0.45f;
        [SerializeField] float speed = 28f;
        [SerializeField] float damage = 30f;
        [SerializeField] float lifetime = 5f;
        [Tooltip("Random spread (degrees) left/right and up/down.")]
        [SerializeField] Vector2 spread = new Vector2(25f, 8f);
        [Tooltip("Sweeps left and right this many degrees while firing (0 = random spread only).")]
        [SerializeField] float sweep = 30f;
        [SerializeField] float sweepSpeed = 0.6f;

        float next;

        void OnEnable() => next = Time.time + 0.3f;

        void Update()
        {
            if (projectilePrefab == null || Time.time < next) return;
            next = Time.time + interval;
            Transform m = muzzle != null ? muzzle : transform;
            float yaw = Mathf.Sin(Time.time * sweepSpeed * Mathf.PI * 2f) * sweep + Random.Range(-spread.x, spread.x);
            Quaternion aim = m.rotation * Quaternion.Euler(Random.Range(-spread.y, spread.y), yaw, 0f);
            Vector3 dir = aim * Vector3.forward;
            var p = Instantiate(projectilePrefab, m.position, aim);
            p.Launch(null, transform.parent != null ? transform.parent : transform, Faction.Neutral, dir * speed, damage, lifetime);
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClack, m.position, 0.6f, 0.7f);
        }
    }
}
