using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Player
{
    /// <summary>
    /// Polls keyboard + gamepad through the new Input System and exposes intent
    /// (move axis, buffered presses). States read intent; they never touch devices.
    /// Keyboard: A/D or arrows move, Space/W/Up jump, J/Left-click attack, K/Shift dodge.
    /// </summary>
    public sealed class PlayerInputReader
    {
        public float Move { get; private set; }
        public bool JumpHeld { get; private set; }
        public float JumpPressedAt { get; private set; } = -99f;
        public float AttackPressedAt { get; private set; } = -99f;
        public float DodgePressedAt { get; private set; } = -99f;
        public bool Enabled { get; set; } = true;

        public void Poll(float now)
        {
            if (!Enabled) { Move = 0; JumpHeld = false; return; }
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var mouse = Mouse.current;

            float move = 0f;
            bool jumpDown = false, jumpHeld = false, attack = false, dodge = false;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += 1f;
                jumpDown |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
                jumpHeld |= kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;
                attack |= kb.jKey.wasPressedThisFrame;
                dodge |= kb.kKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame;
            }
            if (mouse != null) attack |= mouse.leftButton.wasPressedThisFrame;
            if (pad != null)
            {
                float x = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(x) > 0.25f) move = Mathf.Sign(x);
                if (pad.dpad.left.isPressed) move = -1f;
                if (pad.dpad.right.isPressed) move = 1f;
                jumpDown |= pad.buttonSouth.wasPressedThisFrame;
                jumpHeld |= pad.buttonSouth.isPressed;
                attack |= pad.buttonWest.wasPressedThisFrame;
                dodge |= pad.rightShoulder.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
            }

            Move = Mathf.Clamp(move, -1f, 1f);
            JumpHeld = jumpHeld;
            if (jumpDown) JumpPressedAt = now;
            if (attack) AttackPressedAt = now;
            if (dodge) DodgePressedAt = now;
        }

        // Buffered presses: a press counts for `buffer` seconds and is cleared when used.
        public bool HasJump(float now, float buffer) => now - JumpPressedAt <= buffer;
        public bool HasAttack(float now, float buffer) => now - AttackPressedAt <= buffer;
        public bool HasDodge(float now, float buffer) => now - DodgePressedAt <= buffer;
        public void ClearJump() => JumpPressedAt = -99f;
        public void ClearAttack() => AttackPressedAt = -99f;
        public void ClearDodge() => DodgePressedAt = -99f;
    }
}
