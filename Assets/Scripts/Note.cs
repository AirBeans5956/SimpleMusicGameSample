using UnityEngine;

/// <summary>
/// ノーツオブジェクトのComponent
/// </summary>
public class Note : MonoBehaviour
{
    /// <summary>
    /// このノートがどのキーが判定対象か
    /// UnityのKeyCode型
    /// </summary>
    public KeyCode targetKey = KeyCode.D;
    /// <summary>
    /// このノートのタイミング(秒)
    /// </summary>
    public float time;
}
