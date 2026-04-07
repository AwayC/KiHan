using UnityEngine;

public class PlayerView : MonoBehaviour
{
    public LogicEntity BindEntity;
    public float SmoothSpeed = 15f;
    private SpriteRenderer _sr;

    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    private void LateUpdate()
    {
        if (BindEntity == null) return;

        // 坐标插值
        Vector3 targetPos = new Vector3(BindEntity.LogicPos.x, BindEntity.LogicPos.y, 0);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * SmoothSpeed);

        // 贴图
        _sr.sprite = BindEntity.GetCurrentSprite();
        _sr.flipX = BindEntity.IsFacingLeft;
    }
}