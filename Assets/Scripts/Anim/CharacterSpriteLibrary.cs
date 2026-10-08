using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;

namespace KiHan.Logic
{
    [System.Serializable]
    public class EffectAnchorInfo
    {
        public string Name = "Default";
        public Vector2 Position;
    }

    [System.Serializable]
    public class SpriteFrameData
    {
        public string Name;
        public Sprite Sprite;
        public Vector2 Offset;
        
        public List<EffectAnchorInfo> EffectAnchors = new List<EffectAnchorInfo>();

        public List<LogicBox> HurtBoxes = new List<LogicBox>();
        public List<LogicBox> HitBoxes = new List<LogicBox>();
        public List<EffectLayerInfo> ExtraLayers = new List<EffectLayerInfo>();
    }
}

[CreateAssetMenu(fileName = "NewSpriteLibrary", menuName = "Naruto/Sprite Library")]
public class CharacterSpriteLibrary : ScriptableObject
{
    [Header("判定盒模板 (像素单位)")]
    public LogicBox DefaultHurtBox = new LogicBox(new Vector2(0f, 60f), new Vector2(60f, 140f), 80.0f);
    public LogicBox DefaultHitBox = new LogicBox(new Vector2(40f, 60f), new Vector2(60f, 60f), 80.0f);

    [Header("厚度预设 (Side Presets)")]
    public List<float> SidePresets = new List<float> { 50.0f, 80.0f, 100.0f, 150.0f };

    public List<SpriteFrameData> AllFrames = new List<SpriteFrameData>();

    public SpriteFrameData GetFrame(int index)
    {
        if (index >= 0 && index < AllFrames.Count) return AllFrames[index];
        return null;
    }
}