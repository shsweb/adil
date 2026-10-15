using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FartRaft
{
    // Lets you test in the Unity editor without a phone. Disabled on real mobile devices.
    // Each player holds a direction (where the raft should go), like the on-screen joystick:
    //   P1: W A S D    P2: arrow keys    P3: I J K L    P4: numpad 8 4 5 6
    public class KeyboardControls : MonoBehaviour
    {
        public FartSeat[] seats;

#if ENABLE_INPUT_SYSTEM
        static readonly Key[,] Keys =
        {
            { Key.W, Key.A, Key.S, Key.D },
            { Key.UpArrow, Key.LeftArrow, Key.DownArrow, Key.RightArrow },
            { Key.I, Key.J, Key.K, Key.L },
            { Key.Numpad8, Key.Numpad4, Key.Numpad5, Key.Numpad6 },
        };
#else
        static readonly KeyCode[,] Keys =
        {
            { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D },
            { KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow },
            { KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L },
            { KeyCode.Keypad8, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6 },
        };
#endif

        void Update()
        {
            if (Application.isMobilePlatform || seats == null) return;
            for (int i = 0; i < seats.Length && i < Keys.GetLength(0); i++)
            {
                if (seats[i] == null) continue;
                float x = (Pressed(Keys[i, 3]) ? 1f : 0f) - (Pressed(Keys[i, 1]) ? 1f : 0f);
                float y = (Pressed(Keys[i, 0]) ? 1f : 0f) - (Pressed(Keys[i, 2]) ? 1f : 0f);
                seats[i].KeyStickInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
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
