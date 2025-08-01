using KinematicCharacterController;
using UnityEngine;
using System.Collections;
using KinematicCharacterController.Examples;
using System.Collections.Generic;


public class AnimationStateMaster : MonoBehaviour
{
    public enum State
    {
        Idle,
        Walk,
        Jump,
        Climbing,
        Falling,
        Transition
    }

    [SerializeField] ExamplePlayer character;
    [SerializeField] Animator animator; // Animator reference
    private ExampleCharacterController charController;
    private KinematicCharacterMotor motor;
    private CharacterGroundingReport groundingReport;
    private PlayerCharacterInputs characterInputs;

    private static State currentState;
    private static State previousState;

    // Map each state to its transition trigger and main trigger
    private readonly Dictionary<State, string> transitionTriggers = new Dictionary<State, string>
    {
        { State.Idle, "TransitionToIdle" },
        { State.Walk, "TransitionToWalk" },
        { State.Jump, "TransitionToJump" },
        { State.Climbing, "TransitionToClimbing" },
        { State.Falling, "TransitionToFalling" }
    };
    private readonly Dictionary<State, string> mainTriggers = new Dictionary<State, string>
    {
        { State.Idle, "Idle" },
        { State.Walk, "Walk" },
        { State.Jump, "Jump" },
        { State.Climbing, "Climbing" },
        { State.Falling, "Falling" }
    };

    private Coroutine transitionCoroutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentState = State.Idle;
        previousState = State.Idle;
        charController = character.Character;
        motor = charController.Motor;
        groundingReport = motor.GroundingStatus;
    }

    // Update is called once per frame
    void Update()
    {
        if (currentState != previousState)
        {
            previousState = currentState;
        }

        groundingReport = motor.GroundingStatus;
        characterInputs = character.characterInputs;

        CheckState();
    }

    private void CheckState()
    {
        if (groundingReport.IsStableOnGround)
        {
            if (characterInputs.JumpDown)
            {
                TransitionToState(State.Jump);
            }
            else if (characterInputs.MoveAxisForward != 0f || characterInputs.MoveAxisRight != 0f)
            {
                TransitionToState(State.Walk);
            }
            else
            {
                TransitionToState(State.Idle);
            }
        }
        else
        {
            TransitionToState(State.Falling);
        }
    }

    public void TransitionToState(State state)
    {
        if (state != currentState)
        {
            // If a transition is already running, stop it
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(TransitionRoutine(state));
        }
    }

    private IEnumerator TransitionRoutine(State targetState)
    {
        // Reset old triggers
        foreach (var trigger in transitionTriggers.Values)
        {
            animator.ResetTrigger(trigger);
        }
        // Play transition animation if defined
        if (transitionTriggers.TryGetValue(targetState, out string transitionTrigger))
        {
            animator.SetTrigger(transitionTrigger);
            yield return null;
            animator.ResetTrigger(transitionTrigger);
        }
        // Play main animation
        if (mainTriggers.TryGetValue(targetState, out string mainTrigger))
        {
            animator.SetTrigger(mainTrigger);
        }
        previousState = currentState;
        currentState = targetState;
        transitionCoroutine = null;
    }

    public State GetCurrentState()
    {
        return currentState;
    }

    public State GetPreviousState()
    {
        return previousState;
    }
}
