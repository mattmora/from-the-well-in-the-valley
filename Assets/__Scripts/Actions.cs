using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PlayerController;

// For grouping actions that should be executed in mutually exclusive cases
// Cases should be defined as enums, such as PlayerController.PlayerState
public class PlayerActionSet : Dictionary<Enum, PlayerAction>
{
    public PlayerActionSet(params PlayerAction[] actions)
    {
        foreach (PlayerAction action in actions)
        {
            Add(action.correspondingPlayerState, action);
        }
    }

    public bool StartActionFor(Enum id)
    {
        return this[id].StartAction();
    }
}

// An action is composed of several segments which run in sequence as coroutines
// This could be implemented with a single coroutine per action but separating into 
// segments allows us to better take advantage of inheritance
[RequireComponent(typeof(PlayerController))]
public abstract class PlayerAction
{
    public enum ActionFlow
    {
        Free = 0, // The starting action and playing action play freely
        Blocks, // The playing action blocks the starting action
        CanceledBy, // The starting action cancels the playing action
        // PausedBy? PrerequisiteTo? WaitFor?
    }

    protected Dictionary<Type, ActionFlow> currentFlow;

    [Serializable]
    public class Segment
    {
        public string name;
        public Action<Rigidbody> process;
        public int numFrames;
        public Dictionary<Type, ActionFlow> typeFlows;

        public Segment(string n, Action<Rigidbody> p, int f, ActionFlow flow)
        {
            name = n;
            process = p;
            numFrames = f;
            typeFlows = new Dictionary<Type, ActionFlow>();
            foreach (Type type in allActionTypes)
            {
                typeFlows.Add(type, flow);
            }
        }
    }

    public Segment[] segments; // All segments, including start up and end lag if they exist
    public int segmentIndex;
    public int segmentFrame;
    protected bool stopped;
    protected bool canceled;

    protected string actionName;

    protected PlayerController owner;

    public PlayerState correspondingPlayerState;

    [HideInInspector]
    public bool active;

    protected Func<bool> playCheck = () => true;

    protected Action<Rigidbody> process;

    // All types of actions derived from PlayerAction
    public static readonly HashSet<Type> allActionTypes;

    static PlayerAction()
    {
        allActionTypes = new HashSet<Type>();

        foreach (Type type in Assembly.GetAssembly(typeof(PlayerAction)).GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(PlayerAction)))
            {
                allActionTypes.Add(type);
            }
        }
    }

    public PlayerAction(int n)
    {
        // Array of segments of this action, which will run sequentially
        segments = new Segment[n];
        process = EmptyProcess;
    }

    public void Initialize(PlayerController pc, string name)
    {
        //Debug.Log(name + " Initialize");
        owner = pc;
        actionName = name;
    }

    // Should be called from Update and will set physical processes to be called in FixedUpdate
    public bool StartAction(int startingSegment = 0)
    {
        if (!playCheck()) return false;

        List<PlayerAction> actionsToCancel = new List<PlayerAction>();
        foreach (PlayerAction action in owner.currentActions)
        {
            ActionFlow actionFlow = action.currentFlow[GetType()];
            switch (actionFlow)
            {
                case ActionFlow.Free:
                    break;
                case ActionFlow.Blocks:
                    //Debug.Log(action.actionName + " BLOCKED " + actionName);
                    return false;
                case ActionFlow.CanceledBy:
                    actionsToCancel.Add(action);
                    break;
                default:
                    break;
            }
        }

        canceled = false;
        foreach (PlayerAction action in actionsToCancel)
        {
            Debug.Log(action.actionName + " CANCELED BY " + actionName);
            action.canceled = true;
            action.StopAction();
        }

        Debug.Log(actionName + " PLAYED");

        stopped = false;
        owner.currentActions.Add(this);
        segmentIndex = startingSegment;
        PreAction();
        StartCurrentSegment();
        active = true;
        return true;
    }

    public void StopAction()
    {
        if (stopped) return;
        Debug.Log(actionName + " STOPPED");
        segmentIndex = segments.Length-1;
        EndCurrentSegment();
        stopped = true;
    }

    private void EndAction()
    {
        active = false;
        process = EmptyProcess;
        PostAction();
        owner.currentActions.Remove(this);
        if (!stopped) Debug.Log(actionName + " FINISHED");
    }

    // Anything that might need to be done as the full action ends e.g. GroundedDodge resetting the owner drag
    protected virtual void PreAction() { }

    // Anything that might need to be done as the full action ends e.g. GroundedDodge resetting the owner drag
    protected virtual void PostAction() { }

    public void Process(Rigidbody rb)
    {
        if (segmentIndex < segments.Length)
        {
            while (segmentFrame >= segments[segmentIndex].numFrames)
            {
                segmentIndex++;
                if (segmentIndex < segments.Length)
                {
                    StartCurrentSegment();
                }
                else
                {
                    EndAction();
                    break;
                }
            }
            process(rb);
        }
    }

    // Might not be necessary but we'll set the process to this when
    // the action isn't running in case there's a possibility for stray calls
    protected void EmptyProcess(Rigidbody rb) { }

    protected void MinimalProcess(Rigidbody rb)
    {
        // Probably won't do anything here in the base aside from advancing the frame
        segmentFrame++;
    }
    private void StartCurrentSegment()
    {
        //Debug.Log(actionName + " " + segments[segmentIndex].name);
        segmentFrame = 0;
        process = segments[segmentIndex].process;
        currentFlow = segments[segmentIndex].typeFlows;
    }

    protected void EndCurrentSegment()
    {
        if (segmentIndex < segments.Length) segmentFrame = segments[segmentIndex].numFrames;
    }

    protected void SetActionFlowForSegments(int i, ActionFlow flow, params Type[] types)
    {
        foreach (Type type in types)
        {
            segments[i].typeFlows[type] = flow;
        }
    }

    protected void SetActionFlowForSegments(int[] segIndices, ActionFlow flow, params Type[] types)
    {
        foreach (int i in segIndices)
        {
            SetActionFlowForSegments(i, flow, types);
        }
    }
}

