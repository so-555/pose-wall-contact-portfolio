using UnityEngine;

public class RandomWallLooper : MonoBehaviour
{
    [Header("移動の設定")]
    [SerializeField] private Vector3 moveDirection = Vector3.back; // Z軸マイナス方向
    [SerializeField] private float maxDistance = 15.0f;           // 進む最大距離
    [SerializeField] private float speed = 5.0f;                 // 移動速度

    [Header("壁のリスト（子要素が自動で入ります）")]
    [SerializeField] private GameObject[] walls;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private GameObject currentWall; // 現在出現している壁

    // WallPassJudge（通過判定）から参照するための公開プロパティ
    public GameObject CurrentWall => currentWall;          // いま出ている壁
    public float Progress01 { get; private set; }          // 壁の進行度 0〜1
    public Vector3 MoveDirection => moveDirection.normalized;

    void Start()
    {
        // 開始位置と目標地点を計算
        startPosition = transform.position;
        targetPosition = startPosition + moveDirection.normalized * maxDistance;

        // もしインスペクターで壁が登録されていなければ、子要素から自動取得
        if (walls == null || walls.Length == 0)
        {
            walls = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                walls[i] = transform.GetChild(i).gameObject;
            }
        }

        // 開始時はすべての壁を隠し、選んだ1枚だけを表示する
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
            {
                walls[i].SetActive(false);
            }
        }

        ActivateRandomWall();
    }

    void Update()
    {
        // 親（WallManager）ごと移動させる
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // 進行度を更新（判定タイミングに使う）
        Progress01 = maxDistance > 0f
            ? Mathf.Clamp01(Vector3.Distance(startPosition, transform.position) / maxDistance)
            : 0f;

        // 目標地点に到達したらリセット
        if (transform.position == targetPosition)
        {
            ResetAndChangeWall();
        }
    }

    // 壁を初期位置に戻し、ランダムに切り替える関数
    void ResetAndChangeWall()
    {
        // 今の壁を非表示にする
        if (currentWall != null)
        {
            currentWall.SetActive(false);
        }

        // 位置をスタートに戻す
        transform.position = startPosition;
        Progress01 = 0f;

        // 新しい壁をランダムに選んで表示
        ActivateRandomWall();
    }

    // ランダムに壁を1つ選んでアクティブにする関数
    void ActivateRandomWall()
    {
        if (walls.Length == 0) return;

        // 0 ～ 壁の総数の間でランダムな数字を引く
        int randomIndex = Random.Range(0, walls.Length);

        // 選ばれた壁を記録して表示する
        currentWall = walls[randomIndex];
        currentWall.SetActive(true);
    }
}
