using UnityEngine;

/// <summary>
/// Keyboard controls and a small status panel for the standalone portfolio demo.
/// This is not the scoring or game controller used in the team project.
/// </summary>
public class DemoAvatarController : MonoBehaviour
{
    public WallPassJudge contactSystem;
    public float moveSpeed = 2.2f;
    public float moveLimit = 3f;

    private void Update()
    {
        float direction = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction += 1f;

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x + direction * moveSpeed * Time.deltaTime, -moveLimit, moveLimit);
        transform.position = position;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(14, 14, 385, 118), "WALL CONTACT - SYSTEM DEMO");
        GUI.Label(new Rect(30, 43, 350, 22), "A / D or Left / Right: move the avatar");
        GUI.Label(new Rect(30, 67, 350, 22), "Wall contact markers: " +
            (contactSystem != null ? contactSystem.LastHitPositions.Count.ToString() : "-"));
        GUI.Label(new Rect(30, 91, 350, 24), "Displays contact locations only; no score or pass/fail.");
    }
}
