using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapLogic
{
    public float MinX = -7f;
    public float MaxX = 7f;
    public float MinY = -0.9f;
    public float MaxY = 0.9f;

    public Vector3 ClampPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, MinX, MaxX);
        pos.y = Mathf.Clamp(pos.y, MinY, MaxY);

        return pos;
    }
}
