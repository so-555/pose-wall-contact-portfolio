using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WallSilhouetteProjection : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RandomWallLooper wallLooper;
    [SerializeField] private Transform avatarRoot;

    [Header("Appearance")]
    [SerializeField] private Color silhouetteColor = new Color(0.03f, 0.03f, 0.03f, 0.75f);
    [SerializeField] private float wallSurfaceOffset = 0.08f;

    [Header("Size")]
    [SerializeField, Min(0.1f)] private float silhouetteScale = 1f;
    [SerializeField] private Vector2 silhouetteOffset = Vector2.zero;

    private readonly List<Vector3> vertices = new List<Vector3>(8192);
    private readonly List<int> triangles = new List<int>(16384);
    private readonly List<Vector3> vertexBuffer = new List<Vector3>(8192);

    private GameObject silhouetteObject;
    private Mesh silhouetteMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material silhouetteMaterial;

    private SkinnedMeshRenderer[] skinnedRenderers;
    private MeshFilter[] staticFilters;
    private Mesh[] bakedMeshes;

    private void Awake()
    {
        EnsureSilhouetteObject();
    }

    private void Start()
    {
        AutoBindReferences();
    }

    private void LateUpdate()
    {
        if (wallLooper == null || wallLooper.CurrentWall == null)
        {
            SetVisible(false);
            return;
        }

        if (avatarRoot == null || skinnedRenderers == null || staticFilters == null)
        {
            AutoBindReferences();
        }

        // 壁がアバターを通り過ぎたら投影しない（背後の壁に映る影は不自然なため）
        // 高さ成分を無視し、進行方向(XZ平面)だけで前後を判定する
        if (avatarRoot != null && Camera.main != null)
        {
            Vector3 viewDirection = avatarRoot.position - Camera.main.transform.position;
            Vector3 wallOffsetFromAvatar =
                wallLooper.CurrentWall.transform.position - avatarRoot.position;
            viewDirection.y = 0f;
            wallOffsetFromAvatar.y = 0f;
            if (Vector3.Dot(wallOffsetFromAvatar, viewDirection) < 0f)
            {
                SetVisible(false);
                return;
            }
        }

        if (!BuildAvatarSilhouette())
        {
            SetVisible(false);
            return;
        }

        UpdateMaterial();
        PlaceOnCurrentWall();
        SetVisible(true);
    }

    private void AutoBindReferences()
    {
        if (wallLooper == null)
        {
            wallLooper = GetComponent<RandomWallLooper>();
        }

        if (wallLooper == null)
        {
            wallLooper = FindFirstObjectByType<RandomWallLooper>();
        }

        if (avatarRoot == null)
        {
            GameObject kyle = GameObject.Find("KyleRobot");
            if (kyle != null)
            {
                avatarRoot = kyle.transform;
            }
        }

        if (avatarRoot == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                avatarRoot = player.transform.root;
            }
        }

        CacheAvatarMeshes();
    }

    private void CacheAvatarMeshes()
    {
        if (avatarRoot == null)
        {
            skinnedRenderers = null;
            staticFilters = null;
            bakedMeshes = null;
            return;
        }

        skinnedRenderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        bakedMeshes = new Mesh[skinnedRenderers.Length];
        for (int i = 0; i < bakedMeshes.Length; i++)
        {
            bakedMeshes[i] = new Mesh { name = "AvatarSilhouetteBake" };
            bakedMeshes[i].MarkDynamic();
        }

        MeshFilter[] allFilters = avatarRoot.GetComponentsInChildren<MeshFilter>(true);
        List<MeshFilter> validFilters = new List<MeshFilter>(allFilters.Length);
        foreach (MeshFilter filter in allFilters)
        {
            if (filter == null || filter == meshFilter)
            {
                continue;
            }

            validFilters.Add(filter);
        }

        staticFilters = validFilters.ToArray();
    }

    private bool BuildAvatarSilhouette()
    {
        if (avatarRoot == null)
        {
            return false;
        }

        vertices.Clear();
        triangles.Clear();
        Vector3 rootPosition = avatarRoot.position;

        if (skinnedRenderers != null)
        {
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinned = skinnedRenderers[i];
                if (skinned == null || !skinned.enabled ||
                    !skinned.gameObject.activeInHierarchy || skinned.sharedMesh == null)
                {
                    continue;
                }

                Mesh baked = bakedMeshes[i];
                // 引数なしのBakeMeshはグローバルスケール込みで頂点を焼き込む。
                // (useScale:true はローカルスケールのみで、親の×5が反映されない)
                // 変換は位置+回転のみ（スケールを掛けると二重適用で巨大化する）
                skinned.BakeMesh(baked);
                AppendProjected(baked, skinned.transform, rootPosition, applyScale: false);
            }
        }

        if (staticFilters != null)
        {
            foreach (MeshFilter filter in staticFilters)
            {
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                MeshRenderer partRenderer = filter.GetComponent<MeshRenderer>();
                if (partRenderer == null || !partRenderer.enabled ||
                    !partRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                AppendProjected(filter.sharedMesh, filter.transform, rootPosition, applyScale: true);
            }
        }

        if (vertices.Count == 0)
        {
            return false;
        }

        silhouetteMesh.Clear();
        silhouetteMesh.SetVertices(vertices);
        silhouetteMesh.SetTriangles(triangles, 0);
        silhouetteMesh.RecalculateBounds();
        return true;
    }

    private void AppendProjected(Mesh source, Transform sourceTransform, Vector3 rootPosition, bool applyScale)
    {
        vertexBuffer.Clear();

        try
        {
            source.GetVertices(vertexBuffer);
        }
        catch
        {
            return;
        }

        int[] sourceTriangles;
        try
        {
            sourceTriangles = source.triangles;
        }
        catch
        {
            return;
        }

        int vertexOffset = vertices.Count;
        Vector3 position = sourceTransform.position;
        Quaternion rotation = sourceTransform.rotation;
        for (int i = 0; i < vertexBuffer.Count; i++)
        {
            // BakeMesh(mesh, true) 済みの頂点はスケール反映済みなので、位置+回転のみで変換する
            // （TransformPointを使うとスケールが二重に掛かり巨大化する）
            Vector3 world = applyScale
                ? sourceTransform.TransformPoint(vertexBuffer[i])
                : position + rotation * vertexBuffer[i];
            vertices.Add(new Vector3(world.x - rootPosition.x, world.y - rootPosition.y, 0f));
        }

        for (int i = 0; i < sourceTriangles.Length; i++)
        {
            triangles.Add(vertexOffset + sourceTriangles[i]);
        }
    }

    private void EnsureSilhouetteObject()
    {
        if (silhouetteObject != null)
        {
            return;
        }

        // 注意: wallmanagerの子にすると RandomWallLooper が「壁」として
        // 自動登録してしまう（ランダムに選ばれて単独で流れてきたり、
        // SetActive(false)で消されたりする）ため、シーン直下に置く
        silhouetteObject = new GameObject("Avatar Silhouette Projection");
        silhouetteMesh = new Mesh
        {
            name = "AvatarSilhouetteMesh",
            indexFormat = IndexFormat.UInt32
        };
        silhouetteMesh.MarkDynamic();
        meshFilter = silhouetteObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = silhouetteMesh;
        meshRenderer = silhouetteObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        UpdateMaterial();
    }

    private void UpdateMaterial()
    {
        if (silhouetteMaterial == null)
        {
            // ZTest Always を持つ専用シェーダーを優先する
            // （URP UnlitはZTestを変更できず、アバター本体にシルエットが隠れてしまう）
            Shader shader = Shader.Find("Custom/AvatarSilhouette");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            silhouetteMaterial = new Material(shader);
            silhouetteMaterial.name = "Avatar Silhouette Material";
            silhouetteMaterial.renderQueue = (int)RenderQueue.Transparent + 100;

            if (silhouetteMaterial.HasProperty("_Cull"))
            {
                silhouetteMaterial.SetInt("_Cull", (int)CullMode.Off);
            }

            if (silhouetteMaterial.HasProperty("_ZWrite"))
            {
                silhouetteMaterial.SetInt("_ZWrite", 0);
            }

            if (silhouetteMaterial.HasProperty("_ZTest"))
            {
                // カメラはアバターの真後ろにあり、通常の深度テストだと
                // シルエットがアバター本体に隠れて見えないため、常に手前に描く
                silhouetteMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }

            meshRenderer.sharedMaterial = silhouetteMaterial;
        }

        if (silhouetteMaterial.HasProperty("_BaseColor"))
        {
            silhouetteMaterial.SetColor("_BaseColor", silhouetteColor);
        }

        if (silhouetteMaterial.HasProperty("_Color"))
        {
            silhouetteMaterial.SetColor("_Color", silhouetteColor);
        }
    }

    private void PlaceOnCurrentWall()
    {
        Transform wallTransform = wallLooper.CurrentWall.transform;
        Camera cam = Camera.main;
        float zSign = cam != null && cam.transform.position.z < wallTransform.position.z
            ? -1f
            : 1f;
        Vector3 rootPosition = avatarRoot.position;
        float wallZ = wallTransform.position.z + zSign * wallSurfaceOffset;

        // 壁の面に映る影として、アバターの真正面(同じX・同じ高さ)に実寸で貼り付ける。
        // 壁が遠いときに小さく見えるのは、実物の影と同じ遠近法による自然な見え方。
        silhouetteObject.transform.position = new Vector3(
            rootPosition.x + silhouetteOffset.x,
            rootPosition.y + silhouetteOffset.y,
            wallZ);
        silhouetteObject.transform.rotation = Quaternion.identity;
        silhouetteObject.transform.localScale = Vector3.one * silhouetteScale;
    }

    private void SetVisible(bool visible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
    }

    private void OnDestroy()
    {
        if (bakedMeshes != null)
        {
            for (int i = 0; i < bakedMeshes.Length; i++)
            {
                if (bakedMeshes[i] != null)
                {
                    Destroy(bakedMeshes[i]);
                }
            }
        }

        if (silhouetteMesh != null)
        {
            Destroy(silhouetteMesh);
        }

        if (silhouetteMaterial != null)
        {
            Destroy(silhouetteMaterial);
        }
    }
}
