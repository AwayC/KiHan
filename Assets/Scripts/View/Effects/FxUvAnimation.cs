using UnityEngine;

namespace KiHan.View.Effects
{
    /// <summary>
    /// 原版 FXMaker NcUvAnimation 的还原：贴图按速度滚动 (每秒偏移量)，并设置平铺
    /// </summary>
    public sealed class FxUvAnimation : FxAnimatedBase
    {
        public Vector2 scrollSpeed;
        public Vector2 tiling = Vector2.one;
        public Vector2 offset;
        public bool repeat = true;

        protected override void Sample(float time)
        {
            FxRendererState state = State;
            if (state == null) return;
            Vector2 o = offset + scrollSpeed * time;
            if (repeat) o = new Vector2(Mathf.Repeat(o.x, 1f), Mathf.Repeat(o.y, 1f));
            state.Tiling = tiling;
            state.ScrollOffset = o;
        }
    }
}
