// Stub local de UnityEngine.InputSystem para verificación de tipos (no va al repo).
using System;
namespace UnityEngine.InputSystem
{
    public enum InputActionType { Value, Button, PassThrough }
    public class InputAction
    {
        public InputAction(string name, InputActionType type) {}
        public void AddBinding(string path) {}
        public void Enable() {}
        public void Disable() {}
        public void Dispose() {}
        public bool WasPressedThisFrame() => false;
        public bool IsPressed() => false;
        public T ReadValue<T>() => default(T);
        public event Action<CallbackContext> started;
        public event Action<CallbackContext> performed;
        public event Action<CallbackContext> canceled;
        public struct CallbackContext
        {
            public T ReadValue<T>() => default(T);
        }
    }
    public class InputActionAsset
    {
        public InputAction this[string name] => null;
    }
    public class PlayerInput : Behaviour
    {
        public InputActionAsset actions => null;
    }
}
