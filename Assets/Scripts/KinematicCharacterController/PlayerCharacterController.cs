using UnityEngine;
using KinematicCharacterController;
using System.Collections.Generic;

public class PlayerCharacterController : MonoBehaviour, ICharacterController
{
    #region Serialize fields
    [SerializeField] private KinematicCharacterMotor _motor;

    [Header("Stable Movement")]
    [SerializeField] private float _maxStableMoveSpeed = 10f;
    [SerializeField] private float _stableMovementSharpness = 15;
    [SerializeField] private float _orientationSharpness = 10;
    [SerializeField] private OrientationMethod _orientationMethod = OrientationMethod.TowardsMovement;

    [Header("Air Movement")]
    [SerializeField] private float _maxAirMoveSpeed = 10f;
    [SerializeField] private float _airAccelerationSpeed = 5f;
    [SerializeField] private float _drag = 0.1f;

    [Header("Jumping")]
    [SerializeField] private bool _allowJumpingWhenSliding = false;
    [SerializeField] private bool _allowDoubleJump = false;
    [SerializeField] private bool _allowWallJump = false;
    [SerializeField] private float _jumpSpeed = 10f;
    [SerializeField] private float _jumpPreGroundingGraceTime = 0f;
    [SerializeField] private float _jumpPostGroundingGraceTime = 0f;

    [Header("Roll")]
    [SerializeField] private float _rollingSpeed = 10f;
    [SerializeField] private float _maxRollTime = 1.5f;
    [SerializeField] private float _stoppedTime = 0;

    [Header("NoClip")]
    [SerializeField] private float _noClipMoveSpeed = 10f;
    [SerializeField] private float _NoClipSharpness = 15;

    [Header("Misc")]
    [SerializeField] private Vector3 _gravity = new Vector3(0, -30f, 0);
    [SerializeField] private bool _orientTowardsGravity = true;
    [SerializeField] private List<Collider> _ignoredColliders = new List<Collider>();
    #endregion Serialize fields

    #region Private fields
    private PlayerCharacterState CurrentCharacterState;

    private Animator _animator;

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

