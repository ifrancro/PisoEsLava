using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HeatRise
{
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        public static Vector2 Move
        {
            get
            {
                Keyboard k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                    - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                    - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool Jump => Keyboard.current?.spaceKey.wasPressedThisFrame == true;
        public static bool Small => Keyboard.current?.qKey.wasPressedThisFrame == true;
        public static bool Large => Keyboard.current?.eKey.wasPressedThisFrame == true;
        public static bool Normal => Keyboard.current?.rKey.wasPressedThisFrame == true;
        public static bool Push => Keyboard.current?.fKey.wasPressedThisFrame == true;
        public static bool Align => Keyboard.current?.cKey.wasPressedThisFrame == true;
        public static bool Confirm => Keyboard.current?.enterKey.wasPressedThisFrame == true;
        public static bool Pause => Keyboard.current?.escapeKey.wasPressedThisFrame == true
            || Keyboard.current?.pKey.wasPressedThisFrame == true;
        public static bool OrbitHeld => Mouse.current?.rightButton.isPressed == true
            || Mouse.current?.leftButton.isPressed == true
            || Mouse.current?.middleButton.isPressed == true;
        public static Vector2 MousePosition => Mouse.current != null
            ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static float Scroll => Mouse.current != null
            ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        public static Vector2 Move
        {
            get
            {
                float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool Jump => Input.GetKeyDown(KeyCode.Space);
        public static bool Small => Input.GetKeyDown(KeyCode.Q);
        public static bool Large => Input.GetKeyDown(KeyCode.E);
        public static bool Normal => Input.GetKeyDown(KeyCode.R);
        public static bool Push => Input.GetKeyDown(KeyCode.F);
        public static bool Align => Input.GetKeyDown(KeyCode.C);
        public static bool Confirm => Input.GetKeyDown(KeyCode.Return);
        public static bool Pause => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        public static bool OrbitHeld => Input.GetMouseButton(1) || Input.GetMouseButton(0) || Input.GetMouseButton(2);
        public static Vector2 MousePosition => Input.mousePosition;
        public static float Scroll => Input.mouseScrollDelta.y;
#endif
    }
}
