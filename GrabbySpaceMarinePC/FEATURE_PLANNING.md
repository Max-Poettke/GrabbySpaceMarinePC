# Feature Planning & Discussion

## Project Overview
**Project:** Grabby Space Marine PC  
**Date Created:** 2025-08-05  
**Current Focus:** Character controller and animation system

---

## Current System Analysis

### Existing Components
- **AnimationStateMaster.cs** - Handles animation state transitions
- **KinematicCharacterController** - Character movement system
- **ExamplePlayer.cs** - Player input handling
- **ExampleCharacterController.cs** - Character controller logic

### Current State
- Animation state management system in place
- Kinematic character controller integrated
- Basic player movement functionality

---

## Proposed Features

### 🎯 High Priority Features

- [ ] **Custom Climbing System**
  - **Description:** Wall-sticking mechanic with stamina-based wall jumping
  - **Core Features:**
    - Wall attachment with specific input
    - Limited wall jumps (independent jump counter)
    - Stamina depletion only while hanging on walls
    - Enhanced jump distance while climbing
  - **Implementation Notes:** Integrate with existing KinematicCharacterController
  - **Dependencies:** Wall detection system, UI stamina bar, jump counter system
  - **Estimated Effort:** Large

### 🔧 Medium Priority Features
*Add your medium-priority features here*

- [ ] **Feature Name**
  - **Description:** 
  - **Implementation Notes:** 
  - **Dependencies:** 
  - **Estimated Effort:** 

### 💡 Future Considerations
*Ideas for later implementation*

- [ ] **Advanced Climbing Mechanics**
  - **Description:** Corner climbing, ceiling traversal, ledge grabbing
  - **Implementation Notes:** Extend wall detection to handle complex geometry
  - **Dependencies:** Core climbing system, enhanced collision detection
  - **Estimated Effort:** Large

- [ ] **Climbing Surface Types**
  - **Description:** Different surfaces with varying grip levels (ice, rock, metal)
  - **Implementation Notes:** Material-based stamina drain rates and movement speeds
  - **Dependencies:** Material detection system, configurable surface properties
  - **Estimated Effort:** Medium

- [ ] **Multiplayer Climbing**
  - **Description:** Network synchronization for climbing states
  - **Implementation Notes:** State replication, lag compensation for wall detection
  - **Dependencies:** Networking framework, state synchronization system
  - **Estimated Effort:** Large 

---

## Climbing System Implementation Plan

### Phase 1: Core Detection & State Management

#### 1.1 Wall Detection System
**Integration Point:** Add to `ExampleCharacterController.cs` in `BeforeCharacterUpdate()`
- **Raycast Implementation:**
  - Use `Motor.CharacterCollisionsRaycast()` method (existing in KinematicCharacterMotor)
  - Cast from character center in forward direction
  - Additional casts from chest/hand positions for better detection
  - Distance threshold: ~1.5f units from character
- **Surface Validation:**
  - Check surface normal angle (climbable if angle > 60° from horizontal)
  - Layer filtering using `CollidableLayers` (follow existing pattern)
  - Store hit normal and point for attachment positioning
- **Code Structure:**
```csharp
private bool _isNearClimbableWall = false;
private Vector3 _wallNormal = Vector3.zero;
private Vector3 _wallHitPoint = Vector3.zero;

private void DetectClimbableWalls()
{
    // Use Motor.CharacterCollisionsRaycast for detection
    // Store results in _wallNormal and _wallHitPoint
}
```

#### 1.2 Climbing State Integration
**Integration Point:** Extend existing `CharacterState` enum and state system
- **State Enum Extension:**
```csharp
public enum CharacterState
{
    Default,
    Climbing,        // New state
    ClimbingJump     // New state for wall jumping
}
```
- **State Management in UpdateVelocity():**
  - Add new case for `CharacterState.Climbing`
  - Override gravity: `currentVelocity += Vector3.zero; // No gravity`
  - Handle wall attachment physics
  - Implement wall movement velocity calculations
- **Transition Logic:**
  - Enter climbing: Near wall + climb input + not grounded
  - Exit climbing: Ground contact OR stamina depleted OR manual release

### Phase 2: Core Climbing Mechanics

#### 2.1 Wall Attachment System
**Integration Point:** Extend `PlayerCharacterInputs` and `SetInputs()` method
- **Input Structure Extension:**
```csharp
public struct PlayerCharacterInputs
{
    // Existing fields...
    public bool ClimbHold;        // New: Hold to climb
    public bool ClimbRelease;     // New: Release climbing
}
```
- **Input Handling in ExamplePlayer.cs:**
```csharp
characterInputs.ClimbHold = Input.GetKey(KeyCode.LeftShift);
characterInputs.ClimbRelease = Input.GetKeyUp(KeyCode.LeftShift);
```
- **Attachment Logic in UpdateVelocity():**
  - Position snapping: `Motor.SetPosition(_wallHitPoint + _wallNormal * 0.6f)`
  - Velocity zeroing when attaching
  - Smooth transition using existing `Vector3.Lerp` pattern

