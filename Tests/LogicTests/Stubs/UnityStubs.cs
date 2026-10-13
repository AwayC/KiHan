// Unity 替身 (只用于 Tests/LogicTests 命令行测试，不进入游戏)
// 规则：只放逻辑层用到的 Unity 类型；凡是逻辑会依赖其结果的 API (如 Mathf、Vector2Int.RoundToInt) 必须与 Unity 语义一致；
//       不在这里写任何游戏逻辑。逻辑层新用到 Unity API 时在此补充。详见 .agents/testing.md
using System;
namespace UnityEngine {
  public class Object { public string name; public static void Destroy(Object o){} }
  public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>(){return default;} public T[] GetComponentsInChildren<T>(bool b=false){return null;} public T GetComponentInChildren<T>(bool b=false){return default;} }
  public class Behaviour : Component { public bool enabled; }
  public class MonoBehaviour : Behaviour {}
  public class ScriptableObject : Object {}
  public class GameObject : Object { public Transform transform; public int GetInstanceID(){return 0;} }
  public class Transform : Component { public Vector3 position; public Vector3 localPosition; }
  public class Sprite : Object {}
  public class TextAsset : Object { public string text; }
  public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white => new Color(1,1,1,1); }
  public struct Quaternion { public static Quaternion identity => default; }
  public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>default;
    public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y); public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
    public static Vector2 operator*(Vector2 a,float d)=>new Vector2(a.x*d,a.y*d); public static Vector2 operator/(Vector2 a,float d)=>new Vector2(a.x/d,a.y/d);
    public static Vector2 Min(Vector2 a,Vector2 b)=>a; public static Vector2 Max(Vector2 a,Vector2 b)=>b; public static float Distance(Vector2 a,Vector2 b)=>0; }
  public struct Vector2Int { public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;} public static Vector2Int RoundToInt(Vector2 v)=>new Vector2Int((int)System.Math.Round(v.x),(int)System.Math.Round(v.y)); public static Vector2Int zero=>default; }
  public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public Vector3(float x,float y){this.x=x;this.y=y;this.z=0;} public static Vector3 zero=>default; public static Vector3 forward=>default; }
  public static class Mathf { public static float Clamp(float v,float a,float b)=>v<a?a:(v>b?b:v); public static int Clamp(int v,int a,int b)=>v<a?a:(v>b?b:v); public static float Abs(float v)=>System.Math.Abs(v); public static int Max(int a,int b)=>System.Math.Max(a,b); public static float Max(float a,float b)=>System.Math.Max(a,b); public static int Min(int a,int b)=>System.Math.Min(a,b); public static float Min(float a,float b)=>System.Math.Min(a,b); public static float Clamp01(float v)=>Clamp(v,0f,1f); public static int RoundToInt(float f)=>(int)System.Math.Round(f); }
  public static class Debug { public static void Log(object o){} public static void LogWarning(object o){} public static int ErrorCount; public static string LastError; public static void LogError(object o){ ErrorCount++; LastError = o?.ToString(); } }
  public static class Time { public static float deltaTime; }
  public static class Resources { public static T Load<T>(string p) where T:Object => default; }
  public static class JsonUtility { public static T FromJson<T>(string s)=>default; }
  public class TooltipAttribute : Attribute { public TooltipAttribute(string s){} }
  public class HeaderAttribute : Attribute { public HeaderAttribute(string s){} }
  public class HideInInspector : Attribute {}
  public class SerializeField : Attribute {}
  public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
}
