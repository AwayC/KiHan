using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;

[CreateAssetMenu(fileName = "NewAnimData", menuName = "Naruto/Animation Data")]
public class AnimationFrameData : ScriptableObject
{
    public string AnimName;
    public CharacterSpriteLibrary Library; 
    public bool IsLoop = true;

    [System.Serializable]
    public class AnimStep 
    {
        public int FrameIndex; // 帧库中的索引
        public int Duration = 1;
        public Vector2 RootMotion;
    }

    public List<AnimStep> Steps = new List<AnimStep>();

    // 获取当前步对应的库帧数据 (包含图片、判定盒等)
    public SpriteFrameData GetCurrentFrameData(int stepIndex)
    {
        if (Library == null || stepIndex < 0 || stepIndex >= Steps.Count) return null;
        return Library.GetFrame(Steps[stepIndex].FrameIndex);
    }

    // --- 判定盒获取快捷方法 (适配 MUGEN 逻辑) ---
    public List<LogicBox> GetHurtBoxes(int stepIndex)
    {
        // 如果是 Loop，始终沿用【动画序列】中第一步的判定盒
        int targetStep = (IsLoop && Steps.Count > 0) ? 0 : stepIndex;
        var frameData = GetCurrentFrameData(targetStep);
        return frameData?.HurtBoxes;
    }

    public List<LogicBox> GetHitBoxes(int stepIndex)
    {
        int targetStep = (IsLoop && Steps.Count > 0) ? 0 : stepIndex;
        var frameData = GetCurrentFrameData(targetStep);
        return frameData?.HitBoxes;
    }
}
