using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{ 
    public enum PlayerState
    {
        None,
        Grounded,
        Aerial
    }

    public PlayerState currentPlayerState;

    public Vector3 gravity = new Vector3(0, -40, 0);
    public Vector3 activeGravity;

    private float height = 2f;
    public float buffer = 0.1f;

    // Instead of friction, to avoid increased slow down when air dodging into ground
    public float traction = 10.0f;

    public float groundedDrag = 1;
    public float aerialDrag = 1;

    public float aerialControl = 1;
    public float postDodgeControl = 0.1f;

    public Vector3 groundedPosition;
    private float lastGroundedY;

    [Serializable]
    public class PlayerInput
    {
        public float vertical;
        public float horizontal;
        public float verticalAlt;
        public float horizontalAlt;
    }

    [Serializable]
    public class PlayerEvents
    {
        public int jump;
        public bool dodge;
        public bool land;
        public bool falloff;
    }

    public PlayerInput inputs;
    public PlayerEvents events;

    public Vector3 forward;
    public Vector3 right;
    public Vector3 forwardAlt;
    public Vector3 rightAlt;

    public int numAerialJumps = 1;
    public int aerialJumpCount;

    private Vector3 isoForward;
    private Vector3 isoRight;

    private Rigidbody rb;
    private BoxCollider coll;
    private Material mat;

    public Color aerialEmissiveColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);

    public MoveAction move;
    public RunAction run;
    public GroundedJumpAction groundedJump;
    public AerialJumpAction aerialJump;
    public GroundedDodgeAction groundedDodge;
    public AerialDodgeAction aerialDodge;
    public LandAction land;
    public FallOffAction falloff;

    private PlayerActionSet jump;
    private PlayerActionSet dodge;

    public HashSet<PlayerAction> currentActions;

    [HideInInspector]
    public SpriteRenderer sprite;
    [HideInInspector]
    public Animator animator;

    void Reset()
    {
    }

    private void Awake()
    {
        Services.Player = this;

        isoForward = Vector3.zero;//Vector3.Normalize(Vector3.forward + Vector3.right);
        isoRight = Vector3.right;//Vector3.Normalize(Vector3.right + Vector3.back);

        rb = GetComponent<Rigidbody>();
        coll = GetComponent<BoxCollider>();
        mat = GetComponent<Renderer>().material;
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        height = coll.size.y;
        coll.size = new Vector3(coll.size.x, coll.size.y - buffer * 2, coll.size.z);
    }

    // Start is called before the first frame update
    void Start()
    {
        currentActions = new HashSet<PlayerAction>();

        FieldInfo[] fields = GetType().GetFields();
        foreach (FieldInfo field in fields)
        {
            if (field.FieldType.IsSubclassOf(typeof(PlayerAction)))
            {
                PlayerAction action = (PlayerAction)field.GetValue(this);
                action.Initialize(this, field.Name);
            }
        }

        jump = new PlayerActionSet(groundedJump, aerialJump);
        dodge = new PlayerActionSet(groundedDodge, aerialDodge);
    }

    // Update is called once per frame
    void Update()
    {
        GetInput();

        if (currentPlayerState == PlayerState.Grounded)
        {
            mat.SetColor("_EmissionColor", Color.black);
            aerialJumpCount = numAerialJumps;
        }
        else if (currentPlayerState == PlayerState.Aerial)
        {
            mat.SetColor("_EmissionColor", aerialEmissiveColor);
        }

        mat.color = Color.blue;

        if (currentActions.Overlaps(jump.Values))
        {
            mat.color = Color.red;
        }
        if (currentActions.Overlaps(dodge.Values))
        {
            mat.color = Color.green;
        }
        if (currentActions.Contains(land))
        {
            mat.color = Color.gray;
        }

    }

    private void FixedUpdate()
    {
        ProcessInput();

        // Check for grounding
        GroundCheck();

        // Determine last grounded y position for the camera to follow
        if (currentPlayerState == PlayerState.Grounded) lastGroundedY = transform.position.y;
        groundedPosition = new Vector3(transform.position.x, lastGroundedY, transform.position.z);

        // Get isometrically adjusted input vectors
        forward = inputs.vertical * isoForward;
        right = inputs.horizontal * isoRight;

        forwardAlt = inputs.verticalAlt * isoForward;
        rightAlt = inputs.horizontalAlt * isoRight;

        HashSet<PlayerAction> frameActions = new HashSet<PlayerAction>(currentActions);
        foreach (PlayerAction action in frameActions)
        {
            action.Process(rb);
        }

        // Apply environment forces
        //==============================
        // Apply drag, only horizontally
        Vector3 xzVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float drag = currentPlayerState == PlayerState.Grounded ? groundedDrag : aerialDrag;
        float dragMag = xzVelocity.sqrMagnitude * drag;
        Vector3 env = dragMag * -xzVelocity.normalized;

        rb.AddForce(Mathf.Min(traction * Time.fixedDeltaTime, xzVelocity.magnitude) * -xzVelocity.normalized, ForceMode.VelocityChange);

        // Apply gravity
        env += activeGravity;
        // Add the composite environmental forces
        rb.AddForce(env);

        // Max move speed = sqrt((acceleration - traction) / drag)
        // Dodge distance = -traction*0.5*(dodgeFrames/fr)^2 + dodgeV0*(dodgeFrames/fr)
    }

    void GetInput()
    {
        float control = currentPlayerState == PlayerState.Grounded ? 1.0f : aerialControl;
        control = Mathf.Pow(control, 1 / Time.deltaTime);

        //inputs.vertical = (Input.GetAxis("Vertical") * control) + (inputs.vertical * (1.0f - control));
        inputs.horizontal = (Input.GetAxis("Horizontal") * control) + (inputs.horizontal * (1.0f - control));

        if (Input.GetButtonDown("Jump"))
        {
            events.jump = 3;
        }

        //inputs.verticalAlt = (Input.GetAxis("VerticalAlt") * control) + (inputs.verticalAlt * (1.0f - control));
        //inputs.horizontalAlt = (Input.GetAxis("HorizontalAlt") * control) + (inputs.horizontalAlt * (1.0f - control));
        //if (Input.GetButtonDown("Dodge"))
        //{
        //    events.dodge = true;
        //}
    }

    void ProcessInput()
    {
        if (inputs.horizontal != 0 || inputs.vertical != 0)
        {
            //if (Input.GetButton("Dash") && currentPlayerState == PlayerState.Grounded)
            //{
            //    run.StartAction();
            //}
            //else
            {
                //run.StopAction();
                move.StartAction();
            }
        }
        else
        {
            run.StopAction();
            move.StopAction();
        }

        if (events.jump > 0)
        {
            PlayerState effectiveState = falloff.active ? PlayerState.Grounded : currentPlayerState;
            if (jump.StartActionFor(effectiveState))
            {
                events.jump = 0;
            }
        }
        events.jump = Mathf.Max(0, events.jump - 1);

        //if (events.dodge)
        //{
        //    dodge.StartActionFor(currentPlayerState);
        //    events.dodge = false;
        //}
    }

    void GroundCheck()
    {
        RaycastHit hit;
        float castDistance = (height * transform.localScale.y * 0.5f) + Physics.defaultContactOffset;
        //Debug.Log(Physics.defaultContactOffset);

        float groundHeight = float.MinValue;
        int cornersGrounded = 0;
        if (Physics.Raycast(transform.position + (Vector3.right * coll.size.x * 0.5f), Vector3.down, out hit, castDistance))
        {
            cornersGrounded++;
            groundHeight = Math.Max(hit.point.y, groundHeight);
        }
        if (Physics.Raycast(transform.position + (Vector3.left * coll.size.x * 0.5f), Vector3.down, out hit, castDistance))
        {
            cornersGrounded++;
            groundHeight = Math.Max(hit.point.y, groundHeight);
        }

        bool centerGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, castDistance);//Physics.defaultContactOffset);
        if (centerGrounded)
        {
            groundHeight = Math.Max(hit.point.y, groundHeight);
        }

        if (centerGrounded || cornersGrounded > 0)
            //if (Physics.BoxCast(transform.position, new Vector3(0.5f - Physics.defaultContactOffset, 0, 0.5f - Physics.defaultContactOffset), Vector3.down, Quaternion.identity, 0.5f + Physics.defaultContactOffset))
        {
            if (currentPlayerState == PlayerState.Aerial) events.land = true;
            currentPlayerState = PlayerState.Grounded;
            Vector3 landingPosition = transform.position;
            landingPosition.y = groundHeight + castDistance;
            transform.position = landingPosition;
            Vector3 landingVelocity = rb.velocity;
            landingVelocity.y = 0f;
            rb.velocity = landingVelocity;
            activeGravity = Vector3.zero;
            //if (hit.rigidbody is null || Math.Abs(hit.rigidbody.velocity.y) < Physics.defaultContactOffset)
            //{

            //    //activeGravity = gravity;
            //}
        }
        else
        {
            if (currentPlayerState == PlayerState.Grounded) events.falloff = true;
            currentPlayerState = PlayerState.Aerial;
            activeGravity = gravity;
        }

        if (events.land)
        {
            land.StartAction();
            events.land = false;
        }
        else if (events.falloff)
        {
            falloff.StartAction();
            events.falloff = false;
        }
    }

    public void DecrementJumpCount()
    {
        if (currentPlayerState == PlayerState.Aerial) aerialJumpCount--;
    }
}