#### 2.2 Wall Movement & Jumping
**Integration Point:** New case in `UpdateVelocity()` for `CharacterState.Climbing`
- **Wall Movement Physics:**
```csharp
case CharacterState.Climbing:
{
    // Project movement input onto wall plane
    Vector3 wallRight = Vector3.Cross(_wallNormal, Motor.CharacterUp);
    Vector3 wallUp = Vector3.Cross(wallRight, _wallNormal);
    
    Vector3 climbVelocity = (wallRight * _moveInputVector.x + wallUp * _moveInputVector.z) * ClimbMoveSpeed;
    currentVelocity = climbVelocity;
    
    // No gravity while climbing
    break;
}
```
- **Wall Jumping Implementation:**
  - Detect jump input while climbing
  - Calculate enhanced jump vector: `_wallNormal * WallJumpForce + Motor.CharacterUp * WallJumpUpForce`
  - Transition to `CharacterState.ClimbingJump` temporarily
  - Use existing jump logic pattern but with modified vectors

#### 2.3 Stamina & Jump Systems
**Integration Point:** Add new fields to `ExampleCharacterController` class
- **Stamina System Implementation:**
```csharp
[Header("Climbing System")]
public float MaxClimbStamina = 100f;
public float StaminaDepletionRate = 20f;  // Per second while hanging
public float StaminaRegenRate = 30f;      // Per second when not climbing
public float ClimbMoveSpeed = 5f;

private float _currentStamina;
private bool _isClimbing = false;
```
- **Stamina Logic in UpdateVelocity():**
```csharp
// In climbing state, deplete stamina
if (CurrentCharacterState == CharacterState.Climbing)
{
    _currentStamina -= StaminaDepletionRate * deltaTime;
    if (_currentStamina <= 0f)
    {
        // Force detachment
        TransitionToState(CharacterState.Default);
    }
}
else
{
    // Regenerate stamina
    _currentStamina = Mathf.Min(_currentStamina + StaminaRegenRate * deltaTime, MaxClimbStamina);
}
```
- **Jump Limit System:**
```csharp
public int MaxWallJumps = 3;
public float WallJumpForce = 15f;
public float WallJumpUpForce = 10f;
private int _currentWallJumps;

// Reset in OnLanded() method
protected void OnLanded()
{
    _currentWallJumps = MaxWallJumps;
}

// Jump consumption logic
private void ConsumeWallJump()
{
    if (_currentWallJumps > 0)
    {
        _currentWallJumps--;
        // Trigger jump with enhanced force
        Vector3 jumpVector = _wallNormal * WallJumpForce + Motor.CharacterUp * WallJumpUpForce;
        Motor.ForceUnground();
        currentVelocity = jumpVector;
        TransitionToState(CharacterState.Default);
    }
}
```

### Phase 3: UI & Camera Integration

#### 3.1 Camera Zoom System
**Integration Point:** Modify `ExampleCharacterCamera.cs` and `ExamplePlayer.cs`
- **Camera Zoom Implementation:**
```csharp
// In ExampleCharacterCamera.cs - add new fields
public float ClimbingDistance = 3f;  // Closer zoom while climbing
private float _originalDistance;
private bool _isClimbingZoom = false;

// New method to control climbing zoom
public void SetClimbingZoom(bool isClimbing)
{
    if (isClimbing && !_isClimbingZoom)
    {
        _originalDistance = TargetDistance;
        TargetDistance = ClimbingDistance;
        _isClimbingZoom = true;
    }
    else if (!isClimbing && _isClimbingZoom)
    {
        TargetDistance = _originalDistance;
        _isClimbingZoom = false;
    }
}
```
- **Integration in Character Controller:**
```csharp
// In state transitions
public void OnStateEnter(CharacterState state, CharacterState fromState)
{
    switch (state)
    {
        case CharacterState.Climbing:
            // Zoom camera in
            ExamplePlayer.instance.CharacterCamera.SetClimbingZoom(true);
            break;
        case CharacterState.Default:
            if (fromState == CharacterState.Climbing)
            {
                // Zoom camera out
                ExamplePlayer.instance.CharacterCamera.SetClimbingZoom(false);
            }
            break;
    }
}
```

#### 3.2 UI System
**Implementation:** Create new UI Canvas with stamina/jump displays
- **Stamina Bar Implementation:**
```csharp
// In UI Manager script
public Slider staminaBar;
public Image staminaFill;

public void UpdateStaminaUI(float current, float max)
{
    if (staminaBar != null)
    {
        staminaBar.value = current / max;
        
        // Color transitions
        Color staminaColor = Color.Lerp(Color.red, Color.green, staminaBar.value);
        staminaFill.color = staminaColor;
    }
}
```
- **Jump Counter Display:**
```csharp
public Image[] jumpIcons;  // Array of jump indicator images

public void UpdateJumpUI(int currentJumps, int maxJumps)
{
    for (int i = 0; i < jumpIcons.Length; i++)
    {
        jumpIcons[i].enabled = i < currentJumps;
        jumpIcons[i].color = i < currentJumps ? Color.white : Color.gray;
    }
}
```
- **Performance Optimization:** Update UI only when values change, not every frame

