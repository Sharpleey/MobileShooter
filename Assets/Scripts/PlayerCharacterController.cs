using UnityEngine;
using KinematicCharacterController;
using System.Collections.Generic;

public class PlayerCharacterController : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor motor;

    [Header("Stable Movement")]
    public float maxStableMoveSpeed = 10f;
    public float stableMovementSharpness = 15;
    public float orientationSharpness = 10;
    [SerializeField] private OrientationMethod _orientationMethod = OrientationMethod.TowardsMovement;

    [Header("Air Movement")]
    public float maxAirMoveSpeed = 10f;
    public float airAccelerationSpeed = 5f;
    public float drag = 0.1f;

    [Header("Jumping")]
    public bool allowJumpingWhenSliding = false;
    public bool allowDoubleJump = false;
    public bool allowWallJump = false;
    public float jumpSpeed = 10f;
    public float jumpPreGroundingGraceTime = 0f;
    public float jumpPostGroundingGraceTime = 0f;

    [Header("NoClip")]
    public float NoClipMoveSpeed = 10f;
    public float NoClipSharpness = 15;

    [Header("Misc")]
    //public bool RotationObstruction;
    public Vector3 gravity = new Vector3(0, -30f, 0);
    public bool orientTowardsGravity = true;
    //public Transform MeshRoot;
    public List<Collider> IgnoredColliders = new List<Collider>();


    public PlayerCharacterState CurrentCharacterState { get; private set; }

    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;

    private bool _jumpInputIsHeld = false;
    private bool _jumpRequested = false;
    private bool _jumpConsumed = false;
    private bool _jumpedThisFrame = false;
    private bool _doubleJumpConsumed = false;
    private bool _canWallJump = false;

    private Vector3 _wallJumpNormal;
    private Vector3 _internalVelocityAdd = Vector3.zero;

    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;

    private Animator _animator;

    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
    }

    #region Mono
    void Start()
    {
        motor.CharacterController = this;

        _animator = GetComponentInChildren<Animator>();

        // Handle initial state
        TransitionToState(PlayerCharacterState.Default);
    }
    #endregion Mono

    #region State
    /// <summary>
    /// Handles movement state transitions and enter/exit callbacks
    /// </summary>
    public void TransitionToState(PlayerCharacterState newState)
    {
        PlayerCharacterState tmpInitialState = CurrentCharacterState;
        OnStateExit(tmpInitialState, newState);
        CurrentCharacterState = newState;
        OnStateEnter(newState, tmpInitialState);
    }

    /// <summary>
    /// Event when entering a state
    /// </summary>
    public void OnStateEnter(PlayerCharacterState state, PlayerCharacterState fromState)
    {
        switch (state)
        {
            case PlayerCharacterState.Default:
                {
                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    motor.SetCapsuleCollisionsActivation(false);
                    motor.SetMovementCollisionsSolvingActivation(false);
                    motor.SetGroundSolvingActivation(false);
                    break;
                }
        }
    }

    /// <summary>
    /// Event when exiting a state
    /// </summary>
    public void OnStateExit(PlayerCharacterState state, PlayerCharacterState toState)
    {
        switch (state)
        {
            case PlayerCharacterState.Default:
                {
                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    motor.SetCapsuleCollisionsActivation(true);
                    motor.SetMovementCollisionsSolvingActivation(true);
                    motor.SetGroundSolvingActivation(true);
                    break;
                }
        }
    }
    #endregion State

    /// <summary> 
    /// MyPlayer �������� ��� ������ ����, ����� �������� ���������, ����� � ���� ������� ������.
    /// </summary>
    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        // Handle state transition from input
        if (inputs.isNoClipDown)
        {
            if (CurrentCharacterState == PlayerCharacterState.Default)
            {
                TransitionToState(PlayerCharacterState.NoClip);
            }
            else if (CurrentCharacterState == PlayerCharacterState.NoClip)
            {
                TransitionToState(PlayerCharacterState.Default);
            }
        }

        _jumpInputIsHeld = inputs.isJumpHeld;

        // Clamp input
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.moveAxisRight, 0f, inputs.moveAxisForward), 1f);

        // Calculate camera direction and rotation on the character plane
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.cameraRotation * Vector3.forward, motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.cameraRotation * Vector3.up, motor.CharacterUp).normalized;
        }
        
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, motor.CharacterUp);

        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    // Move and look inputs
                    _moveInputVector = cameraPlanarRotation * moveInputVector;
                    _lookInputVector = cameraPlanarDirection;

                    switch (_orientationMethod)
                    {
                        case OrientationMethod.TowardsCamera:
                            _lookInputVector = cameraPlanarDirection;
                            break;
                        case OrientationMethod.TowardsMovement:
                            _lookInputVector = _moveInputVector.normalized;
                            break;
                    }

                    // Jumping input
                    if (inputs.isJumpDown)
                    {
                        _timeSinceJumpRequested = 0f;
                        _jumpRequested = true;

                        _animator.SetTrigger(HashAnimParam.PlayerIsJump);
                    }

                    // Roll input
                    if(inputs.isRollDown)
                    {
                        if(motor.GroundingStatus.IsStableOnGround)
                        {
                            _animator.SetTrigger(HashAnimParam.PlayerIsRoll);
                        }

                        // ��������� �������
                        AddVelocity(transform.forward * 20f);
                    }

                    _animator.SetBool(HashAnimParam.PlayerOnAiming, inputs.isAimingToggle);

                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    _moveInputVector = inputs.cameraRotation * moveInputVector;
                    _lookInputVector = cameraPlanarDirection;
                    break;
                }
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    break;
                }
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// ����� �� ��������� ���������, ����� ������ ���� ��� �������� � ������ ������. 
    /// ��� ������������ �����, ��� �� ������ ������ �������� ���������.
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    if (_lookInputVector != Vector3.zero && orientationSharpness > 0f)
                    {
                        // ������� ������������ �� �������� � �������� ����������� �������
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-orientationSharpness * deltaTime)).normalized;

                        // ���������� ������� �������� (������� ����� �������������� KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
                    }

                    if (orientTowardsGravity)
                    {
                        // ������� �� ������� �� ��������������� ����������
                        currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -gravity) * currentRotation;
                    }

                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    if (_lookInputVector != Vector3.zero && orientationSharpness > 0f)
                    {
                        // ������� ������������ �� �������� � �������� ����������� �������
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-orientationSharpness * deltaTime)).normalized;

                        // ���������� ������� �������� (������� ����� �������������� KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
                    }
                    if (orientTowardsGravity)
                    {
                        // ������� �� ������� �� ��������������� ����������
                        currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -gravity) * currentRotation;
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// ����� ������������� �������� ��������� ������, ����� ������ ��� ���� �� ������� ������.
    /// ��� ������������ �����, ��� �� ����� ���������� �������� ���������.
    /// </summary>
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    Vector3 targetMovementVelocity = Vector3.zero;
                    if (motor.GroundingStatus.IsStableOnGround)
                    {
                        // ��������������� �������� ��������� �� ������� ����� ����� (��� ���������� ������, ��� �� �� �����, ����� ���� ����������� ��������� � ������� �������� ��� ��������� ������).
                        currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                        // ���������� ������� ��������
                        Vector3 inputRight = Vector3.Cross(_moveInputVector, motor.CharacterUp);
                        Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                        targetMovementVelocity = reorientedInput * maxStableMoveSpeed;

                        // ���������� ��������
                        currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-stableMovementSharpness * deltaTime));

                        // ������ ��������� ��������
                        _animator.SetFloat(HashAnimParam.PlayerVelocity, currentVelocity.magnitude/maxStableMoveSpeed);

                        _animator.SetBool(HashAnimParam.PlayerOnAir, false);
                    }
                    else
                    {
                        // ��������� ���� �����������
                        if (_moveInputVector.sqrMagnitude > 0f)
                        {
                            targetMovementVelocity = _moveInputVector * maxAirMoveSpeed;

                            // �������������� ������� �� ������������ ������� � ������� �������� �������
                            if (motor.GroundingStatus.FoundAnyGround)
                            {
                                Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), motor.CharacterUp).normalized;
                                targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                            }

                            Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity);
                            currentVelocity += velocityDiff * airAccelerationSpeed * deltaTime;
                        }

                        // Gravity
                        currentVelocity += gravity * deltaTime;

                        // Drag
                        currentVelocity *= (1f / (1f + (drag * deltaTime)));

                        _animator.SetBool(HashAnimParam.PlayerOnAir, true);
                    }

                    // Handle jumping
                    _jumpedThisFrame = false;
                    _timeSinceJumpRequested += deltaTime;
                    if (_jumpRequested)
                    {
                        // Handle double jump
                        if (allowDoubleJump)
                        {
                            if (_jumpConsumed && !_doubleJumpConsumed && (allowJumpingWhenSliding ? !motor.GroundingStatus.FoundAnyGround : !motor.GroundingStatus.IsStableOnGround))
                            {
                                motor.ForceUnground(0.1f);

                                // Add to the return velocity and reset jump state
                                currentVelocity += (motor.CharacterUp * jumpSpeed) - Vector3.Project(currentVelocity, motor.CharacterUp);
                                _jumpRequested = false;
                                _doubleJumpConsumed = true;
                                _jumpedThisFrame = true;
                            }
                        }

                        // ���������, ������������� �� ��� ��������� �������
                        if (_canWallJump || (!_jumpConsumed && ((allowJumpingWhenSliding ? motor.GroundingStatus.FoundAnyGround : motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= jumpPostGroundingGraceTime)))
                        {
                            // ����������� ����������� ������ ����� ������������
                            Vector3 jumpDirection = motor.CharacterUp;
                            if (_canWallJump)
                            {
                                jumpDirection = _wallJumpNormal;
                            }
                            else if (motor.GroundingStatus.FoundAnyGround && !motor.GroundingStatus.IsStableOnGround)
                            {
                                jumpDirection = motor.GroundingStatus.GroundNormal;
                            }

                            // ���������� ��������� ���������� ������������/���������� ����� ��� ��������� ����������. 
                            // ���� �� ���� ������ �� ����, �������� ��������� �� ������������� � ����� ��� ������� ������
                            motor.ForceUnground(0.1f);

                            // �������� � �������� �������� � �������� ��������� ������
                            currentVelocity += (jumpDirection * jumpSpeed) - Vector3.Project(currentVelocity, motor.CharacterUp);
                            _jumpRequested = false;
                            _jumpConsumed = true;
                            _jumpedThisFrame = true;

                        }
                    }

                    // Reset wall jump
                    _canWallJump = false;

                    // Take into account additive velocity
                    if (_internalVelocityAdd.sqrMagnitude > 0f)
                    {
                        currentVelocity += _internalVelocityAdd;
                        _internalVelocityAdd = Vector3.zero;
                    }

                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    float verticalInput = 0f + (_jumpInputIsHeld ? 1f : 0f);

                    // Smoothly interpolate to target velocity
                    Vector3 targetMovementVelocity = (_moveInputVector + (motor.CharacterUp * verticalInput)).normalized * NoClipMoveSpeed;
                    currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-NoClipSharpness * deltaTime));
                    break;
                }
        }
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    // ���������� �������� ������� ����� �������
                    if (_jumpRequested && _timeSinceJumpRequested > jumpPreGroundingGraceTime)
                    {
                        _jumpRequested = false;
                    }

                    // ��������� �������� �� ����� ����������
                    if (allowJumpingWhenSliding ? motor.GroundingStatus.FoundAnyGround : motor.GroundingStatus.IsStableOnGround)
                    {
                        // ���� �� ��������� �� ����������� �����, �������� �������� �������.
                        if (!_jumpedThisFrame)
                        {
                            _doubleJumpConsumed = false;
                            _jumpConsumed = false;
                        }
                        _timeSinceLastAbleToJump = 0f;
                    }
                    else
                    {
                        // ������� �� ��������, ��������� � ��� ���, ��� �� � ��������� ��� ������� (� ������� ��������� �������)
                        _timeSinceLastAbleToJump += deltaTime;
                    }

                    break;
                }
        }
       
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        if (IgnoredColliders.Contains(coll))
            return false;
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    // �� ����� ������� �� �����, ������ ���� �� �� ��������� �� ����� � �������� ������ �����������.
                    if (allowWallJump && !motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
                    {
                        _canWallJump = true;
                        _wallJumpNormal = hitNormal;
                    }

                    break;
                }
        }
    }

    public void AddVelocity(Vector3 velocity)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    _internalVelocityAdd += velocity;

                    break;
                }
        }

    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {

    }

    public void PostGroundingUpdate(float deltaTime)
    {
        // ��������� ����������� �� ����� � ������ �� ���
        if (motor.GroundingStatus.IsStableOnGround && !motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLanded();
        }
        else if (!motor.GroundingStatus.IsStableOnGround && motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLeaveStableGround();
        }
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {

    }

    protected void OnLanded()
    {
        _animator.SetTrigger(HashAnimParam.PlayerIsLanded);
    }

    protected void OnLeaveStableGround()
    {
        _animator.SetTrigger(HashAnimParam.PlayerIsLeaveStableGround);
    }
}
