using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapLogic
{
    public float MinX = -15f;
    public float MaxX = 15f;
    public float MinY = -4f;
    public float MaxY = 4f;

    public Vector3 ClampPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, MinX, MaxX);
        pos.y = Mathf.Clamp(pos.y, MinY, MaxY);

        return pos;
    }
}
