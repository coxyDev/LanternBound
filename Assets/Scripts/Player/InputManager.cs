using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static PlayerInput PlayerInput;
    public static Animator animator;

    public static Vector2 Movement;
    public static bool JumpWasPressed;
    public static bool JumpIsHeld;
    public static bool JumpWasReleased;
    public static bool RunIsHeld;
    public static bool DashWasPressed;

    // Lantern inputs (separate from movement)
    public static bool LanternTogglePressed;
    public static Vector2 MousePosition;
    public static Vector2 RightStickInput;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _runAction;
    private InputAction _dashAction;
    private InputAction _lanternToggleAction;
    private InputAction _mousePositionAction;
    private InputAction _rightStickAction;

    private void Awake()
    {
        PlayerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();

        _moveAction = PlayerInput.actions["Move"];
        _jumpAction = PlayerInput.actions["Jump"];
        _runAction = PlayerInput.actions["Run"];
        _dashAction = PlayerInput.actions["Dash"];

        try
        {
            _lanternToggleAction = PlayerInput.actions["LanternToggle"];
        }
        catch
        {
            Debug.LogWarning("LanternToggle action not found in Input Actions. Add it if you want to toggle the lantern.");
        }

        try
        {
            _mousePositionAction = PlayerInput.actions["MousePosition"];
        }
        catch
        {
            Debug.LogWarning("MousePosition action not found. Lantern will use Unity's Input.mousePosition instead.");
        }

        try
        {
            _rightStickAction = PlayerInput.actions["RightStick"];
        }
        catch
        {
            Debug.LogWarning("RightStick action not found. Controller lantern aiming won't work.");
        }
    }

    private void Update()
    {
        Movement = _moveAction.ReadValue<Vector2>();

        JumpWasPressed = _jumpAction.WasPressedThisFrame();
        JumpIsHeld = _jumpAction.IsPressed();
        JumpWasReleased = _jumpAction.WasReleasedThisFrame();

        RunIsHeld = _runAction.IsPressed();
        DashWasPressed = _dashAction.WasPressedThisFrame();

        LanternTogglePressed = _lanternToggleAction?.WasPressedThisFrame() ?? false;

        // Mouse position for lantern aiming
        if (_mousePositionAction != null)
        {
            MousePosition = _mousePositionAction.ReadValue<Vector2>();
        }
        else
        {
            MousePosition = Input.mousePosition; // Fallback to old input system
        }

        // Right stick for controller lantern aiming
        RightStickInput = _rightStickAction?.ReadValue<Vector2>() ?? Vector2.zero;
    }
}
