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

    // Fallback for F key if Input Actions not configured
    private bool _useFallbackInput = false;

    private void Awake()
    {
        PlayerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();

        // Core movement actions (these should exist)
        _moveAction = PlayerInput.actions["Move"];
        _jumpAction = PlayerInput.actions["Jump"];
        _runAction = PlayerInput.actions["Run"];
        _dashAction = PlayerInput.actions["Dash"];

        // Lantern action with fallback
        try
        {
            _lanternToggleAction = PlayerInput.actions["LanternToggle"];
            Debug.Log("✓ LanternToggle action found in Input Actions");
        }
        catch
        {
            Debug.LogWarning("⚠️ LanternToggle action not found. Using F key fallback.");
            _useFallbackInput = true;
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

        Debug.Log("✓ InputManager initialized");
    }

    private void Update()
    {
        Movement = _moveAction.ReadValue<Vector2>();

        JumpWasPressed = _jumpAction.WasPressedThisFrame();
        JumpIsHeld = _jumpAction.IsPressed();
        JumpWasReleased = _jumpAction.WasReleasedThisFrame();

        RunIsHeld = _runAction.IsPressed();
        DashWasPressed = _dashAction.WasPressedThisFrame();

        // Lantern toggle with fallback
        if (_useFallbackInput)
        {
            LanternTogglePressed = Input.GetKeyDown(KeyCode.F);
        }
        else
        {
            LanternTogglePressed = _lanternToggleAction?.WasPressedThisFrame() ?? Input.GetKeyDown(KeyCode.F);
        }

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

        // Debug lantern input
        if (LanternTogglePressed)
        {
            Debug.Log("🔦 Lantern toggle input detected!");
        }
    }

    [ContextMenu("Test Lantern Input")]
    public void TestLanternInput()
    {
        Debug.Log($"Lantern Toggle Action: {(_lanternToggleAction != null ? "Found" : "Missing")}");
        Debug.Log($"Using Fallback Input: {_useFallbackInput}");
        Debug.Log("Press F key to test lantern toggle");
    }
}