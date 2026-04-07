using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimData", menuName = "Naruto/Animation Data")]
public class AnimationFrameData : ScriptableObject
{
    public string AnimName;
    public float FrameRate = 15f; // 逻辑帧率
    public bool IsLoop = true;

    [System.Serializable]
    public class FrameInfo
    {
        public Sprite Sprite;

        public int Duration = 1;

        [Header("物理/战斗数据")]
        public Rect HurtBox;
        public Rect HitBox;
        public Vector2 RootMotion; // 逻辑位移
    }

    public List<FrameInfo> Frames = new List<FrameInfo>();
}