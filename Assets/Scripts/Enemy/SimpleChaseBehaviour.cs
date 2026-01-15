using UnityEngine;
using UnityEngine.AI;

public class SimpleChaseBehaviour : EnemyBehaviour
{
    [Header("Chase Settings")]
    public float detectionRadius = 15f;
    public float stopDistance = 1.5f;
    public float moveSpeed = 3.5f;

    [Header("Aggression")]
    public bool aggressive = true;
    public EnemyAttack attack;

    Transform player;
    EnemyStats stats;
    NavMeshAgent agent;

    public override void Initialize(EnemyStats stats)
    {
        this.stats = stats;
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.stoppingDistance = stopDistance;
            agent.speed = moveSpeed;
            agent.updateRotation = true;
        }
        if (attack == null)
            attack = GetComponent<EnemyAttack>();
    }

    void Start()
    {
        if (stats == null)
        {
            stats = GetComponent<EnemyStats>();
            if (stats != null) Initialize(stats);
        }
    }

    void Update()
    {
        TickBehaviour();
    }

    public override void TickBehaviour()
    {
        if (stats == null || stats.isDead) return;

        if (player == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > detectionRadius)
            return; // idle until player within detection

        // Move toward player
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(player.position);
        }
        else
        {
            // Fallback simple movement
            Vector3 dir = (player.position - transform.position);
            dir.y = 0f;
            float d = dir.magnitude;
            if (d > stopDistance)
            {
                dir /= Mathf.Max(d, 0.0001f);
                transform.position += dir * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 10f * Time.deltaTime);
            }
        }

        // Attack when close
        if (aggressive && attack != null)
        {
            attack.TryAttack(player.gameObject);
        }
    }
}