//===============================================================================================================
// DERIVED ACTIONS ==============================================================================================
//===============================================================================================================

[Serializable]
public class MoveAction : PlayerAction
{
    public float groundedAcceleration = 100;
    public float aerialAcceleration = 100;

    public MoveAction() : base(1)
    {
        segments[0] = new Segment("Move", MoveProcess, 1, ActionFlow.Free);

        // Blocks self
        SetActionFlowForSegments(0, ActionFlow.Blocks, typeof(MoveAction));
        // Cancelled by run
        SetActionFlowForSegments(0, ActionFlow.CanceledBy, typeof(RunAction), typeof(GroundedDodgeAction), typeof(AerialDodgeAction), typeof(LandAction), typeof(GroundedJumpAction), typeof(CrouchAction)); 
    }

    private void MoveProcess(Rigidbody rb)
    {
        if (owner.currentPlayerState == PlayerState.Grounded) owner.animator.Play("Move");
        // Apply normal acceleration based on direction input
        float acceleration = owner.currentPlayerState == PlayerState.Grounded ? groundedAcceleration : aerialAcceleration;
        Vector3 movement = acceleration * Vector3.Normalize(owner.forward + owner.face);
        rb.AddForce(movement);
        if (owner.face.x > 0) owner.sprite.flipX = false;
        else if (owner.face.x < 0) owner.sprite.flipX = true;
        //segmentFrame++;
    }

    protected override void PostAction()
    {
        if (owner.currentPlayerState == PlayerState.Grounded) owner.animator.Play("Stand");
    }
}

[Serializable]
public class CrouchAction : PlayerAction
{
    public CrouchAction() : base(2)
    {
        segments[0] = new Segment("Crouch", CrouchProcess, 20, ActionFlow.CanceledBy);
        segments[1] = new Segment("Look", LookProcess, 2, ActionFlow.CanceledBy);

        // Blocks self
        SetActionFlowForSegments(0, ActionFlow.Blocks, typeof(CrouchAction), typeof(MoveAction));
        SetActionFlowForSegments(1, ActionFlow.Blocks, typeof(CrouchAction), typeof(MoveAction));
    }

    private void CrouchProcess(Rigidbody rb)
    {
        owner.animator.Play("Land");
        if (owner.face.x > 0) owner.sprite.flipX = false;
        else if (owner.face.x < 0) owner.sprite.flipX = true;
        segmentFrame++;
    }

    private void LookProcess(Rigidbody rb)
    {
        if (segmentFrame == 0)
        {
            owner.MoveCameraFocus(Vector3.down * 4f);
            segmentFrame++;
        }
        if (owner.face.x > 0) owner.sprite.flipX = false;
        else if (owner.face.x < 0) owner.sprite.flipX = true;
    }

    protected override void PostAction()
    {
        owner.MoveCameraFocus(Vector3.zero);
        if (!canceled) owner.animator.Play("Stand");
    }
}

