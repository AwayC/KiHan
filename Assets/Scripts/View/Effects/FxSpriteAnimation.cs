using UnityEngine;

namespace KiHan.View.Effects
{
    /// <summary>
    /// 原版 FXMaker NcSpriteAnimation 的还原：贴图按 tiles 切成网格 (左上为第 0 帧，逐行)，按 fps 播放
    /// 延迟期间隐藏；不循环时播完停在最后一帧，autoDestruct 则隐藏
    /// </summary>
    public sealed class FxSpriteAnimation : FxAnimatedBase
    {
        public float delay;
        public Vector2Int tiles = new Vector2Int(1, 1);
        public int startFrame;
        public int frameCount = 1;
        public float fps = 30f;
        public bool loop;

        [Tooltip("循环次数，0 为无限")]
        public int loopingCount;

        public bool autoDestruct;

        protected override void Sample(float time)
        {
            FxRendererState state = State;
            if (state == null) return;

            float local = time - delay;
            if (local < 0f)
            {
                state.SelfHidden = true;
                return;
            }

            int count = Mathf.Max(1, frameCount);
            int index = Mathf.FloorToInt(local * fps);
            bool finished = loop ? loopingCount > 0 && index >= count * loopingCount : index >= count;
            state.SelfHidden = finished && autoDestruct;
            int frame = startFrame + (finished ? count - 1 : index % count);

            int tx = Mathf.Max(1, tiles.x), ty = Mathf.Max(1, tiles.y);
            int col = frame % tx;
            int row = (frame / tx) % ty;
            state.SpriteScale = new Vector2(1f / tx, 1f / ty);
            state.SpriteOffset = new Vector2((float)col / tx, 1f - (float)(row + 1) / ty);
        }
    }
}
