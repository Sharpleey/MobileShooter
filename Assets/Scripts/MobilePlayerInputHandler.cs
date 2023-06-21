using UnityEngine;

public class MobilePlayerInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerCharacterController _characterController;
    [SerializeField] private CharacterCamera _characterCamera;

    [Space(10)]
    [SerializeField] private Transform _cameraFollowPoint;

    [Space(10)]
    [SerializeField] private Joystick _joystickMovement;
    [SerializeField] private Joystick _joystickCamera;


    private Vector3 _lookInputVector = Vector3.zero;

    private PlayerCharacterInputs _inputs = new PlayerCharacterInputs();

    #region Mono
    private void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;

        // Tell camera to follow transform
        _characterCamera.SetFollowTransform(_cameraFollowPoint);

        // Ignore the character's collider(s) for camera obstruction checks
        _characterCamera.IgnoredColliders.Clear();
        _characterCamera.IgnoredColliders.AddRange(_characterController.GetComponentsInChildren<Collider>());
    }

    private void FixedUpdate()
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
        float mouseLookAxisUp = _joystickCamera.Vertical; 
        float mouseLookAxisRight = _joystickCamera.Horizontal;

        _lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

        //// Prevent moving the camera while the cursor isn't locked
        //if (Cursor.lockState != CursorLockMode.Locked)
        //{
        //    _lookInputVector = Vector3.zero;
        //}

        // Apply inputs to the camera
        _characterCamera.UpdateWithInput(Time.deltaTime, _lookInputVector);
    }

    private void HandleCharacterInput()
    {

        // Build the CharacterInputs struct
        _inputs.moveAxisForward = _joystickMovement.Vertical;
        _inputs.moveAxisRight = _joystickMovement.Horizontal;
        _inputs.cameraRotation = _characterCamera.Transform.rotation;
        _inputs.isJumpDown = Input.GetKeyDown(KeyCode.Space);
        _inputs.isJumpHeld = Input.GetKey(KeyCode.Space);
        _inputs.isRollDown = Input.GetKeyDown(KeyCode.LeftShift);
        _inputs.isNoClipDown = Input.GetKeyUp(KeyCode.G);

        // Apply inputs to character
        _characterController.SetInputs(ref _inputs);

      
    }
    #endregion Private methods
}
