using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimData", menuName = "Naruto/Animation Data")]
public class AnimationFrameData : ScriptableObject
{
    public string AnimName;
    public float FrameRate = 15f; // 逻辑帧率，如 15 代表 1 帧 = 0.066s
    public bool IsLoop = true;

    [System.Serializable]
    public class FrameInfo
    {
        public Sprite Sprite;

        [Tooltip("这一帧持续的逻辑 Tick 数量。如果解包编号从 0002 跳到 0012，这里填 10")]
        public int Duration = 1;

        [Header("物理/战斗数据")]
        public Rect HurtBox;
        public Rect HitBox;
        public Vector2 RootMotion; // 这一帧带来的逻辑位移
    }

    public List<FrameInfo> Frames = new List<FrameInfo>();
}