using KiHan.Config;
using KiHan.View.Render;
using UnityEngine;

/// <summary>
/// 3D 地图的两台背景透视相机 (由 CameraControllor 持有并逐帧同步)：
/// 1. 远景相机 (depth = 主相机 - 2，清屏底色，Layer Map3DFar)：天穹、云层、远景建筑；
/// 2. 地台相机 (depth = 主相机 - 1，只清深度，Layer Map3D)：脚下地台与相连的近景。
/// 主相机 (正交) 只清深度后叠画角色。
///
/// 两台相机都按「地面纵深中线」(Y = 0, Z = 0 的直线) 求视野与移轴 (m02 / m12)，对齐主相机的画面中心与屏幕内旋转。
/// 地台相机的中线与正交主相机逐像素重合：物距 / 机位高度 / 俯仰任意调，镜头平移 / 缩放 / 旋转时角色脚底都不滑动；
/// 不俯仰时地面纵深 Z 处映射到屏幕纵坐标 高度斜率 × D × Z / (D + Z)
/// </summary>
public sealed class Map3DCameraRig
{
    private readonly Camera _main;
    private readonly Camera _far;
    private readonly Camera _stage;
    private Map3DCameraSettings _settings;

    public bool Active => _settings != null;

    public Map3DCameraRig(Camera main)
    {
        _main = main;
        _far = CreateCamera(main, "BgFar3DCamera", main.depth - 2f, CameraClearFlags.SolidColor, BattleViewSpace.Map3DFarLayer, 1f, 1000f);
        _stage = CreateCamera(main, "BgStage3DCamera", main.depth - 1f, CameraClearFlags.Depth, BattleViewSpace.Map3DLayer, 0.5f, 300f);
    }

    /// <summary>
    /// 切换地图时调用：settings 为 null 时关闭 3D 相机 (2D 背景板地图)。
    /// 保存的是 MapConfig 中对象的引用，Play 模式下修改地图资源即时生效
    /// </summary>
    public void Apply(Map3DCameraSettings settings)
    {
        _settings = settings;
        _far.enabled = settings != null;
        _stage.enabled = settings != null;
        _main.clearFlags = settings != null ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
    }

    /// <summary>
    /// 与主相机同步：camX / camY 为主相机位置，size 为正交尺寸，roll 为屏幕内旋转 (度)
    /// </summary>
    public void Sync(float camX, float camY, float size, float roll)
    {
        if (_settings == null) return;
        float aspect = _main.aspect;
        size = Mathf.Max(0.01f, size);

        Place(_stage, _settings.stageDistance, _settings.stageHeightSlope, _settings.stagePitch, camX, camY, size, aspect, roll);

        _far.backgroundColor = _settings.farBackgroundColor;
        Place(_far, _settings.farDistance, _settings.farHeightSlope, _settings.farPitch,
            camX * _settings.farParallaxScale, camY + _settings.farVerticalOffset, size * _settings.farZoomScale, aspect, roll);
    }

    /// <summary>
    /// 机位 (camX, 高度斜率 × 物距, -物距)，俯仰 pitch、屏幕内旋转 roll。
    /// 视野与移轴按「地面纵深中线」(Y = 0, Z = 0 的直线) 求解：这条线在任何俯仰下都与画面横轴平行，
    /// 令它的横向比例等于正交 (1 世界单位 = 1 / size 半屏)，且线上 x = camX 的点落在正交主相机的同一屏幕位置，
    /// 于是中线上的地面与正交角色逐像素重合；俯仰只让竖直物体产生上下汇聚 (三点透视)
    /// </summary>
    private static void Place(Camera cam, float distance, float heightSlope, float pitch, float camX, float camY, float size, float aspect, float roll)
    {
        cam.transform.SetPositionAndRotation(new Vector3(camX, heightSlope * distance, -distance), Quaternion.Euler(pitch, 0f, roll));

        // 中线参考点在相机空间的坐标 (Unity 相机空间朝 -Z 看)
        Vector3 v = cam.worldToCameraMatrix.MultiplyPoint3x4(new Vector3(camX, 0f, 0f));
        float depth = Mathf.Max(0.1f, -v.z);
        float cot = depth / size;
        float fov = 2f * Mathf.Atan(1f / cot) * Mathf.Rad2Deg;
        cam.fieldOfView = fov;

        // 正交主相机下该点相对画面中心 (camX, camY) 的偏移 (0, -camY)，按屏幕内旋转分解为屏幕坐标
        float rad = roll * Mathf.Deg2Rad;
        float orthoX = -camY * Mathf.Sin(rad) / (size * aspect);
        float orthoY = -camY * Mathf.Cos(rad) / size;

        // 透视下该点的屏幕坐标为 m00·x/depth - m02，m11·y/depth - m12；移轴使其与正交一致
        Matrix4x4 proj = Matrix4x4.Perspective(fov, aspect, cam.nearClipPlane, cam.farClipPlane);
        proj.m02 = cot / aspect * v.x / depth - orthoX;
        proj.m12 = cot * v.y / depth - orthoY;
        cam.projectionMatrix = proj;
    }

    private static Camera CreateCamera(Camera main, string name, float depth, CameraClearFlags clear, int layer, float near, float far)
    {
        var go = new GameObject(name);
        go.transform.SetParent(main.transform, false);
        var cam = go.AddComponent<Camera>();
        cam.orthographic = false;
        cam.depth = depth;
        cam.clearFlags = clear;
        cam.cullingMask = 1 << layer;
        cam.nearClipPlane = near;
        cam.farClipPlane = far;
        cam.enabled = false;
        return cam;
    }
}
