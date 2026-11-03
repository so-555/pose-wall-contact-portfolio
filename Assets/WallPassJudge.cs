using System.Collections.Generic;
using UnityEngine;

public class WallPassJudge : MonoBehaviour
{
    [Header("References")]
    public RandomWallLooper wallLooper;
    public Transform avatarRoot;
    public Animator avatarAnimator;

    [Header("Timing")]
    public bool judgeAtAvatarPlane = true;
    [Range(0.0f, 1.0f)]
    public float judgeProgress = 0.78f;
    [Range(0.0f, 1.0f)]
    public float resetJudgeProgress = 0.15f;

    [System.Serializable]
    public struct DemoContactPoint
    {
        public HumanBodyBones bodyPart;
        public Transform anchor;
    }

    [Header("Demo-only sampling (for primitive avatar without Humanoid rig)")]
    public DemoContactPoint[] demoContactPoints;

    [Header("Lightweight Body Sampling")]
    [Tooltip("Use humanoid bones and a few points between them instead of a full mesh collision.")]
    public bool useBodySamples = true;
    [Range(0, 4)]
    public int pointsBetweenJoints = 2;
    [Min(0.0f)]
    [Tooltip("Maximum radius used by any body-part sample.")]
    public float bodyRadius = 0.22f;

    [Header("Body Fit Radii")]
    [Range(0.05f, 0.35f)]
    public float radiusToBoneLength = 0.18f;
    [Min(0.0f)]
    public float torsoRadius = 0.18f;
    [Min(0.0f)]
    public float headRadius = 0.16f;
    [Min(0.0f)]
    public float upperArmRadius = 0.10f;
    [Min(0.0f)]
    public float lowerArmRadius = 0.08f;
    [Min(0.0f)]
    public float handRadius = 0.06f;
    [Min(0.0f)]
    public float upperLegRadius = 0.13f;
    [Min(0.0f)]
    public float lowerLegRadius = 0.10f;
    [Min(0.0f)]
    public float footRadius = 0.08f;

    [Tooltip("Add one small collision point at the tip of each hand.")]
    public bool includeFingerTipPoints = true;
    [Min(0.0f)]
    public float fingerTipRadius = 0.04f;
    [Min(0.0f)]
    public float fallbackFingerTipExtension = 0.12f;
    [Header("Fallback Hole")]
    public float fallbackHoleMinX = -1.0f;
    public float fallbackHoleMaxX = 1.0f;