    #region Rolling
    private Vector3 _currentChargeVelocity;
    private bool _isStopped;
    private bool _mustStopVelocity = false;
    private float _timeSinceStartedCharge = 0;
    private float _timeSinceStopped = 0;
    #endregion
    #endregion Private fields

    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
    }

    #region Mono
    void Start()
    {
        _motor.CharacterController = this;

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
            case PlayerCharacterState.Rolling:
                {
                    _animator.SetTrigger(HashAnimParam.PlayerIsRoll);

                    _currentChargeVelocity = _motor.CharacterForward * _rollingSpeed;
                    _isStopped = false;
                    _timeSinceStartedCharge = 0f;
                    _timeSinceStopped = 0f;
                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    _motor.SetCapsuleCollisionsActivation(false);
                    _motor.SetMovementCollisionsSolvingActivation(false);
                    _motor.SetGroundSolvingActivation(false);
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
                    _motor.SetCapsuleCollisionsActivation(true);
                    _motor.SetMovementCollisionsSolvingActivation(true);
                    _motor.SetGroundSolvingActivation(true);
                    break;
                }
        }
    }
    #endregion State

    #region Methods
    /// <summary> 
    /// MyPlayer вызывает это каждый кадр, чтобы сообщить персонажу, какие у него входные данные.
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
        // Handle state transition from input
        else if (inputs.isRollDown && CurrentCharacterState != PlayerCharacterState.Rolling)
        {
            TransitionToState(PlayerCharacterState.Rolling);
        }

        _jumpInputIsHeld = inputs.isJumpHeld;

        // Clamp input
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.moveAxisRight, 0f, inputs.moveAxisForward), 1f);

        // Calculate camera direction and rotation on the character plane
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.cameraRotation * Vector3.forward, _motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.cameraRotation * Vector3.up, _motor.CharacterUp).normalized;
        }
        
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, _motor.CharacterUp);

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
            case PlayerCharacterState.Rolling:
                {
                    // Update times
                    _timeSinceStartedCharge += deltaTime;
                    if (_isStopped)
                    {
                        _timeSinceStopped += deltaTime;
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// Здесь вы сообщаете персонажу, каким должно быть его вращение в данный момент. 
    /// Это ЕДИНСТВЕННОЕ место, где вы должны задать вращение персонажа.
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    if (_lookInputVector != Vector3.zero && _orientationSharpness > 0f)
                    {
                        // Плавная интерполяция от текущего к целевому направлению взгляда
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(_motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-_orientationSharpness * deltaTime)).normalized;

                        // Установите текущее вращение (которое будет использоваться KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, _motor.CharacterUp);
                    }

                    if (_orientTowardsGravity)
                    {
                        // Поворот от текущей до инвертированной гравитации
                        currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -_gravity) * currentRotation;
                    }

                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    if (_lookInputVector != Vector3.zero && _orientationSharpness > 0f)
                    {
                        // Плавная интерполяция от текущего к целевому направлению взгляда
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(_motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-_orientationSharpness * deltaTime)).normalized;

                        // Установите текущее вращение (которое будет использоваться KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, _motor.CharacterUp);
                    }
                    if (_orientTowardsGravity)
                    {
                        // Поворот от текущей до инвертированной гравитации
                        currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -_gravity) * currentRotation;
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// Здесь устанавливаем скорость персонажу игрока, какой должна она быть на текущий момент.
    /// Это ЕДИНСТВЕННОЕ место, где мы можем установить скорость персонажа.
    /// </summary>
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case PlayerCharacterState.Default:
                {
                    Vector3 targetMovementVelocity = Vector3.zero;
                    if (_motor.GroundingStatus.IsStableOnGround)
                    {
                        // Переориентируем скорость источника на текущий уклон земли (это необходимо потому, что мы не хотим, чтобы наше сглаживание приводило к потерям скорости при изменении уклона).
                        currentVelocity = _motor.GetDirectionTangentToSurface(currentVelocity, _motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                        // Рассчитать целевую скорость
                        Vector3 inputRight = Vector3.Cross(_moveInputVector, _motor.CharacterUp);
                        Vector3 reorientedInput = Vector3.Cross(_motor.GroundingStatus.GroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                        targetMovementVelocity = reorientedInput * _maxStableMoveSpeed;

                        // Сглаживаем скорость
                        currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-_stableMovementSharpness * deltaTime));

                        // Задаем аниматору параметр
                        _animator.SetFloat(HashAnimParam.PlayerVelocity, currentVelocity.magnitude/_maxStableMoveSpeed);

                        _animator.SetBool(HashAnimParam.PlayerOnAir, false);
                    }
                    else
                    {
                        // Добавляем ввод перемещения
                        if (_moveInputVector.sqrMagnitude > 0f)
                        {
                            targetMovementVelocity = _moveInputVector * _maxAirMoveSpeed;

                            // Предотвращение подъема на неустойчивых склонах с помощью движения воздуха
                            if (_motor.GroundingStatus.FoundAnyGround)
                            {
                                Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(_motor.CharacterUp, _motor.GroundingStatus.GroundNormal), _motor.CharacterUp).normalized;
                                targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                            }

                            Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, _gravity);
                            currentVelocity += velocityDiff * _airAccelerationSpeed * deltaTime;
                        }

                        // Gravity
                        currentVelocity += _gravity * deltaTime;

                        // Drag
                        currentVelocity *= (1f / (1f + (_drag * deltaTime)));

                        _animator.SetBool(HashAnimParam.PlayerOnAir, true);
                    }

                    // Handle jumping
                    _jumpedThisFrame = false;
                    _timeSinceJumpRequested += deltaTime;
                    if (_jumpRequested)
                    {
                        // Handle double jump
                        if (_allowDoubleJump)
                        {
                            if (_jumpConsumed && !_doubleJumpConsumed && (_allowJumpingWhenSliding ? !_motor.GroundingStatus.FoundAnyGround : !_motor.GroundingStatus.IsStableOnGround))
                            {
                                _motor.ForceUnground(0.1f);

                                // Add to the return velocity and reset jump state
                                currentVelocity += (_motor.CharacterUp * _jumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);
                                _jumpRequested = false;
                                _doubleJumpConsumed = true;
                                _jumpedThisFrame = true;
                            }
                        }

                        // Посмотрим, действительно ли нам разрешено прыгать
                        if (_canWallJump || (!_jumpConsumed && ((_allowJumpingWhenSliding ? _motor.GroundingStatus.FoundAnyGround : _motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= _jumpPostGroundingGraceTime)))
                        {
                            // Рассчитайте направление прыжка перед разземлением
                            Vector3 jumpDirection = _motor.CharacterUp;
                            if (_canWallJump)
                            {
                                jumpDirection = _wallJumpNormal;
                            }
                            else if (_motor.GroundingStatus.FoundAnyGround && !_motor.GroundingStatus.IsStableOnGround)
                            {
                                jumpDirection = _motor.GroundingStatus.GroundNormal;
                            }

                            // Заставляет персонажа пропустить зондирование/ощупывание земли при следующем обновлении. 
                            // Если бы этой строки не было, персонаж оставался бы прикрепленным к земле при попытке прыжка
                            _motor.ForceUnground(0.1f);

                            // Добавить к скорости возврата и сбросить состояние прыжка
                            currentVelocity += (jumpDirection * _jumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);
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
            case PlayerCharacterState.Rolling:
                {
                    // If we have stopped and need to cancel velocity, do it here
                    if (_mustStopVelocity)
                    {
                        currentVelocity = Vector3.zero;
                        _mustStopVelocity = false;
                    }

                    if (_isStopped)
                    {
                        // When stopped, do no velocity handling except gravity
                        currentVelocity += _gravity * deltaTime;
                    }
                    else
                    {
                        // When charging, velocity is always constant
                        float previousY = currentVelocity.y;
                        currentVelocity = _currentChargeVelocity;
                        currentVelocity.y = previousY;
                        currentVelocity += _gravity * deltaTime;
                    }
                    break;
                }
            case PlayerCharacterState.NoClip:
                {
                    float verticalInput = 0f + (_jumpInputIsHeld ? 1f : 0f);

                    // Smoothly interpolate to target velocity
                    Vector3 targetMovementVelocity = (_moveInputVector + (_motor.CharacterUp * verticalInput)).normalized * _noClipMoveSpeed;
                    currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-_NoClipSharpness * deltaTime));
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
                    // Преодолеть отсрочку прыжков перед прыжком
                    if (_jumpRequested && _timeSinceJumpRequested > _jumpPreGroundingGraceTime)
                    {
                        _jumpRequested = false;
                    }

                    // Управлять прыжками во время скольжения
                    if (_allowJumpingWhenSliding ? _motor.GroundingStatus.FoundAnyGround : _motor.GroundingStatus.IsStableOnGround)
                    {
                        // Если мы находимся на поверхности земли, сбросьте значения прыжков.
                        if (!_jumpedThisFrame)
                        {
                            _doubleJumpConsumed = false;
                            _jumpConsumed = false;
                        }
                        _timeSinceLastAbleToJump = 0f;
                    }
                    else
                    {
                        // Следите за временем, прошедшим с тех пор, как мы в последний раз прыгали (в течение льготного периода)
                        _timeSinceLastAbleToJump += deltaTime;
                    }

                    break;
                }
            case PlayerCharacterState.Rolling:
                {
                    // Detect being stopped by elapsed time
                    if (!_isStopped && _timeSinceStartedCharge > _maxRollTime)
                    {
                        _mustStopVelocity = true;
                        _isStopped = true;
                    }

                    // Detect end of stopping phase and transition back to default movement state
                    if (_timeSinceStopped > _stoppedTime)
                    {
                        TransitionToState(PlayerCharacterState.Default);
                    }
                    break;
                }
        }
       
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        if (_ignoredColliders.Contains(coll))
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
                    // Мы можем прыгать от стены, только если мы не устойчивы на земле и движемся против препятствия.
                    if (_allowWallJump && !_motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
                    {
                        _canWallJump = true;
                        _wallJumpNormal = hitNormal;
                    }

                    break;
                }
            case PlayerCharacterState.Rolling:
                {
                    // Detect being stopped by obstructions
                    if (!_isStopped && !hitStabilityReport.IsStable && Vector3.Dot(-hitNormal, _currentChargeVelocity.normalized) > 0.5f)
                    {
                        _mustStopVelocity = true;
                        _isStopped = true;
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
        // Обработка приземления на землю и отрыва от нее
        if (_motor.GroundingStatus.IsStableOnGround && !_motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLanded();
        }
        else if (!_motor.GroundingStatus.IsStableOnGround && _motor.LastGroundingStatus.IsStableOnGround)
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
    #endregion Methods
}