[Serializable]
public class RunAction : PlayerAction
{
    private readonly float initialAcceleration = 100;
    private readonly float accelerationInc = 10;
    private float acceleration;

    public RunAction() : base(3)
    {
        segments[0] = new Segment("InitialDash", InitialDashProcess, 4, ActionFlow.CanceledBy);
        segments[1] = new Segment("Run", MoveProcess, 1, ActionFlow.CanceledBy);
        segments[2] = new Segment("Stop", MinimalProcess, 4, ActionFlow.Blocks);

        SetActionFlowForSegments(0, ActionFlow.Blocks, typeof(MoveAction));
        SetActionFlowForSegments(1, ActionFlow.Blocks, typeof(RunAction), typeof(MoveAction));
        SetActionFlowForSegments(new int[] { 0, 1, 2 }, ActionFlow.Blocks, typeof(RunAction));
    }

    protected override void PreAction()
    {
        acceleration = initialAcceleration;
    }

    private void InitialDashProcess(Rigidbody rb)
    {
        Vector3 movement = acceleration * Vector3.Normalize(owner.forward + owner.face);
        rb.AddForce(movement);
        acceleration += accelerationInc;
        segmentFrame++;
    }

    private void MoveProcess(Rigidbody rb)
    {
        Vector3 movement = acceleration * Vector3.Normalize(owner.forward + owner.face);
        rb.AddForce(movement);
        //segmentFrame++;
    }
}

[Serializable]
public class GroundedJumpAction : PlayerAction
{
    public float shortJumpForce = 5;
    public float fullJumpForce = 10;
    public float floatForce = 5;
    public bool releaseToJump = false;
    private float jumpForce;
    public GroundedJumpAction() : base(3)
    {
        correspondingPlayerState = PlayerState.Grounded;
        segments[0] = new Segment("JumpSquat", JumpSquatProcess, 3, ActionFlow.Blocks);
        segments[1] = new Segment("Jump", JumpProcess, 1, ActionFlow.Blocks);
        segments[2] = new Segment("Rising", RisingProcess, 1, ActionFlow.Free);

        SetActionFlowForSegments(2, ActionFlow.CanceledBy, typeof(AerialDodgeAction), typeof(AerialJumpAction));
        SetActionFlowForSegments(2, ActionFlow.Blocks, typeof(LandAction), typeof(FallOffAction));
    }

    private void JumpSquatProcess(Rigidbody rb)
    {
        owner.animator.Play("Squat");
        if (Input.GetButton("Jump"))
        {
            jumpForce = fullJumpForce;
        }
        else
        {
            jumpForce = shortJumpForce;
            if (releaseToJump) EndCurrentSegment();
        }
        segmentFrame++;
    }

    // Called in the owner's FixedUpdate for the duration of the segment
    private void JumpProcess(Rigidbody rb)
    {
        Vector3 xzVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.velocity = xzVelocity;
        Vector3 jump = jumpForce * Vector3.up;
        rb.AddForce(jump, ForceMode.VelocityChange);
        segmentFrame++;
    }

    private void RisingProcess(Rigidbody rb)
    {
        owner.animator.Play("Jump");
        if (rb.velocity.y <= 0)
        {
            owner.animator.Play("Stand");
            EndCurrentSegment();
        }
        else if(Input.GetButton("Jump"))
        {
            rb.AddForce(floatForce * Vector3.up, ForceMode.Acceleration);
        }
    }
}

[Serializable]
public class AerialJumpAction : PlayerAction
{
    public float jumpForce = 10;

    public AerialJumpAction() : base(2)
    {
        correspondingPlayerState = PlayerState.Aerial;
        segments[0] = new Segment("Jump", JumpProcess, 1, ActionFlow.Blocks);
        segments[1] = new Segment("Rising", RisingProcess, 1, ActionFlow.Free);

        SetActionFlowForSegments(1, ActionFlow.CanceledBy, typeof(AerialDodgeAction), typeof(AerialJumpAction));
        SetActionFlowForSegments(1, ActionFlow.Blocks, typeof(LandAction), typeof(FallOffAction));

        playCheck = () => (owner.aerialJumpCount > 0);
    }

    // Called in the owner's FixedUpdate for the duration of the segment
    private void JumpProcess(Rigidbody rb)
    {
        owner.animator.Play("Squat");
        owner.DecrementJumpCount();
        Vector3 xzVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.velocity = xzVelocity;
        Vector3 jump = jumpForce * Vector3.up;
        rb.AddForce(jump, ForceMode.VelocityChange);
        segmentFrame++;
    }