    [Header("Collision Visualization")]
    [Tooltip("Show the actual body-sample sizes in Scene/Game view when Gizmos is enabled.")]
    public bool showCollisionGizmos = true;
    public bool showBoneLines = true;
    public Color bodyGizmoColor = new Color(0.2f, 1.0f, 0.35f, 0.9f);
    public Color fingerTipGizmoColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);

    [Header("Hit Location Display")]
    [Tooltip("Show contact positions in Scene/Game Gizmos and as screen markers during play.")]
    public bool showHitLocations = true;
    public Color hitLocationColor = new Color(1.0f, 0.15f, 0.1f, 1.0f);
    [Min(0.0f)] public float hitLocationGizmoRadius = 0.05f;
    [Min(0.0f)] public float hitLocationScreenSize = 12f;

    [Header("Runtime Debug")]
    [SerializeField] private int lastSampleCount;
    [SerializeField] private string lastHitBonesSummary = "-";

    private readonly List<Vector3> samplePositions = new List<Vector3>(256);
    private readonly List<HumanBodyBones> sampleBones = new List<HumanBodyBones>(256);
    private readonly HashSet<HumanBodyBones> lastHitBones = new HashSet<HumanBodyBones>();
    private readonly List<Vector3> lastHitPositions = new List<Vector3>(64);
    private GameObject observedWall;
    private Collider[] wallColliders = new Collider[0];
    private bool judgeAgainstWallGeometry;
    private bool judgedCurrentWall;

    private static readonly HumanBodyBones[,] BoneSegments =
    {
        { HumanBodyBones.Hips, HumanBodyBones.Spine },
        { HumanBodyBones.Spine, HumanBodyBones.Chest },
        { HumanBodyBones.Chest, HumanBodyBones.Neck },
        { HumanBodyBones.Neck, HumanBodyBones.Head },

        { HumanBodyBones.Chest, HumanBodyBones.LeftShoulder },
        { HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm },
        { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
        { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },

        { HumanBodyBones.Chest, HumanBodyBones.RightShoulder },
        { HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm },
        { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
        { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },

        { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg },
        { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
        { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },

        { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg },
        { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
        { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot }
    };

    public int LastSampleCount => lastSampleCount;
    public IReadOnlyCollection<HumanBodyBones> LastHitBones => lastHitBones;
    public string LastHitBonesSummary => lastHitBonesSummary;
    public IReadOnlyList<Vector3> LastHitPositions => lastHitPositions;

    public void ResetContacts()
    {
        lastSampleCount = 0;
        lastHitBones.Clear();
        lastHitPositions.Clear();
        lastHitBonesSummary = "-";
        judgedCurrentWall = false;
    }

    private void Start()
    {
        if (wallLooper == null)
        {
            wallLooper = FindFirstObjectByType<RandomWallLooper>();
        }

        BindAnimator();
        if (avatarRoot == null && avatarAnimator != null)
        {
            avatarRoot = avatarAnimator.transform;
        }
    }

    private void Update()
    {
        if (wallLooper == null)
        {
            return;
        }

        if (observedWall != wallLooper.CurrentWall)
        {
            observedWall = wallLooper.CurrentWall;
            wallColliders = observedWall != null
                ? observedWall.GetComponentsInChildren<Collider>()
                : new Collider[0];
            judgedCurrentWall = false;
        }

        if (wallLooper.Progress01 <= resetJudgeProgress)
        {
            judgedCurrentWall = false;
        }

        if (judgedCurrentWall || !HasReachedAvatar())
        {
            return;
        }

        judgedCurrentWall = true;
        JudgeCurrentWall();
    }

    private void JudgeCurrentWall()
    {
        lastHitBones.Clear();
        lastHitPositions.Clear();
        lastHitBonesSummary = "-";
        WallHoleDefinition hole = wallLooper.CurrentWall != null
            ? wallLooper.CurrentWall.GetComponent<WallHoleDefinition>()
            : null;

        float holeMinX = hole != null ? hole.holeMinX : fallbackHoleMinX;
        float holeMaxX = hole != null ? hole.holeMaxX : fallbackHoleMaxX;
        judgeAgainstWallGeometry = hole == null && TryGetWallBounds(out _);

        if (useBodySamples && TryCollectBodyContacts(holeMinX, holeMaxX))
        {
            Debug.Log(
                $"Wall contacts: {lastHitPositions.Count}/{lastSampleCount} sample points, " +
                $"hit bones: {lastHitBonesSummary}. No pass/fail decision.");
            return;
        }

        // Non-humanoid fallback: record a single center contact without scoring it.
        Vector3 fallbackPosition = avatarRoot != null ? avatarRoot.position : new Vector3(GetAvatarX(), 0f, 0f);
        bool touched = judgeAgainstWallGeometry && avatarRoot != null
            ? HitsWall(fallbackPosition)
            : fallbackPosition.x < holeMinX || fallbackPosition.x > holeMaxX;
        lastSampleCount = 1;
        if (touched)
        {
            lastHitPositions.Add(fallbackPosition);
            lastHitBonesSummary = "AvatarRoot (fallback)";
        }
        Debug.Log($"Wall center fallback: {lastHitBonesSummary}. No pass/fail decision.");
    }

    private bool TryCollectBodyContacts(float holeMinX, float holeMaxX)
    {
        BindAnimator();
        samplePositions.Clear();
        sampleBones.Clear();

        if (avatarAnimator != null && avatarAnimator.isHuman)
        {
            int segmentCount = BoneSegments.GetLength(0);
            for (int i = 0; i < segmentCount; i++)
            {
                HumanBodyBones fromBone = BoneSegments[i, 0];
                HumanBodyBones toBone = BoneSegments[i, 1];
                Transform from = avatarAnimator.GetBoneTransform(fromBone);
                Transform to = avatarAnimator.GetBoneTransform(toBone);
                if (from == null || to == null)
                {
                    continue;
                }

                int steps = pointsBetweenJoints + 1;
                float sampleRadius = GetSegmentRadius(toBone, Vector3.Distance(from.position, to.position));
                for (int step = 0; step <= steps; step++)
                {
                    float t = step / (float)steps;
                    AddSampleWithRadius(Vector3.Lerp(from.position, to.position, t), sampleRadius, toBone);
                }
            }

            if (includeFingerTipPoints)
            {
                AddFingerTipPoint(
                    HumanBodyBones.LeftMiddleDistal,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand);
                AddFingerTipPoint(
                    HumanBodyBones.RightMiddleDistal,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand);
            }
        }
        else if (demoContactPoints != null && demoContactPoints.Length > 0)
        {
            // Primitive-figure demo only; real avatars use the Humanoid bone sampler above.
            foreach (DemoContactPoint demoPoint in demoContactPoints)
            {
                if (demoPoint.anchor != null)
                {
                    AddSampleWithRadius(demoPoint.anchor.position, 0.035f, demoPoint.bodyPart);
                }
            }
        }
        else
        {
            return false;
        }

        lastSampleCount = samplePositions.Count;
        if (lastSampleCount == 0)
        {
            return false;
        }

        for (int i = 0; i < samplePositions.Count; i++)
        {
            if (judgeAgainstWallGeometry)
            {
                if (HitsWall(samplePositions[i]))
                {
                    lastHitBones.Add(sampleBones[i]);
                    lastHitPositions.Add(samplePositions[i]);
                }
                continue;
            }

            float x = samplePositions[i].x;
            if (x < holeMinX || x > holeMaxX)
            {
                lastHitBones.Add(sampleBones[i]);
                lastHitPositions.Add(samplePositions[i]);
            }
        }

        lastHitBonesSummary = lastHitBones.Count > 0
            ? string.Join(", ", lastHitBones)
            : "-";

        // Record only the contact positions and body parts; no gameplay result.
        return true;
    }

    private float GetSegmentRadius(HumanBodyBones toBone, float boneLength)
    {
        float configuredRadius;
        switch (toBone)
        {
            case HumanBodyBones.Head:
                configuredRadius = headRadius;
                break;
            case HumanBodyBones.LeftShoulder:
            case HumanBodyBones.RightShoulder:
            case HumanBodyBones.LeftUpperArm:
            case HumanBodyBones.RightUpperArm:
                configuredRadius = upperArmRadius;
                break;
            case HumanBodyBones.LeftLowerArm:
            case HumanBodyBones.RightLowerArm:
                configuredRadius = lowerArmRadius;
                break;
            case HumanBodyBones.LeftHand:
            case HumanBodyBones.RightHand:
                configuredRadius = handRadius;
                break;
            case HumanBodyBones.LeftUpperLeg:
            case HumanBodyBones.RightUpperLeg:
                configuredRadius = upperLegRadius;
                break;
            case HumanBodyBones.LeftLowerLeg:
            case HumanBodyBones.RightLowerLeg:
                configuredRadius = lowerLegRadius;
                break;
            case HumanBodyBones.LeftFoot:
            case HumanBodyBones.RightFoot:
                configuredRadius = footRadius;
                break;
            default:
                configuredRadius = torsoRadius;
                break;
        }

        float lengthLimit = boneLength * radiusToBoneLength;
        return Mathf.Max(0f, Mathf.Min(bodyRadius, Mathf.Min(configuredRadius, lengthLimit)));
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showHitLocations || lastHitPositions.Count == 0)
        {
            return;
        }

        Camera viewCamera = Camera.main;
        if (viewCamera == null && Camera.allCamerasCount > 0)
        {
            viewCamera = Camera.allCameras[0];
        }
        if (viewCamera == null)
        {
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = hitLocationColor;
        float size = Mathf.Max(1f, hitLocationScreenSize);
        for (int i = 0; i < lastHitPositions.Count; i++)
        {
            Vector3 screen = viewCamera.WorldToScreenPoint(lastHitPositions[i]);
            if (screen.z <= 0f)
            {
                continue;
            }

            GUI.DrawTexture(
                new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.5f, size, size),
                Texture2D.whiteTexture);
        }
        GUI.color = previousColor;
    }

    private void OnDrawGizmos()
    {
        if (showHitLocations)
        {
            Gizmos.color = hitLocationColor;
            for (int i = 0; i < lastHitPositions.Count; i++)
            {
                Gizmos.DrawSphere(lastHitPositions[i], hitLocationGizmoRadius);
            }
        }

        if (!showCollisionGizmos)
        {
            return;
        }

        Animator debugAnimator = avatarAnimator;
        if (debugAnimator == null && avatarRoot != null)
        {
            debugAnimator = avatarRoot.GetComponentInChildren<Animator>();
        }

        if (debugAnimator == null || !debugAnimator.isHuman)
        {
            if (demoContactPoints != null)
            {
                Gizmos.color = bodyGizmoColor;
                foreach (DemoContactPoint demoPoint in demoContactPoints)
                {
                    if (demoPoint.anchor != null)
                    {
                        Gizmos.DrawWireSphere(demoPoint.anchor.position, 0.035f);
                    }
                }
            }
            return;
        }

        Gizmos.color = bodyGizmoColor;
        int segmentCount = BoneSegments.GetLength(0);
        for (int i = 0; i < segmentCount; i++)
        {
            HumanBodyBones fromBone = BoneSegments[i, 0];
            HumanBodyBones toBone = BoneSegments[i, 1];
            Transform from = debugAnimator.GetBoneTransform(fromBone);
            Transform to = debugAnimator.GetBoneTransform(toBone);
            if (from == null || to == null)
            {
                continue;
            }

            if (showBoneLines)
            {
                Gizmos.DrawLine(from.position, to.position);
            }

            int steps = pointsBetweenJoints + 1;
            float radius = GetSegmentRadius(toBone, Vector3.Distance(from.position, to.position));
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                Gizmos.DrawWireSphere(Vector3.Lerp(from.position, to.position, t), radius);
            }
        }

        if (!includeFingerTipPoints)
        {
            return;
        }

        Gizmos.color = fingerTipGizmoColor;
        DrawFingerTipGizmo(
            debugAnimator,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand);
        DrawFingerTipGizmo(
            debugAnimator,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand);
    }

    private void DrawFingerTipGizmo(
        Animator animator,
        HumanBodyBones middleFingerTipBone,
        HumanBodyBones lowerArmBone,
        HumanBodyBones handBone)
    {
        if (TryGetFingerTipPosition(
            animator, middleFingerTipBone, lowerArmBone, handBone, out Vector3 position))
        {
            Gizmos.DrawWireSphere(position, fingerTipRadius);
        }
    }

    private void AddFingerTipPoint(
        HumanBodyBones middleFingerTipBone,
        HumanBodyBones lowerArmBone,
        HumanBodyBones handBone)
    {
        if (TryGetFingerTipPosition(
            avatarAnimator, middleFingerTipBone, lowerArmBone, handBone, out Vector3 position))
        {
            AddSampleWithRadius(position, fingerTipRadius, middleFingerTipBone);
        }
    }

    private bool TryGetFingerTipPosition(
        Animator animator,
        HumanBodyBones middleFingerTipBone,
        HumanBodyBones lowerArmBone,
        HumanBodyBones handBone,
        out Vector3 position)
    {
        Transform middleFingerTip = animator.GetBoneTransform(middleFingerTipBone);
        if (middleFingerTip != null)
        {
            position = middleFingerTip.position;
            return true;
        }

        Transform lowerArm = animator.GetBoneTransform(lowerArmBone);
        Transform hand = animator.GetBoneTransform(handBone);
        if (lowerArm == null || hand == null)
        {
            position = Vector3.zero;
            return false;
        }

        Vector3 handDirection = hand.position - lowerArm.position;
        float extension = Mathf.Max(handDirection.magnitude * 0.25f, fallbackFingerTipExtension);
        Vector3 direction = handDirection.sqrMagnitude > 0.0001f
            ? handDirection.normalized
            : Vector3.right;
        position = hand.position + direction * extension;
        return true;
    }

    private void AddSampleWithRadius(Vector3 center, float radius, HumanBodyBones bone)
    {
        AddSample(center, bone);
        if (radius <= 0f) return;
        AddSample(center - Vector3.right * radius, bone);
        AddSample(center + Vector3.right * radius, bone);
        if (judgeAgainstWallGeometry)
        {
            AddSample(center - Vector3.up * radius, bone);
            AddSample(center + Vector3.up * radius, bone);
        }
    }

    private void AddSample(Vector3 position, HumanBodyBones bone)
    {
        samplePositions.Add(position);
        sampleBones.Add(bone);
    }

    private bool HasReachedAvatar()
    {
        if (judgeAtAvatarPlane && observedWall != null &&
            observedWall.GetComponent<WallHoleDefinition>() == null &&
            avatarRoot != null && wallLooper.MoveDirection.sqrMagnitude > 0f &&
            TryGetWallBounds(out Bounds bounds))
        {
            return Vector3.Dot(bounds.center - avatarRoot.position, wallLooper.MoveDirection) >= 0f;
        }
        return wallLooper.Progress01 >= judgeProgress;
    }

    private bool TryGetWallBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Collider wallCollider in wallColliders)
        {
            if (wallCollider == null || !wallCollider.enabled || wallCollider.isTrigger) continue;
            if (found) bounds.Encapsulate(wallCollider.bounds);
            else bounds = wallCollider.bounds;
            found = true;
        }
        return found;
    }

    private bool HitsWall(Vector3 point)
    {
        Vector3 direction = -wallLooper.MoveDirection;
        if (direction.sqrMagnitude == 0f) return false;

        // Cast through the wall at each body point's projected position, preserving real holes.
        foreach (Collider wallCollider in wallColliders)
        {
            if (wallCollider == null || !wallCollider.enabled || wallCollider.isTrigger) continue;
            Bounds bounds = wallCollider.bounds;
            float extent = bounds.extents.magnitude + 1f;
            float offset = Vector3.Dot(bounds.center - point, direction);
            Ray ray = new Ray(point + direction * (offset - extent), direction);
            if (wallCollider.Raycast(ray, out _, extent * 2f)) return true;
        }
        return false;
    }

    private void BindAnimator()
    {
        if (avatarAnimator != null)
        {
            return;
        }

        if (avatarRoot != null)
        {
            avatarAnimator = avatarRoot.GetComponentInChildren<Animator>();
        }

        if (avatarAnimator == null)
        {
            Animator[] animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].isHuman)
                {
                    avatarAnimator = animators[i];
                    break;
                }
            }
        }
    }

    private float GetAvatarX()
    {
        if (avatarRoot != null)
        {
            return avatarRoot.position.x;
        }

        return 0f;
    }
}
