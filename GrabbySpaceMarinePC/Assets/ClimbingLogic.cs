using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using KinematicCharacterController.Examples;
using UnityEngine.Animations.Rigging;
using KinematicCharacterController;

public class ClimbingLogic : MonoBehaviour
{
    [SerializeField] private InputActionReference holdLeftAction;
    [SerializeField] private InputActionReference holdRightAction;
    [SerializeField] private Transform frontOfPlayer;
    [SerializeField] private Transform leftHandIK;
    [SerializeField] private Transform rightHandIK;
    [SerializeField] private Transform leftHandStart;
    [SerializeField] private Transform rightHandStart;
    [SerializeField] private Rig climbingRig;
    private Vector3 rayOrigin;
    public static bool isClimbing = false;
    private bool startedClimbing = false;
    private bool leftMovedLast = false;
    private float handMoveSpeed = 5f;
    private float armLength = 1f;
    private Vector3 currentLeftHandPosition;
    private Vector3 currentRightHandPosition;
    private Vector3 targetLeftHandPosition;
    private Vector3 targetRightHandPosition;
    private Vector3 cursorPosition;
    private bool isMovingLeft = false;
    private bool isMovingRight = false;
    private bool holdingLeft = false;
    private bool holdingRight = false;
    private Vector3 currentBodyMovement;
    private Rigidbody rb;

    private void Awake()
    {
        currentLeftHandPosition = leftHandIK.position;
        currentRightHandPosition = rightHandIK.position;
        rb = GetComponent<Rigidbody>();
        holdLeftAction.action.performed += ctx => HoldLeft(ctx);
        holdLeftAction.action.canceled += ctx => StopHoldLeft(ctx);
        holdRightAction.action.performed += ctx => HoldRight(ctx);
        holdRightAction.action.canceled += ctx => StopHoldRight(ctx);
    }

    public void StartClimbing()
    {
        isClimbing = true;
        currentLeftHandPosition = leftHandIK.position;
        currentRightHandPosition = rightHandIK.position;

        // Disable character movement
        ExamplePlayer.receiveInput = false;
        ExamplePlayer.instance.CharacterCamera.RotationSpeed = 0f;
        Cursor.lockState = CursorLockMode.None;
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();
        characterInputs.MoveAxisForward = 0f;
        characterInputs.MoveAxisRight = 0f;
        characterInputs.CameraRotation = ExamplePlayer.instance.CharacterCamera.Transform.rotation;
        characterInputs.JumpDown = false;
        characterInputs.CrouchDown = false;
        characterInputs.CrouchUp = false;
        ExamplePlayer.instance.Character.SetInputs(ref characterInputs);



        ExamplePlayer.instance.Character.Gravity = new Vector3(0, 0, 0);
        ExamplePlayer.instance.Character.Motor.BaseVelocity = Vector3.zero;

        climbingRig.weight = 1f;

        RaycastHit hit;
        if(Physics.Raycast(leftHandStart.position, leftHandStart.forward, out hit))
        {
            Debug.Log("Left hand hit");
            leftHandIK.position = hit.point;
        }
        if(Physics.Raycast(rightHandStart.position, rightHandStart.forward, out hit))
        {
            Debug.Log("Right hand hit");
            rightHandIK.position = hit.point;
        }
        startedClimbing = true;
    }

    public void StopClimbing()
    {
        ExamplePlayer.receiveInput = true;
        ExamplePlayer.instance.CharacterCamera.RotationSpeed = 1.3f;
        Cursor.lockState = CursorLockMode.Locked;
        isClimbing = false;

        ExamplePlayer.instance.Character.Gravity = new Vector3(0, -30, 0);
        climbingRig.weight = 0f;
    }

    private void Update()
    {
        if (isClimbing)
        {
            if(holdingLeft){
                MoveLeftHand();
            }
            if(holdingRight){
                MoveRightHand();
            }
            if(holdingLeft || holdingRight){
                MoveBody();
            }
        }
    }

    public void OnJump(InputValue value)
    {
        if (isClimbing)
        {
            StopClimbing();
        } else {
            Debug.Log("Checking for climbable walls");
            CheckForClimbableWalls();
        }
    }

    private void CheckForClimbableWalls()
    {
        RaycastHit hit;
        if(Physics.Raycast(frontOfPlayer.position, frontOfPlayer.forward, out hit, armLength))
        {
            Debug.Log(hit.transform.name);
            if(!hit.transform.CompareTag("Climbable"))
            {
                return;
            }

            StartClimbing();
        }
    }

    /*
    public void OnSelect(InputValue value)
    {
        if(MousePositionDisplay.hitPointPosition == Vector3.zero){
            return;
        }
        if(leftMovedLast){
            ConfirmNewRightHandPosition();
        }
        else{
            ConfirmNewLeftHandPosition();
        }
    }
    */

    public void HoldLeft(InputAction.CallbackContext context){
        if(MousePositionDisplay.hitPointPosition == Vector3.zero){
            return;
        }
        if(!holdingLeft) ConfirmNewLeftHandPosition();
        holdingLeft = context.ReadValue<float>() > 0f;
    }

    public void StopHoldLeft(InputAction.CallbackContext context){
        holdingLeft = false;
    }

    public void HoldRight(InputAction.CallbackContext context){
        if(MousePositionDisplay.hitPointPosition == Vector3.zero){
            return;
        }
        if(!holdingRight) ConfirmNewRightHandPosition();
        holdingRight = context.ReadValue<float>() > 0f;
    }

    public void StopHoldRight(InputAction.CallbackContext context){
        holdingRight = false;
    }

    private void ConfirmNewLeftHandPosition()
    {
        //set the targetPosition to be a point on the wall within a maximum radius from the player
        targetLeftHandPosition = MousePositionDisplay.hitPointPosition;
        leftMovedLast = true;
    }

    private void ConfirmNewRightHandPosition()
    {
        targetRightHandPosition = MousePositionDisplay.hitPointPosition;
        leftMovedLast = false;
    }

    private void MoveLeftHand()
    {
        if(Vector3.Distance(leftHandIK.position, targetLeftHandPosition) < 0.01f)
        {
            isMovingLeft = false;
            return;
        }
        isMovingLeft = true;
        leftHandIK.position = Vector3.MoveTowards(leftHandIK.position, targetLeftHandPosition, handMoveSpeed * Time.deltaTime);
    }

    private void MoveRightHand()
    {
        if(Vector3.Distance(rightHandIK.position, targetRightHandPosition) < 0.01f)
        {
            isMovingRight = false;
            return;
        }
        isMovingRight = true;
        rightHandIK.position = Vector3.MoveTowards(rightHandIK.position, targetRightHandPosition, handMoveSpeed * Time.deltaTime);
    }

    private void MoveBody()
    {
        if(isMovingLeft || isMovingRight)
        {
            return;
        }
        Vector3 leftInfluence = holdingLeft ? targetLeftHandPosition : transform.position;
        Vector3 rightInfluence = holdingRight ? targetRightHandPosition : transform.position;
        Vector3 targetBodyPosition = (leftInfluence - transform.position) / 2 + (rightInfluence - transform.position) / 2 + transform.position;
        ExamplePlayer.instance.Character.Motor.SetPosition(Vector3.MoveTowards(transform.position, targetBodyPosition, handMoveSpeed * Time.deltaTime));
         
    }
}
