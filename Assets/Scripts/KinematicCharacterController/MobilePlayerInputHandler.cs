using UnityEngine;
using UnityEngine.UI;
public class MobilePlayerInputHandler : MonoBehaviour
{
    #region Serialize fields
    [Header("CharacterController")]
    [SerializeField] private PlayerCharacterController _characterController;

    [Header("CharacterCamera")]
    [SerializeField] private CharacterCamera _characterCamera;
    [SerializeField] private Transform _cameraFollowPoint;
    [SerializeField] private float _horizontalSensitivity = 8f;
    [SerializeField] private float _verticalSensitivity = 5f;

    [Header("Joysticks")]
    [SerializeField] private Joystick _movementJoystick;
    [SerializeField] private Joystick _cameraJoystick;

    #endregion Serialize fields

    #region Properties
    /// <summary>
    /// Âûחûגאועסÿ ג EventTrigger ף JumpButton
    /// </summary>
    public bool IsJumpDown
    {
        set
        {
            _inputs.isJumpDown = value;
        }
    }

    /// <summary>
    /// Âûחûגאועסÿ ג EventTrigger ף RollButton
    /// </summary>
    public bool IsRollDown
    {
        set
        {
            _inputs.isRollDown = value;
        }
    }

    /// <summary>
    /// Âûחûגאועסÿ ג EventTrigger ף AimingButton
    /// </summary>
    public bool IsAimingToggle
    {
        set
        {
            _inputs.isAimingToggle = !_inputs.isAimingToggle;
        }
    }

    #endregion Properties

    #region Private fields
    private Vector3 _lookInputVector = Vector3.zero;
    private PlayerCharacterInputs _inputs = new PlayerCharacterInputs();
    #endregion Private fields

    #region Mono
    private void Start()
    {
        // Tell camera to follow transform
        _characterCamera.SetFollowTransform(_cameraFollowPoint);

        // Ignore the character's collider(s) for camera obstruction checks
        _characterCamera.IgnoredColliders.Clear();
        _characterCamera.IgnoredColliders.AddRange(_characterController.GetComponentsInChildren<Collider>());
    }

    private void Update()
    {
        HandleCharacterInput();
    }

    private void LateUpdate()
    {
        HandleCameraInput();
    }
    #endregion Mono

    #region Private methods 
    private void HandleCameraInput()
    {
        // Create the look input vector for the camera
        float mouseLookAxisUp = _cameraJoystick.Vertical * _verticalSensitivity; 
        float mouseLookAxisRight = _cameraJoystick.Horizontal * _horizontalSensitivity;

        _lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

        // Apply inputs to the camera
        _characterCamera.UpdateWithInput(Time.deltaTime, _lookInputVector);
    }

    private void HandleCharacterInput()
    {
        // Build the CharacterInputs struct
        _inputs.moveAxisForward = _movementJoystick.Vertical;
        _inputs.moveAxisRight = _movementJoystick.Horizontal;
        _inputs.cameraRotation = _characterCamera.Transform.rotation;
        _inputs.isNoClipDown = Input.GetKeyUp(KeyCode.G);

        // Apply inputs to character
        _characterController.SetInputs(ref _inputs);
    }
    #endregion Private methods
}
