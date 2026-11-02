namespace KiHan.UI
{
    /// <summary>
    /// 流程切换 Loading (GameFlow.RunTransition)：进度条与百分比 (预生成 0%~100% 字符串，不分配)
    /// </summary>
    [UIPanel("UI/Panels/Common/LoadingPanel", UILayer.Loading)]
    public partial class LoadingPanel : UIPanel
    {
        private static readonly string[] Percents = BuildPercents();
        private int _shown = -1;

        protected override void OnOpen()
        {
            _shown = -1;
            SetProgress(0f);
        }

        /// <param name="progress">0 ~ 1</param>
        public void SetProgress(float progress)
        {
            int percent = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(progress * 100f), 0, 100);
            if (percent == _shown) return;
            _shown = percent;
            _slider.value = percent / 100f;
            _percent.text = Percents[percent];
        }

        private static string[] BuildPercents()
        {
            var s = new string[101];
            for (int i = 0; i <= 100; i++) s[i] = i + "%";
            return s;
        }
    }
}
