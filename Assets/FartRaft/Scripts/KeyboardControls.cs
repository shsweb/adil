using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FartRaft
{
    // Lets you test in the Unity editor without a phone. Disabled on real mobile devices.
    //   P1: hold A to fart, Z / X to walk     P2: hold L to fart, N / M to walk
    //   P3: hold Q to fart, 1 / 2 to walk     P4: hold P to fart, 9 / 0 to walk
    public class KeyboardControls : MonoBehaviour
    {
        public FartSeat[] seats;

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] FartKeys = { Key.A, Key.L, Key.Q, Key.P };
        static readonly Key[] LeftKeys = { Key.Z, Key.N, Key.Digit1, Key.Digit9 };
        static readonly Key[] RightKeys = { Key.X, Key.M, Key.Digit2, Key.Digit0 };
#else
        static readonly KeyCode[] FartKeys = { KeyCode.A, KeyCode.L, KeyCode.Q, KeyCode.P };
        static readonly KeyCode[] LeftKeys = { KeyCode.Z, KeyCode.N, KeyCode.Alpha1, KeyCode.Alpha9 };
        static readonly KeyCode[] RightKeys = { KeyCode.X, KeyCode.M, KeyCode.Alpha2, KeyCode.Alpha0 };
#endif

        void Update()
        {
            if (Application.isMobilePlatform || seats == null) return;
            for (int i = 0; i < seats.Length && i < FartKeys.Length; i++)
            {
                if (seats[i] == null) continue;
                seats[i].KeyHeld = Pressed(FartKeys[i]);
                seats[i].KeyMoveInput = (Pressed(RightKeys[i]) ? 1f : 0f) - (Pressed(LeftKeys[i]) ? 1f : 0f);
            }
        }

#if ENABLE_INPUT_SYSTEM
        static bool Pressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }
#else
        static bool Pressed(KeyCode key)
        {
            return Input.GetKey(key);
        }
#endif
    }
}
