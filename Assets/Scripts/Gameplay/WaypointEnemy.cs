using UnityEngine;
using KingdomBorder.TwinTowers;

[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(EnemyAgent))]
public sealed class WaypointEnemy : MonoBehaviour
{
    public FortressEnemyDefinition definition;
    public float moveSpeed = 1.25f;
    public float maxHealth = 125f;
    [Range(0,95)] public float armorPercent = 15f;
    private Vector3[] route;
    private int nextPoint;
    private WaveGameController game;
    private Animator animationPlayer;
    private EnemyAgent combat;
    private string facing;
    private bool resolved;
    private float traveled, totalLength;
    public bool Resolved => resolved;
    public WaveGameController Owner => game;
    public string RouteName { get; private set; }
    public int NextPointIndex => nextPoint;
    public EnemyAgent Combat => combat;
    public int BaseDamage => definition != null ? Mathf.Max(1, definition.baseDamage) : 1;
    public int GoldReward => definition != null ? Mathf.Max(0, definition.goldReward) : 18;
    public string DisplayName => definition != null ? definition.displayName : "骷髅兵";

    public void Initialize(WaveGameController owner, WaypointPath path)
    {
        game = owner;
        route = path.GetPositions();
        RouteName = path.name;
        nextPoint = 1;
        resolved = false;
        traveled = totalLength = 0;
        for (int i=1;i<route.Length;i++) totalLength += Vector3.Distance(route[i-1],route[i]);
        animationPlayer = GetComponent<Animator>();
        combat = GetComponent<EnemyAgent>();
        combat.body = GetComponent<SpriteRenderer>();
        // The map owns movement; TwinTowers owns health, armor and slow sources.
        if (definition != null) { maxHealth = definition.health; armorPercent = definition.armorPercent; moveSpeed = definition.speed; }
        combat.destroyOnDeath = false;
        combat.Initialize(maxHealth, armorPercent, moveSpeed, null);
        combat.Died += OnKilled;
        if (route.Length >= 2)
        {
            transform.position = route[0];
            Face(route[1] - route[0]);
        }
    }
    private void OnKilled(EnemyAgent enemy) { Defeat(); }
    private void Update()
    {
        if (game != null && !game.IsGameOver && combat != null && combat.IsAlive)
        {
            animationPlayer.speed = combat.SpeedMultiplier * WorldCombatModifiers.EnemyMovementSpeed;
            Advance(combat.CurrentSpeed * Time.deltaTime);
        }
    }
    public void Advance(float distanceBudget)
    {
        if (resolved || route == null || route.Length < 2 || game == null || game.IsGameOver) return;
        while (distanceBudget > 0f && nextPoint < route.Length)
        {
            Vector3 difference = route[nextPoint] - transform.position;
            float distance = difference.magnitude;
            if (distance < 0.0001f) { nextPoint++; continue; }
            Face(difference);
            float step = Mathf.Min(distanceBudget, distance);
            transform.position = Vector3.MoveTowards(transform.position, route[nextPoint], step);
            traveled += step;
            distanceBudget -= step;
            if (step >= distance) nextPoint++;
        }
        if (combat != null) combat.SetExternalProgress(totalLength > 0 ? traveled / totalLength : 0);
        if (nextPoint >= route.Length) ReachHome();
    }
    private void Face(Vector3 direction)
    {
        string next = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
            ? (direction.x >= 0f ? "east" : "west") : (direction.y >= 0f ? "north" : "south");
        if (next == facing || animationPlayer == null) return;
        float phase = animationPlayer.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        animationPlayer.Play((definition != null ? definition.walkAnimationPrefix : "Skeleton_Walk_") + next, 0, phase);
        facing = next;
    }
    public void ReachHome()
    {
        if (resolved || game == null) return;
        resolved = true;
        if (combat != null) combat.enabled = false;
        game.EnemyReachedHome(this);
        FortressAudio.Play(FortressSound.Leak, transform.position);
        Destroy(gameObject);
    }
    public void Defeat()
    {
        if (resolved) return;
        resolved = true;
        if (combat != null) combat.enabled = false;
        if (game != null) game.EnemyDefeated(this);
        FortressAudio.Play(FortressSound.EnemyDeath, transform.position);
        var visual = GetComponent<FortressEnemyVisual>();
        if (visual != null) visual.PlayDeath(); else Destroy(gameObject);
    }
    public void DiscardForDebug()
    {
        resolved = true;
        if (combat) combat.enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (combat != null) combat.Died -= OnKilled;
        if (!resolved && game != null) game.UnregisterEnemy(this);
    }
}
