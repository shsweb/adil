using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FartRaft
{
    // Lets you test in the Unity editor without a phone: hold A, L, Q, P for
    // players 1-4. Disabled on real mobile devices.
    public class KeyboardControls : MonoBehaviour
    {
        public FartSeat[] seats;

        void Update()
        {
            if (Application.isMobilePlatform || seats == null) return;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null) seats[i].KeyHeld = IsHeld(i);
            }
        }

        static bool IsHeld(int player)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (player)
            {
                case 0: return keyboard.aKey.isPressed;
                case 1: return keyboard.lKey.isPressed;
                case 2: return keyboard.qKey.isPressed;
                default: return keyboard.pKey.isPressed;
            }
#else
            switch (player)
            {
                case 0: return Input.GetKey(KeyCode.A);
                case 1: return Input.GetKey(KeyCode.L);
                case 2: return Input.GetKey(KeyCode.Q);
                default: return Input.GetKey(KeyCode.P);
            }
#endif
        }
    }
}
