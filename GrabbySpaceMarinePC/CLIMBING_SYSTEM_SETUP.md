# Climbing System Setup Guide

## Overview

The climbing system allows the character to:
- Detect and attach to climbable walls
- Move along wall surfaces with stamina management
- Jump off walls with enhanced jump vectors (limited jumps)
- Visual debugging for development and testing
- Stamina system that drains while climbing and regenerates when not climbing
- Independent jump limit system that resets when touching ground

## Unity Editor Setup Instructions

### Step 1: Configure Climbable Layers

1. **Create a new layer for climbable surfaces:**
   - Go to `Edit > Project Settings > Tags and Layers`
   - Add a new layer called "Climbable" (or any name you prefer)
   - Note the layer number (e.g., Layer 8)

2. **Set up your wall objects:**
   - Select wall GameObjects you want to be climbable
   - Set their layer to "Climbable"
   - Ensure they have colliders attached

### Step 2: Configure Climbing Parameters

In the `ExampleCharacterController` component, you'll find these climbing settings:

#### Wall Detection
- **Wall Detection Distance**: How far to raycast for walls (default: 1f)
- **Climbable Angle Threshold**: Minimum angle for a surface to be climbable (default: 45°)
- **Climbable Layers**: Which layers contain climbable surfaces

#### Climbing Movement
- **Climb Move Speed**: Speed when moving along walls (default: 5f)
- **Wall Attachment Offset**: Distance from wall when climbing (default: 0.6f)
- **Climbing Transition Speed**: Speed of attachment transition (default: 10f)

#### Stamina System
- **Max Stamina**: Maximum stamina points (default: 100f)
- **Stamina Drain Rate**: Stamina consumed per second while climbing (default: 20f)
- **Stamina Regen Rate**: Stamina regenerated per second when not climbing (default: 30f)
- **Min Stamina To Start Climbing**: Minimum stamina required to begin climbing (default: 10f)

#### Jump Limit System
- **Max Wall Jumps**: Maximum number of wall jumps before touching ground (default: 3)
- **Wall Jump Reset Time**: Time after touching ground to reset jump count (default: 2f)

### Step 3: Test Wall Detection

1. **Enter Play Mode**
2. **Enable Gizmos** in the Scene view (Gizmos button in Scene view toolbar)
3. **Move your character near a climbable wall**

#### Visual Debug Indicators:
- **Yellow rays** = Detection raycasts from character center and chest
- **Green sphere** = Wall hit point when climbable wall detected
- **Green ray** = Wall surface normal direction
- **Green/White wireframe quad** = Climbable wall surface indicator
- **Green cube above character** = Ready to climb (not grounded + near wall)
- **Orange cube above character** = Near wall but grounded (can't climb yet)
- **Cyan spheres** = Character detection points (center and chest)

## Testing the Climbing System

### Basic Testing
1. Create a wall with appropriate layer and angle
2. Approach the wall while in the air (not grounded)
3. Hold the climb input (default: Left Shift) - requires sufficient stamina
4. Use movement inputs to climb along the wall - watch stamina drain
5. Press jump to perform a wall jump - limited by jump count
6. Release climb input to detach from wall
7. Touch ground to reset wall jump count
8. Wait for stamina to regenerate when not climbing

### Debug Information
Watch the Scene view while testing to see:
- Detection rays extending from character
- Wall hit points and normals
- Climbing readiness indicator (green only when stamina sufficient)
- Stamina bar draining while climbing, regenerating when not
- Jump count indicators showing used vs available jumps
- State transitions between Default, Climbing, and ClimbingJump
- Target climbing position during wall attachment
- Movement and jump direction indicators

## Troubleshooting

### Wall Not Detected
1. **Check layer assignment** - Ensure wall is on climbable layer
2. **Verify angle** - Wall might be too shallow (below threshold)
3. **Check distance** - Character might be too far from wall
4. **Collider issues** - Ensure wall has proper collider

### False Detections
1. **Layer filtering** - Remove non-climbable objects from climbable layer
2. **Angle threshold** - Increase threshold to be more restrictive
3. **Ignored colliders** - Add problematic colliders to ignored list

### Debug Gizmos Not Showing
1. **Enable debug flag** - Check "Enable Climbing Debug" is true
2. **Gizmos visibility** - Ensure Gizmos are enabled in Scene view
3. **Play mode** - Some gizmos only show during play

## Implementation Status

The climbing system implementation is now substantially complete:

✅ **Wall Detection System**: Multi-point raycasting with surface validation  
✅ **Climbing State Management**: Full state machine with Default, Climbing, and ClimbingJump states  
✅ **Wall Attachment Physics**: Smooth transition to wall and climbing movement  
✅ **Stamina System**: Drains while climbing, regenerates when not climbing  
✅ **Jump Limit System**: Independent wall jump counting with ground-based reset  
✅ **Visual Debugging**: Comprehensive gizmos for all systems  

## Next Steps

Remaining development tasks:

1. **UI Integration**: Creating stamina bar and jump counter UI elements
2. **Camera Integration**: Adding camera zoom during climbing states
3. **Audio Feedback**: Adding climbing sound effects and audio cues
4. **Polish and Optimization**: Performance tuning and edge case handling
5. **Animation Integration**: Connecting climbing states to animation system
6. **Advanced Features**: Wall sliding, corner climbing, surface type variations

### Key Methods Added
- `DetectClimbableWalls()` - Main detection logic
- `IsWallClimbable(RaycastHit)` - Surface validation
- `OnDrawGizmos()` - Visual debugging

### Key Fields Added
- `_isNearClimbableWall` - Detection state
- `_wallNormal` - Surface normal for attachment
- `_wallHitPoint` - Exact contact point
- `_wallHit` - Full raycast hit data

The system is now ready for the next phase of climbing implementation!
