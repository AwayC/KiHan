using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    public float ParallaxFactor;

    private Transform _cameraTrans;
    private Vector3 _startCameraPos;
    private float _startX; // 初始水平位置

    private void Start()
    {
        if (Camera.main != null)
        {
            _cameraTrans = Camera.main.transform;
            _startCameraPos = _cameraTrans.position;

            _startX = transform.position.x;
        }
    }

    private void LateUpdate()
    {
        if (_cameraTrans == null) return;

        float cameraDeltaX = _cameraTrans.position.x - _startCameraPos.x;

        float offsetX = cameraDeltaX * (1 - ParallaxFactor);

        transform.position = new Vector3(_startX + offsetX, transform.position.y, transform.position.z);
    }
}