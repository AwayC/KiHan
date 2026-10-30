using UnityEngine;

namespace KiHan.View.Effects
{
    /// <summary>
    /// 原版 FXMaker NcBillboard 的还原：面片始终正对镜头 (战斗相机不旋转，世界旋转归零即正对)，
    /// rotationMode 1 = 开始时绕 rotationAxis 随机旋转，2 = 按 rotationValue (度/秒) 持续旋转
    /// </summary>
    public sealed class FxBillboard : FxAnimatedBase
    {
        public int rotationMode;

        [Tooltip("0 = x, 1 = y, 2 = z")]
        public int rotationAxis = 2;

        public float rotationValue;

        private float _randomAngle;

        protected override void Restart()
        {
            _randomAngle = Random.Range(0f, rotationValue);
        }

        protected override void Sample(float time)
        {
            float angle = rotationMode == 1 ? _randomAngle : rotationMode == 2 ? rotationValue * time : 0f;
            Vector3 axis = rotationAxis == 0 ? Vector3.right : rotationAxis == 1 ? Vector3.up : Vector3.forward;
            transform.rotation = Quaternion.AngleAxis(angle, axis);
        }
    }
}
