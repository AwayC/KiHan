using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;

[CreateAssetMenu(fileName = "NewAnimData", menuName = "Naruto/Animation Data")]
public class AnimationFrameData : ScriptableObject
{
    public string AnimName;
    public CharacterSpriteLibrary Library;

    [System.Serializable]
    public class AnimStep
    {
        public int FrameIndex; // 帧库中的索引
        public int Duration = 1;
    }

    public List<AnimStep> Steps = new List<AnimStep>();

    // 获取当前步对应的库帧数据 (包含图片、判定盒等)
    public SpriteFrameData GetCurrentFrameData(int stepIndex)
    {
        if (Library == null || stepIndex < 0 || stepIndex >= Steps.Count) return null;
        return Library.GetFrame(Steps[stepIndex].FrameIndex);
    }

    // --- 判定盒获取方法 ---
    public List<LogicBox> GetHurtBoxes(int stepIndex)
    {
        var frameData = GetCurrentFrameData(stepIndex);
        return frameData?.HurtBoxes;
    }

    public List<LogicBox> GetHitBoxes(int stepIndex)
    {
        var frameData = GetCurrentFrameData(stepIndex);
        return frameData?.HitBoxes;
    }
}