    private void RisingProcess(Rigidbody rb)
    {
        owner.animator.Play("Jump");
        if (rb.velocity.y <= 0)
        {
            owner.animator.Play("Stand");
            EndCurrentSegment();
        }
    }
}

[Serializable]
public class GroundedDodgeAction : PlayerAction
{
    public float dodgeForce = 10;
    public float dodgeDrag = 0;
    private float ownerDrag;

    public GroundedDodgeAction() : base(2)
    {
        correspondingPlayerState = PlayerState.Grounded;
        segments[0] = new Segment("Dodge", DodgeProcess, 20, ActionFlow.Blocks);
        segments[1] = new Segment("EndLag", StopProcess, 8, ActionFlow.Blocks);
        SetActionFlowForSegments(0, ActionFlow.CanceledBy, typeof(FallOffAction));
    }

    private void DodgeProcess(Rigidbody rb)
    {
        if (segmentFrame == 0)
        {
            rb.AddForce(-rb.velocity, ForceMode.VelocityChange);
            //rb.velocity = new Vector3(0, rb.velocity.y, 0);
            ownerDrag = owner.groundedDrag;
            owner.groundedDrag = dodgeDrag;

            Vector3 dodge = dodgeForce * Vector3.Normalize(owner.forwardAlt.normalized + owner.rightAlt.normalized);
            rb.AddForce(dodge, ForceMode.VelocityChange);
        }
        segmentFrame++;
    }

    private void StopProcess(Rigidbody rb)
    {
        //rb.velocity = Vector3.zero;
        if (segmentFrame == 0)  rb.AddForce(-rb.velocity, ForceMode.VelocityChange);
        segmentFrame++;
    }

    protected override void PostAction()
    {
        //Debug.Log(actionName + " PostAction");
        owner.groundedDrag = ownerDrag;
    }
}


[Serializable]
public class AerialDodgeAction : PlayerAction
{
    public float dodgeForce = 20;
    public float dodgeDrag = 0;
    private float ownerDrag;
    public Vector3 gravity = Vector3.zero;
    private Vector3 ownerGravity;

    public AerialDodgeAction() : base(2)
    {
        correspondingPlayerState = PlayerState.Aerial;
        segments[0] = new Segment("Dodge", DodgeProcess, 10, ActionFlow.Blocks);
        segments[1] = new Segment("EndLag", StopProcess, 8, ActionFlow.Blocks);
        SetActionFlowForSegments(0, ActionFlow.CanceledBy, typeof(LandAction));
    }

    // Called in the owner's FixedUpdate for the duration of the segment
    private void DodgeProcess(Rigidbody rb)
    {
        if (segmentFrame == 0)
        {
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
            ownerDrag = owner.aerialDrag;
            owner.aerialDrag = dodgeDrag;
            ownerGravity = owner.gravity;
            owner.gravity = gravity;

            Vector3 dodge = dodgeForce * Vector3.Normalize(owner.forwardAlt.normalized + owner.rightAlt.normalized);
            dodge = new Vector3(dodge.x, -dodge.magnitude, dodge.z);
            rb.AddForce(dodge, ForceMode.VelocityChange);
        }
        segmentFrame++;
    }

    private void StopProcess(Rigidbody rb)
    {
        //rb.velocity = Vector3.zero;
        if (segmentFrame == 0) rb.AddForce(-rb.velocity, ForceMode.VelocityChange);
        segmentFrame++;
    }

    protected override void PostAction()
    {
        //Debug.Log(actionName + " PostAction");
        owner.aerialDrag = ownerDrag;
        owner.gravity = ownerGravity;
    }
}

[Serializable]
public class LandAction : PlayerAction
{
    public LandAction() : base(1)
    {
        segments[0] = new Segment("Land", LandProcess, 3, ActionFlow.Blocks);
    }

    private void LandProcess(Rigidbody rb)
    {
        owner.animator.Play("Land");
        segmentFrame++;
    }

    protected override void PostAction()
    {
        owner.animator.Play("Stand");
    }
}

// Not exactly an action, but allows for canceling other actions when going from grounded to aerial
[Serializable]
public class FallOffAction : PlayerAction
{
    public FallOffAction() : base(1)
    {
        // TODO: Implement variable segment length based on player state (e.g. in melee, 10 frames landing lag from helpless state after air dodge, fewer frames from normal state)
        segments[0] = new Segment("Falloff", FalloffProcess, 1, ActionFlow.Free);
    }

    private void FalloffProcess(Rigidbody rb)
    {
        owner.animator.Play("Stand");
        segmentFrame++;
    }
}