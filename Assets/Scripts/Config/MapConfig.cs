using System;
using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 地图清单：一个文件夹就是一张地图 (Assets/AssetPackages/Maps/&lt;id&gt;/&lt;id&gt;.asset)，选图截图 shot.png 放同目录
    /// 清单直接引用场景预制体与截图，登记到地图注册表 (MapRegistry) 后即可在选图界面使用
    /// 全部地图共用 MapLogic 边界 (见 MapLogic)，地图只决定背景画面
    /// </summary>
    [CreateAssetMenu(fileName = "Map", menuName = "KiHan/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [Tooltip("地图 ID，全局唯一 (与目录名一致，如 01)")]
        public string id;

        [Tooltip("选图界面显示的地图名")]
        public string displayName;

        [Tooltip("场景预制体 (MapManager 实例化)")]
        public GameObject scene;

        [Tooltip("选图界面的地图截图")]
        public Sprite shot;

        [Tooltip("3D 场景：预制体放在原点按 1:1 显示，由地台 / 远景两台透视相机绘制 (见 CameraControllor)；否则为 2D 背景板")]
        public bool is3D;

        [Tooltip("3D 场景的相机参数 (仅 is3D 时生效)。Play 模式下直接改本资源即时生效并保存")]
        public Map3DCameraSettings camera3D = new Map3DCameraSettings();
    }

    /// <summary>
    /// 3D 场景的两台透视相机参数。
    /// 两台相机都按「地面纵深中线」(Y = 0, Z = 0) 求视野与移轴：地台相机的这条线与正交角色脚底逐像素重合，
    /// 物距 / 高度 / 俯仰怎么调都不滑步；不开放地台缩放与平移 (会破坏重合)。远景相机不承载角色，另有视差、平移、缩放
    /// </summary>
    [Serializable]
    public class Map3DCameraSettings
    {
        [Tooltip("地台相机物距 (米)：越大地台透视越弱，地台在画面中的宽度不变")]
        [Range(8f, 80f)]
        public float stageDistance = 32f;

        [Tooltip("地台机位高度 = 斜率 × 物距：0.368 与逻辑纵深一致；调大地面纵向铺得更开 (后沿护栏在画面上升高)，调小地面压扁 (角色可能走过护栏)")]
        [Range(0.05f, 1.2f)]
        public float stageHeightSlope = 0.368f;

        [Tooltip("地台俯仰角 (度)：正值低头，负值抬头；竖直物体会上下汇聚 (三点透视)")]
        [Range(-20f, 30f)]
        public float stagePitch;

        [Tooltip("远景相机物距 (米)：越小远景透视越强")]
        [Range(3f, 60f)]
        public float farDistance = 12f;

        [Tooltip("远景机位高度 = 斜率 × 物距；0.368 与地台一致，调大从高处俯看")]
        [Range(-0.5f, 1.5f)]
        public float farHeightSlope = 0.368f;

        [Tooltip("远景俯仰角 (度)：正值低头，负值抬头")]
        [Range(-45f, 45f)]
        public float farPitch;

        [Tooltip("远景横向视差倍率：镜头横移时远景移动量的倍数 (1 为真实透视)")]
        [Range(0f, 6f)]
        public float farParallaxScale = 1f;

        [Tooltip("远景画面上下平移 (世界单位)：正值下移")]
        [Range(-5f, 5f)]
        public float farVerticalOffset;

        [Tooltip("远景视野倍率：小于 1 放大远景，大于 1 缩小远景")]
        [Range(0.4f, 2.5f)]
        public float farZoomScale = 1f;

        [Tooltip("远景相机底色 (天穹未覆盖处)")]
        public Color farBackgroundColor = new Color(0.28f, 0.34f, 0.42f, 1f);
    }
}
