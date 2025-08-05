using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;

namespace KinematicCharacterController.Examples
{
    public enum CharacterState
    {
        Default,
        Climbing,        // Wall climbing state
        ClimbingJump     // Wall jumping state
    }

    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
    }

    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;
        public float MoveAxisRight;
        public Quaternion CameraRotation;
        public bool JumpDown;
        public bool CrouchDown;
        public bool CrouchUp;
        public bool ClimbHold;        // Hold to climb
        public bool ClimbRelease;     // Release climbing
    }

    public struct AICharacterInputs
    {
        public Vector3 MoveVector;
        public Vector3 LookVector;
    }

    public enum BonusOrientationMethod
    {
        None,
        TowardsGravity,
        TowardsGroundSlopeAndGravity,
    }

    public class ExampleCharacterController : MonoBehaviour, ICharacterController
    {
        public KinematicCharacterMotor Motor;

        [Header("Stable Movement")]
        public float MaxStableMoveSpeed = 10f;
        public float StableMovementSharpness = 15f;
        public float OrientationSharpness = 10f;
        public OrientationMethod OrientationMethod = OrientationMethod.TowardsCamera;

        [Header("Air Movement")]
        public float MaxAirMoveSpeed = 15f;
        public float AirAccelerationSpeed = 15f;
        public float Drag = 0.1f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false;
        public float JumpUpSpeed = 10f;
        public float JumpScalableForwardSpeed = 10f;
        public float JumpPreGroundingGraceTime = 0f;
        public float JumpPostGroundingGraceTime = 0f;

        [Header("Climbing")]
        public LayerMask ClimbableLayers = -1;
        public float WallDetectionDistance = 1f;
        public float ClimbableAngleThreshold = 45f;
        public float ClimbMoveSpeed = 2f; // Reduced for jump-focused gameplay
        public float WallAttachmentOffset = 0.6f;
        public float ClimbingTransitionSpeed = 10f;

        [Header("Wall Jumping")]
        public float WallJumpForce = 20f;
        public float WallJumpUpwardForce = 12f;
        public float WallJumpGracePeriod = 0.5f; // Time after wall jump before re-attachment allowed
        public float CameraInfluenceOnJump = 0.7f; // How much camera direction affects jump (0-1)

        [Header("Climbing Stamina")]
        public float MaxStamina = 100f;
        public float StaminaDrainRate = 20f; // Stamina per second while climbing
        public float StaminaRegenRate = 30f; // Stamina per second when not climbing
        public float MinStaminaToStartClimbing = 10f;

        [Header("Climbing Jump Limits")]
        public int MaxWallJumps = 3;
        public float WallJumpResetTime = 2f; // Time after touching ground to reset jump count

        [Header("Climbing Debug")]
        [SerializeField] private bool DebugShowWallDetection = true;
        [SerializeField] private bool DebugShowDetectionRays = true;
        [SerializeField] private bool DebugShowClimbingState = true;
        [SerializeField] private Color DebugRayColor = Color.yellow;
        [SerializeField] private Color DebugWallColor = Color.green;
        [SerializeField] private Color DebugClimbingColor = Color.magenta;
        [SerializeField] private Color DebugWallJumpColor = Color.red;

        [Header("Misc")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public BonusOrientationMethod BonusOrientationMethod = BonusOrientationMethod.None;
        public float BonusOrientationSharpness = 10f;
        public Vector3 Gravity = new Vector3(0, -30f, 0);
        public Transform MeshRoot;
        public Transform CameraFollowPoint;
        public float CrouchedCapsuleHeight = 1f;

        public CharacterState CurrentCharacterState { get; private set; }

        private Collider[] _probedColliders = new Collider[8];
        private RaycastHit[] _probedHits = new RaycastHit[8];
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private bool _jumpRequested = false;
        private bool _jumpConsumed = false;
        private bool _jumpedThisFrame = false;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump = 0f;
        private Vector3 _internalVelocityAdd = Vector3.zero;
        private bool _shouldBeCrouching = false;
        private bool _isCrouching = false;

        private Vector3 lastInnerNormal = Vector3.zero;
        private Vector3 lastOuterNormal = Vector3.zero;

        // Climbing state variables
        private bool _isNearClimbableWall;
        private Vector3 _wallNormal;
        private Vector3 _wallHitPoint;
        private RaycastHit _wallHit;
        private bool _isTransitioningToClimbing;
        private Vector3 _targetClimbingPosition;
        private bool _climbInputPressed;
        private bool _climbInputReleased;

        // Stamina system variables
        private float _currentStamina;
        private bool _hasStaminaToClimb;

        // Jump limit system variables
        private int _currentWallJumps;
        private float _timeSinceLastGrounded;
        private bool _canWallJump;

        // Wall jump grace period system
        private float _timeSinceWallJump;
        private bool _isInWallJumpGracePeriod;

        // Wall detection arrays
        private RaycastHit[] _wallDetectionHits = new RaycastHit[8];

        private void Awake()
        {
            // Handle initial state
            TransitionToState(CharacterState.Default);

            // Assign the characterController to the motor
            Motor.CharacterController = this;
            
            // Initialize stamina system
            _currentStamina = MaxStamina;
            _hasStaminaToClimb = true;
            
            // Initialize jump limit system
            _currentWallJumps = 0;
            _timeSinceLastGrounded = 0f;
            _canWallJump = true;
            
            // Initialize grace period system
            _timeSinceWallJump = 0f;
            _isInWallJumpGracePeriod = false;
        }

        /// <summary>
        /// Handles movement state transitions and enter/exit callbacks
        /// </summary>
        public void TransitionToState(CharacterState newState)
        {
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        /// <summary>
        /// Event when entering a state
        /// </summary>
        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    {
                        _isTransitioningToClimbing = false;
                        break;
                    }
                case CharacterState.Climbing:
                    {
                        // Calculate target climbing position
                        _targetClimbingPosition = _wallHitPoint + _wallNormal * WallAttachmentOffset;
                        _isTransitioningToClimbing = true;
                        
                        // Force unground to prevent ground snapping
                        Motor.ForceUnground();
                        break;
                    }
                case CharacterState.ClimbingJump:
                    {
                        // Prepare for enhanced wall jump
                        Motor.ForceUnground();
                        break;
                    }
            }
        }

        /// <summary>
        /// Event when exiting a state
        /// </summary>
        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    {
                        break;
                    }
                case CharacterState.Climbing:
                    {
                        _isTransitioningToClimbing = false;
                        break;
                    }
                case CharacterState.ClimbingJump:
                    {
                        // Clean up after wall jump
                        break;
                    }
            }
        }

        /// <summary>
        /// This is called every frame by ExamplePlayer in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs(ref PlayerCharacterInputs inputs)
        {
            // Clamp input
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // Calculate camera direction and rotation on the character plane
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Move and look inputs
                        _moveInputVector = cameraPlanarRotation * moveInputVector;

                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                _lookInputVector = _moveInputVector.normalized;
                                break;
                        }

                        // Jumping input
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // Crouching input
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        // Climbing input handling
                        _climbInputPressed = inputs.ClimbHold;
                        _climbInputReleased = inputs.ClimbRelease;

                        // Check for climbing state transition (with grace period check)
                        if (_climbInputPressed && _isNearClimbableWall && !Motor.GroundingStatus.IsStableOnGround && _hasStaminaToClimb && !_isInWallJumpGracePeriod)
                        {
                            TransitionToState(CharacterState.Climbing);
                        }

                        break;
                    }
                case CharacterState.Climbing:
                    {
                        // Project movement input onto wall plane for climbing movement
                        Vector3 wallRight = Vector3.Cross(_wallNormal, Motor.CharacterUp).normalized;
                        Vector3 wallUp = Vector3.Cross(wallRight, _wallNormal).normalized;
                        
                        _moveInputVector = (wallRight * inputs.MoveAxisRight + wallUp * inputs.MoveAxisForward);
                        _lookInputVector = -_wallNormal; // Face away from wall

                        // Handle climbing release
                        if (_climbInputReleased || inputs.ClimbRelease)
                        {
                            TransitionToState(CharacterState.Default);
                        }

                        // Handle wall jumping
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                            if (_canWallJump)
                            {
                                _currentWallJumps++;
                                
                                // Start grace period to prevent immediate re-attachment
                                _isInWallJumpGracePeriod = true;
                                _timeSinceWallJump = 0f;
                                
                                TransitionToState(CharacterState.ClimbingJump);
                            }
                        }

                        break;
                    }
                case CharacterState.ClimbingJump:
                    {
                        // During climbing jump, use air movement inputs
                        _moveInputVector = cameraPlanarRotation * moveInputVector;
                        _lookInputVector = cameraPlanarDirection;
                        
                        // Handle climbing input during wall jump
                        _climbInputPressed = inputs.ClimbHold;
                        _climbInputReleased = inputs.ClimbRelease;
                        
                        // Only transition back to climbing if:
                        // 1. Climb input is held
                        // 2. Character is near a climbable wall
                        // 3. Character has stamina
                        // 4. Character is not grounded
                        // 5. Not in grace period after wall jump
                        if (_climbInputPressed && _isNearClimbableWall && _hasStaminaToClimb && !Motor.GroundingStatus.IsStableOnGround && !_isInWallJumpGracePeriod)
                        {
                            TransitionToState(CharacterState.Climbing);
                        }
                        // Otherwise transition to default if not holding climb or conditions not met
                        else if (!_climbInputPressed || !_isNearClimbableWall || !_hasStaminaToClimb || Motor.GroundingStatus.IsStableOnGround)
                        {
                            TransitionToState(CharacterState.Default);
                        }
                        
                        break;
                    }
            }
        }

        /// <summary>
        /// This is called every frame by the AI script in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs(ref AICharacterInputs inputs)
        {
            _moveInputVector = inputs.MoveVector;
            _lookInputVector = inputs.LookVector;
        }

        private Quaternion _tmpTransientRot;

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called before the character begins its movement update
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            // Detect climbable walls
            DetectClimbableWalls();
            
            // Update stamina system
            UpdateStaminaSystem(deltaTime);
            
            // Update jump limit system
            UpdateJumpLimitSystem(deltaTime);
            
            // Update grace period system
            UpdateGracePeriodSystem(deltaTime);
        }

        /// <summary>
        /// Detects climbable walls using raycast system
        /// </summary>
        private void DetectClimbableWalls()
        {
            _isNearClimbableWall = false;
            
            // Cast from character center forward
            Vector3 characterCenter = Motor.TransientPosition;
            Vector3 forwardDirection = Motor.CharacterForward;
            
            // Primary raycast from character center
            int hitCount = Motor.CharacterCollisionsRaycast(
                characterCenter,
                forwardDirection,
                WallDetectionDistance,
                out RaycastHit closestHit,
                _wallDetectionHits,
                false
            );
            
            if (hitCount > 0)
            {
                // Check if the hit surface is climbable
                if (IsWallClimbable(closestHit))
                {
                    _isNearClimbableWall = true;
                    _wallNormal = closestHit.normal;
                    _wallHitPoint = closestHit.point;
                    _wallHit = closestHit;
                }
            }
            
            // Additional detection points for better coverage
            if (!_isNearClimbableWall)
            {
                // Cast from chest level (slightly higher)
                Vector3 chestPosition = characterCenter + Motor.CharacterUp * (Motor.Capsule.height * 0.3f);
                hitCount = Motor.CharacterCollisionsRaycast(
                    chestPosition,
                    forwardDirection,
                    WallDetectionDistance,
                    out closestHit,
                    _wallDetectionHits,
                    false
                );
                
                if (hitCount > 0 && IsWallClimbable(closestHit))
                {
                    _isNearClimbableWall = true;
                    _wallNormal = closestHit.normal;
                    _wallHitPoint = closestHit.point;
                    _wallHit = closestHit;
                }
            }
        }
        
        /// <summary>
        /// Determines if a raycast hit represents a climbable wall
        /// </summary>
        private bool IsWallClimbable(RaycastHit hit)
        {
            // Check if the collider is valid for climbing
            if (!IsColliderValidForCollisions(hit.collider))
                return false;
                
            // Check if the surface is on climbable layers
            if ((ClimbableLayers.value & (1 << hit.collider.gameObject.layer)) == 0)
                return false;
            
            // Check surface angle - must be steep enough to be considered a wall
            float wallAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (wallAngle < ClimbableAngleThreshold)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Updates the stamina system - drains while climbing, regenerates when not climbing
        /// </summary>
        private void UpdateStaminaSystem(float deltaTime)
        {
            if (CurrentCharacterState == CharacterState.Climbing)
            {
                // Drain stamina while climbing
                _currentStamina -= StaminaDrainRate * deltaTime;
                _currentStamina = Mathf.Max(0f, _currentStamina);
                
                // Check if we still have stamina to climb
                _hasStaminaToClimb = _currentStamina > 0f;
                
                // Force exit climbing if stamina depleted
                if (!_hasStaminaToClimb)
                {
                    TransitionToState(CharacterState.Default);
                }
            }
            else
            {
                // Regenerate stamina when not climbing
                _currentStamina += StaminaRegenRate * deltaTime;
                _currentStamina = Mathf.Min(MaxStamina, _currentStamina);
                
                // Update climbing availability
                _hasStaminaToClimb = _currentStamina >= MinStaminaToStartClimbing;
            }
        }
        
        /// <summary>
        /// Updates the jump limit system - tracks wall jumps and resets when grounded
        /// </summary>
        private void UpdateJumpLimitSystem(float deltaTime)
        {
            // Track time since last grounded
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                _timeSinceLastGrounded = 0f;
                
                // Reset wall jump count when grounded for enough time
                if (_timeSinceLastGrounded == 0f)
                {
                    _currentWallJumps = 0;
                    _canWallJump = true;
                }
            }
            else
            {
                _timeSinceLastGrounded += deltaTime;
            }
            
            // Update wall jump availability
            _canWallJump = _currentWallJumps < MaxWallJumps;
        }
        
        /// <summary>
        /// Updates the grace period system - prevents immediate wall re-attachment after jumps
        /// </summary>
        private void UpdateGracePeriodSystem(float deltaTime)
        {
            if (_isInWallJumpGracePeriod)
            {
                _timeSinceWallJump += deltaTime;
                
                // End grace period after specified time
                if (_timeSinceWallJump >= WallJumpGracePeriod)
                {
                    _isInWallJumpGracePeriod = false;
                    _timeSinceWallJump = 0f;
                }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its rotation should be right now. 
        /// This is the ONLY place where you should set the character's rotation
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
                        {
                            // Smoothly interpolate from current to target look direction
                            Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                            // Set the current rotation (which will be used by the KinematicCharacterMotor)
                            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                        }

                        Vector3 currentUp = (currentRotation * Vector3.up);
                        if (BonusOrientationMethod == BonusOrientationMethod.TowardsGravity)
                        {
                            // Rotate from current up to invert gravity
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        else if (BonusOrientationMethod == BonusOrientationMethod.TowardsGroundSlopeAndGravity)
                        {
                            if (Motor.GroundingStatus.IsStableOnGround)
                            {
                                Vector3 initialCharacterBottomHemiCenter = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);

                                Vector3 smoothedGroundNormal = Vector3.Slerp(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                                // Move the position to create a rotation around the bottom hemi center instead of around the pivot
                                Motor.SetTransientPosition(initialCharacterBottomHemiCenter + (currentRotation * Vector3.down * Motor.Capsule.radius));
                            }
                            else
                            {
                                Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                            }
                        }
                        else
                        {
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, Vector3.up, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Ground movement
                        if (Motor.GroundingStatus.IsStableOnGround)
                        {
                            float currentVelocityMagnitude = currentVelocity.magnitude;

                            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                            // Reorient velocity on slope
                            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                            // Calculate target velocity
                            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                            Vector3 targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                            // Smooth movement Velocity
                            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
                        }
                        // Air movement
                        else
                        {
                            // Add move input
                            if (_moveInputVector.sqrMagnitude > 0f)
                            {
                                Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;

                                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                                // Limit air velocity from inputs
                                if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                                {
                                    // clamp addedVel to make total vel not exceed max vel on inputs plane
                                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, MaxAirMoveSpeed);
                                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                                }
                                else
                                {
                                    // Make sure added vel doesn't go in the direction of the already-exceeding velocity
                                    if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                                    {
                                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                                    }
                                }

                                // Prevent air-climbing sloped walls
                                if (Motor.GroundingStatus.FoundAnyGround)
                                {
                                    if (Vector3.Dot(currentVelocity + addedVelocity, addedVelocity) > 0f)
                                    {
                                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpenticularObstructionNormal);
                                    }
                                }

                                // Apply added velocity
                                currentVelocity += addedVelocity;
                            }

                            // Gravity
                            currentVelocity += Gravity * deltaTime;

                            // Drag
                            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
                        }

                        // Handle jumping
                        _jumpedThisFrame = false;
                        _timeSinceJumpRequested += deltaTime;
                        if (_jumpRequested)
                        {
                            // See if we actually are allowed to jump
                            if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                            {
                                // Calculate jump direction before ungrounding
                                Vector3 jumpDirection = Motor.CharacterUp;
                                if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                {
                                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                                }

                                // Makes the character skip ground probing/snapping on its next update. 
                                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                                Motor.ForceUnground();

                                // Add to the return velocity and reset jump state
                                currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                                currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
                                _jumpRequested = false;
                                _jumpConsumed = true;
                                _jumpedThisFrame = true;
                            }
                        }

                        // Take into account additive velocity
                        if (_internalVelocityAdd.sqrMagnitude > 0f)
                        {
                            currentVelocity += _internalVelocityAdd;
                            _internalVelocityAdd = Vector3.zero;
                        }
                        break;
                    }
                case CharacterState.Climbing:
                    {
                        // Wall attachment physics
                        if (_isTransitioningToClimbing)
                        {
                            // Smooth transition to wall attachment position
                            Vector3 currentPosition = Motor.TransientPosition;
                            Vector3 targetPosition = _targetClimbingPosition;
                            
                            // Calculate velocity needed to reach target position
                            Vector3 positionDifference = targetPosition - currentPosition;
                            currentVelocity = positionDifference * ClimbingTransitionSpeed;
                            
                            // Check if we're close enough to the wall to complete attachment
                            if (positionDifference.magnitude < 0.1f)
                            {
                                _isTransitioningToClimbing = false;
                                Motor.SetTransientPosition(targetPosition);
                                currentVelocity = Vector3.zero;
                            }
                        }
                        else
                        {
                            // Normal climbing movement along wall surface
                            Vector3 climbVelocity = _moveInputVector * ClimbMoveSpeed;
                            currentVelocity = climbVelocity;
                        }
                        
                        // No gravity while climbing
                        // No drag while climbing
                        
                        // Check if we've lost contact with the wall
                        if (!_isNearClimbableWall)
                        {
                            TransitionToState(CharacterState.Default);
                        }
                        
                        break;
                    }
                case CharacterState.ClimbingJump:
                    {
                        // Enhanced wall jump physics with camera direction influence
                        if (_jumpRequested)
                        {
                            // Get camera direction projected onto wall plane
                            Vector3 cameraForward = _lookInputVector.normalized;
                            Vector3 wallPlaneDirection = Vector3.ProjectOnPlane(cameraForward, _wallNormal).normalized;
                            
                            // Calculate horizontal jump direction (blend wall normal with camera direction)
                            Vector3 wallNormalHorizontal = _wallNormal; // Away from wall
                            Vector3 horizontalJumpDirection = Vector3.Lerp(wallNormalHorizontal, wallPlaneDirection, CameraInfluenceOnJump).normalized;
                            
                            // Combine horizontal direction with upward force
                            Vector3 finalJumpDirection = horizontalJumpDirection * WallJumpForce + Motor.CharacterUp * WallJumpUpwardForce;
                            
                            currentVelocity = finalJumpDirection;
                            
                            _jumpRequested = false;
                            _jumpConsumed = true;
                            _jumpedThisFrame = true;
                        }
                        else
                        {
                            // Apply normal air physics after wall jump
                            currentVelocity += Gravity * deltaTime;
                            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
                        }
                        
                        break;
                    }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called after the character has finished its movement update
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Handle jump-related values
                        {
                            // Handle jumping pre-ground grace period
                            if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                            {
                                _jumpRequested = false;
                            }

                            if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                            {
                                // If we're on a ground surface, reset jumping values
                                if (!_jumpedThisFrame)
                                {
                                    _jumpConsumed = false;
                                }
                                _timeSinceLastAbleToJump = 0f;
                            }
                            else
                            {
                                // Keep track of time since we were last able to jump (for grace period)
                                _timeSinceLastAbleToJump += deltaTime;
                            }
                        }

                        // Handle uncrouching
                        if (_isCrouching && !_shouldBeCrouching)
                        {
                            // Do an overlap test with the character's standing height to see if there are any obstructions
                            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                            if (Motor.CharacterOverlap(
                                Motor.TransientPosition,
                                Motor.TransientRotation,
                                _probedColliders,
                                Motor.CollidableLayers,
                                QueryTriggerInteraction.Ignore) > 0)
                            {
                                // If obstructions, just stick to crouching dimensions
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                            }
                            else
                            {
                                // If no obstructions, uncrouch
                                MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                                _isCrouching = false;
                            }
                        }
                        break;
                    }
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            // Handle landing and leaving ground
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLanded();
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLeaveStableGround();
            }
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (IgnoredColliders.Count == 0)
            {
                return true;
            }

            if (IgnoredColliders.Contains(coll))
            {
                return false;
            }

            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void AddVelocity(Vector3 velocity)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        _internalVelocityAdd += velocity;
                        break;
                    }
            }
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        protected void OnLanded()
        {
        }

        protected void OnLeaveStableGround()
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        /// <summary>
        /// Visual debugging for wall detection system
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!DebugShowWallDetection || Motor == null)
                return;

            Vector3 characterCenter = Motor.TransientPosition;
            Vector3 forwardDirection = Motor.CharacterForward;
            Vector3 chestPosition = characterCenter + Motor.CharacterUp * (Motor.Capsule.height * 0.3f);

            // Draw detection rays
            if (DebugShowDetectionRays)
            {
                Gizmos.color = DebugRayColor;
                Gizmos.DrawRay(characterCenter, forwardDirection * WallDetectionDistance);
                Gizmos.DrawRay(chestPosition, forwardDirection * WallDetectionDistance);
            }

            // Draw wall detection results
            if (_isNearClimbableWall)
            {
                // Draw wall hit point and normal
                Gizmos.color = DebugWallColor;
                Gizmos.DrawWireSphere(_wallHitPoint, 0.1f);
                Gizmos.DrawRay(_wallHitPoint, _wallNormal * 1f);
                
                // Draw wall surface indicator
                Gizmos.color = Color.Lerp(DebugWallColor, Color.white, 0.3f);
                Vector3 wallRight = Vector3.Cross(_wallNormal, Motor.CharacterUp).normalized;
                Vector3 wallUp = Vector3.Cross(wallRight, _wallNormal).normalized;
                
                // Draw a small quad to represent the climbable wall surface
                Vector3 quadCenter = _wallHitPoint + _wallNormal * 0.01f;
                Vector3[] quadCorners = new Vector3[4]
                {
                    quadCenter + wallRight * 0.5f + wallUp * 0.5f,
                    quadCenter - wallRight * 0.5f + wallUp * 0.5f,
                    quadCenter - wallRight * 0.5f - wallUp * 0.5f,
                    quadCenter + wallRight * 0.5f - wallUp * 0.5f
                };
                
                for (int i = 0; i < 4; i++)
                {
                    Gizmos.DrawLine(quadCorners[i], quadCorners[(i + 1) % 4]);
                }
                
                // Draw climbing readiness indicator (now includes stamina and grace period check)
                bool canStartClimbing = _isNearClimbableWall && !Motor.GroundingStatus.IsStableOnGround && _hasStaminaToClimb && !_isInWallJumpGracePeriod;
                Gizmos.color = canStartClimbing ? Color.green : (_isInWallJumpGracePeriod ? Color.yellow : Color.red);
                Gizmos.DrawWireCube(characterCenter + Vector3.up * 2.5f, Vector3.one * 0.2f);
                
                // Draw grace period indicator
                if (_isInWallJumpGracePeriod && DebugShowClimbingState)
                {
                    float gracePeriodPercentage = _timeSinceWallJump / WallJumpGracePeriod;
                    Gizmos.color = Color.Lerp(Color.yellow, Color.green, gracePeriodPercentage);
                    Vector3 gracePeriodBarStart = characterCenter + Vector3.up * 2.2f + Vector3.left * 0.3f;
                    Vector3 gracePeriodBarEnd = gracePeriodBarStart + Vector3.right * (gracePeriodPercentage * 0.6f);
                    Gizmos.DrawLine(gracePeriodBarStart, gracePeriodBarEnd);
                    
                    // Draw grace period bar outline
                    Gizmos.color = Color.white;
                    Vector3 gracePeriodBarOutlineEnd = gracePeriodBarStart + Vector3.right * 0.6f;
                    Gizmos.DrawLine(gracePeriodBarStart, gracePeriodBarOutlineEnd);
                }
                
                // Draw stamina indicator
                if (DebugShowClimbingState)
                {
                    float staminaPercentage = _currentStamina / MaxStamina;
                    Gizmos.color = Color.Lerp(Color.red, Color.green, staminaPercentage);
                    Vector3 staminaBarStart = characterCenter + Vector3.up * 2.8f + Vector3.left * 0.5f;
                    Vector3 staminaBarEnd = staminaBarStart + Vector3.right * (staminaPercentage * 1f);
                    Gizmos.DrawLine(staminaBarStart, staminaBarEnd);
                    
                    // Draw stamina bar outline
                    Gizmos.color = Color.white;
                    Vector3 staminaBarOutlineEnd = staminaBarStart + Vector3.right * 1f;
                    Gizmos.DrawLine(staminaBarStart, staminaBarOutlineEnd);
                    
                    // Draw jump count indicator
                    for (int i = 0; i < MaxWallJumps; i++)
                    {
                        Vector3 jumpIndicatorPos = characterCenter + Vector3.up * 3.2f + Vector3.right * (i * 0.3f - 0.3f);
                        Gizmos.color = i < _currentWallJumps ? Color.red : Color.green;
                        Gizmos.DrawWireCube(jumpIndicatorPos, Vector3.one * 0.1f);
                    }
                }
            }
            
            // Draw character detection bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(characterCenter, 0.05f);
            Gizmos.DrawWireSphere(chestPosition, 0.05f);
            
            // Draw climbing state indicators
            if (DebugShowClimbingState)
            {
                if (CurrentCharacterState == CharacterState.Climbing)
                {
                    // Draw climbing state indicator
                    Gizmos.color = DebugClimbingColor;
                    Gizmos.DrawWireCube(characterCenter + Vector3.up * 3f, Vector3.one * 0.3f);
                    
                    // Draw target climbing position
                    if (_isTransitioningToClimbing)
                    {
                        Gizmos.color = DebugRayColor;
                        Gizmos.DrawWireSphere(_targetClimbingPosition, 0.15f);
                        Gizmos.DrawLine(characterCenter, _targetClimbingPosition);
                    }
                    
                    // Draw climbing movement direction
                    if (_moveInputVector.sqrMagnitude > 0f)
                    {
                        Gizmos.color = Color.cyan;
                        Vector3 climbMovement = _moveInputVector * 0.5f;
                        Gizmos.DrawRay(characterCenter, climbMovement);
                    }
                }
                else if (CurrentCharacterState == CharacterState.ClimbingJump)
                {
                    // Draw wall jump state indicator
                    Gizmos.color = DebugWallJumpColor;
                    Gizmos.DrawWireCube(characterCenter + Vector3.up * 3f, Vector3.one * 0.3f);
                    
                    // Draw wall jump direction
                    Vector3 wallJumpDirection = (_wallNormal * 15f + Motor.CharacterUp * 10f).normalized;
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(characterCenter, wallJumpDirection * 2f);
                }
            }
            
            // Draw state information text (visible in Scene view)
            if (DebugShowClimbingState)
            {
                Vector3 textPosition = characterCenter + Vector3.up * 3.5f;
                string stateText = $"State: {CurrentCharacterState}";
                if (CurrentCharacterState == CharacterState.Climbing)
                {
                    stateText += $"\nTransitioning: {_isTransitioningToClimbing}";
                    stateText += $"\nNear Wall: {_isNearClimbableWall}";
                }
                stateText += $"\nStamina: {_currentStamina:F1}/{MaxStamina}";
                stateText += $"\nWall Jumps: {_currentWallJumps}/{MaxWallJumps}";
                stateText += $"\nCan Climb: {_hasStaminaToClimb}";
                stateText += $"\nCan Wall Jump: {_canWallJump}";
                stateText += $"\nGrace Period: {(_isInWallJumpGracePeriod ? $"{_timeSinceWallJump:F1}s/{WallJumpGracePeriod:F1}s" : "None")}";
                
                // Note: Unity doesn't have built-in text rendering in Gizmos
                // This is a placeholder for where you could add a custom text rendering solution
                // For now, we rely on the visual indicators above
            }
        }
    }
}