#### 3.3 Audio Feedback
**Integration Point:** Add AudioSource component and sound clips
- **Audio triggers in state transitions and climbing actions**
- **Use existing Unity Audio system patterns**

## Implementation Strategy

### Development Order
1. **Week 1:** Wall detection system and basic state management
2. **Week 2:** Wall attachment and basic climbing movement
3. **Week 3:** Stamina system implementation
4. **Week 4:** Jump limit system and wall jumping mechanics
5. **Week 5:** UI system and camera zoom integration
6. **Week 6:** Audio feedback and edge case handling
7. **Week 7:** Performance optimization and testing
8. **Week 8:** Polish and final integration testing

### Testing Milestones
- **Week 2:** Basic wall attachment working
- **Week 4:** Full climbing mechanics functional
- **Week 6:** Complete system with UI/audio
- **Week 8:** Production-ready implementation

---

## Technical Considerations & Design Challenges

### Architecture Decisions
- **State Machine Extension:** Leverage existing AnimationStateMaster pattern
- **Physics Integration:** Work with KinematicCharacterController's velocity-based system
- **Modular Design:** Create separate ClimbingController component for maintainability

### Major Design Challenges

#### 1. **Physics Integration Complexity**
- **Challenge:** KinematicCharacterController expects ground-based movement
- **Solution:** Override velocity calculations during climbing states
- **Risk:** Potential conflicts with existing movement code
- **Mitigation:** Create climbing-specific physics layer with fallback mechanisms

#### 2. **Wall Detection Accuracy**
- **Challenge:** Reliable detection across different wall angles and surfaces
- **Issues:**
  - False positives on non-climbable surfaces
  - Edge cases at corners and overhangs
  - Performance impact of continuous raycasting
- **Solution:** Multi-point detection with surface normal validation
- **Optimization:** Spatial partitioning for wall detection queries

#### 3. **Smooth State Transitions**
- **Challenge:** Seamless transitions between ground, air, and wall states
- **Issues:**
  - Position/rotation interpolation
  - Velocity preservation during transitions
  - Physics state switching
- **Solution:** Transition state buffers and interpolation systems

#### 4. **Stamina & Jump Balance**
- **Challenge:** Balancing two independent systems for engaging gameplay
- **Stamina Considerations:**
  - Drain rate while hanging vs. time pressure
  - Recovery time vs. pacing
  - Encourages quick decision-making
- **Jump Limit Considerations:**
  - Jump count vs. level design requirements
  - Reset conditions (ground contact)
  - Mobility vs. strategic planning
- **Solution:** Configurable parameters with playtesting feedback loops

#### 5. **Performance Optimization**
- **Challenge:** Continuous wall detection and physics calculations
- **Issues:**
  - Raycast frequency and complexity
  - UI updates for stamina bar
  - Physics state calculations
- **Solutions:**
  - LOD system for detection accuracy
  - Object pooling for UI elements
  - Cached surface data

#### 6. **Edge Case Handling**
- **Challenge:** Robust behavior in complex geometry
- **Edge Cases:**
  - Moving platforms while climbing
  - Destructible walls
  - Overlapping climbable surfaces
  - Network synchronization (if multiplayer)
- **Solution:** Comprehensive testing framework and fallback behaviors

### Dependencies & Requirements
- **Unity Physics:** Raycast and collision systems
- **UI Toolkit/UGUI:** For stamina bar implementation
- **Input System:** For climbing input detection
- **Audio System:** For climbing sound effects
- **Performance Profiler:** For optimization validation

### Risk Mitigation Strategies
1. **Prototype Early:** Build basic wall detection first
2. **Incremental Integration:** Add features one at a time
3. **Extensive Testing:** Create test levels with edge cases
4. **Performance Monitoring:** Profile each implementation phase
5. **Fallback Systems:** Ensure graceful degradation when systems fail

---

## Discussion Notes

### Meeting Notes
*Add discussion points and decisions here*

**Date:** 2025-08-05
- Created initial planning document
- Ready to discuss next features

### Action Items
- [ ] Define specific features to implement
- [ ] Prioritize feature list
- [ ] Create detailed implementation plans
- [ ] Set development timeline

---

## Resources & References

### Documentation Links
- [Unity Animation Documentation](https://docs.unity3d.com/Manual/AnimationOverview.html)
- [Kinematic Character Controller Documentation](link-to-docs)

### Code References
- `AnimationStateMaster.cs` - Line 121 (current focus)
- Character controller examples in project

---

*Last Updated: 2025-08-05*
