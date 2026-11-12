// Stub local de UnityEngine para verificación de tipos FUERA de Unity (no va al repo).
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.x * k, a.y * k, a.z * k);
        public static Vector3 operator *(float k, Vector3 a) => a * k;
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity => new Quaternion();
        public static Quaternion Euler(float x, float y, float z) => identity;
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color green => new Color(0, 1, 0);
        public static Color white => new Color(1, 1, 1);
        public static Color operator *(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        public static Color Lerp(Color a, Color b, float t) => a;
    }
    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Abs(float a) => a < 0 ? -a : a;
        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static Color Lerp(Color a, Color b, float t) => a;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
    public class Object
    {
        public string name;
        public static void DontDestroyOnLoad(Object o) {}
        public static void Destroy(Object o) {}
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static T Instantiate<T>(T original) where T : Object => original;
        public static GameObject Instantiate(GameObject original, Vector3 pos, Quaternion rot) => original;
    }
    public class Component : Object
    {
        public GameObject gameObject => null;
        public Transform transform => null;
        public T GetComponent<T>() => default(T);
        public T GetComponentInParent<T>() => default(T);
        public T GetComponentInChildren<T>() => default(T);
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new T[0];
    }
    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 localPosition;
        public Vector3 localScale;
        public Vector3 localEulerAngles;
        public Vector3 eulerAngles;
        public Quaternion rotation;
        public Quaternion localRotation;
        public Transform parent => null;
        public void SetParent(Transform p, bool worldPositionStays) {}
        public Transform Find(string n) => null;
        public Vector3 forward => Vector3.forward;
        public bool IsChildOf(Transform t) => false;
    }
    public class GameObject : Object
    {
        public Transform transform => null;
        public int layer;
        public GameObject() {}
        public GameObject(string name) {}
        public T AddComponent<T>() where T : Component => default(T);
        public T GetComponent<T>() => default(T);
        public T GetComponentInParent<T>() => default(T);
        public T GetComponentInChildren<T>() => default(T);
        public void SetActive(bool v) {}
        public static GameObject CreatePrimitive(PrimitiveType t) => new GameObject();
        public static GameObject Find(string name) => null;
    }
    public enum PrimitiveType { Cube, Sphere, Cylinder, Capsule, Plane, Quad }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour
    {
        protected virtual void Awake() {}
        protected virtual void Start() {}
        protected virtual void Update() {}
        protected virtual void OnEnable() {}
        protected virtual void OnDisable() {}
        protected virtual void OnDestroy() {}
        protected virtual void LateUpdate() {}
    }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => default(T);
    }
    public class Shader : Object { public static Shader Find(string name) => null; }
    public class Material : Object
    {
        public Material(Shader s) {}
        public Material(Material src) {} // clon (ETSVArt.Tinted); en el stub no copia nada
        public Color color;
        public bool HasProperty(string n) => false;
        public void SetColor(string n, Color c) {}
    }
    public class Renderer : Component
    {
        public Material sharedMaterial;
        public Material material => null;
    }
    public class MeshRenderer : Renderer {}
    public class Font : Object { public Material material => null; }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum TextAlignment { Left, Center, Right }
    public class TextMesh : Component
    {
        public string text;
        public Font font;
        public int fontSize;
        public Color color;
        public TextAnchor anchor;
        public TextAlignment alignment;
        public float characterSize;
    }
    public class Collider : Component { public bool enabled; }
    public class BoxCollider : Collider { public Vector3 size; public Vector3 center; }
    public class Rigidbody : Component
    {
        public float mass;
        public bool isKinematic;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public RigidbodyInterpolation interpolation;
        public void AddForce(Vector3 f, ForceMode m) {}
    }
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum ForceMode { Force, Impulse, VelocityChange, AccelerationChange }
    public struct Ray { public Vector3 origin, direction; public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; } }
    public struct RaycastHit
    {
        public float distance;
        public Collider collider => null;
        public Vector3 point;
    }
    public static class Physics
    {
        public static bool Raycast(Ray r, out RaycastHit hit, float max) { hit = default(RaycastHit); return false; }
        public static RaycastHit[] RaycastAll(Ray r, float max) => new RaycastHit[0];
        public static int RaycastNonAlloc(Ray r, RaycastHit[] results, float max) => 0;
    }
    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }
    public static class Time { public static float deltaTime => 0.016f; public static float time => 0f; }
    public static class Debug
    {
        public static void Log(object o) {}
        public static void LogWarning(object o) {}
        public static void LogError(object o) {}
    }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute {}
    [AttributeUsage(AttributeTargets.Class)] public class RequireComponent : Attribute { public RequireComponent(Type t) {} }
    [AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) {} }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethod : Attribute
    {
        public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t) {}
        public RuntimeInitializeOnLoadMethod() {}
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
    public enum CursorLockMode { None, Locked, Confined }
    public static class Application
    {
        public static void Quit() {}
        public static string persistentDataPath => "/tmp/etsv-persistent";
        public static bool isPlaying => true;
        public static string unityVersion => "6000.3.5f2";
    }
}
