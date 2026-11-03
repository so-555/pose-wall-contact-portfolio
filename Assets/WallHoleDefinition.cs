using UnityEngine;

public enum WallHoleSide
{
    Left,
    Center,
    Right
}

// 各壁オブジェクトにアタッチし、「通れる穴」のX範囲（ステージ座標）を定義する
public class WallHoleDefinition : MonoBehaviour
{
    public WallHoleSide holeSide = WallHoleSide.Center;
    public float holeMinX = -1.0f;
    public float holeMaxX = 1.0f;

    public bool IsInside(float stageX)
    {
        return stageX >= holeMinX && stageX <= holeMaxX;
    }

#if UNITY_EDITOR
    // Sceneビューで穴範囲を緑で可視化
    private void OnDrawGizmosSelected()
    {
        float y = transform.position.y;
        float z = transform.position.z;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3((holeMinX + holeMaxX) * 0.5f, y, z),
            new Vector3(holeMaxX - holeMinX, 3f, 0.2f));
    }
#endif
}
