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
        public bool jump;
    }

    [Serializable]
    public class PlayerEvents
    {
        public int jump;
        public bool dodge;
        public bool land;
        public bool falloff;
    }

    public class CollisionFlags
    {
        public bool ground;
        public bool ceiling;
        public bool left;
        public bool right;
    }

    public PlayerInput inputs;
    public PlayerEvents events;
    private CollisionFlags collision = new CollisionFlags();

    public Vector3 forward;
    public Vector3 right;
    public Vector3 forwardAlt;
    public Vector3 rightAlt;

    public int numAerialJumps = 1;
    public int aerialJumpCount;

    private Vector3 isoForward;
    private Vector3 isoRight;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private BoxCollider box;
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
        capsule = GetComponent<CapsuleCollider>();
        box = GetComponent<BoxCollider>();
        mat = GetComponent<Renderer>().material;
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        capsule.height -= (buffer + Physics.defaultContactOffset) * 2;
        capsule.radius -= buffer + Physics.defaultContactOffset;
        Vector3 boxSize = box.size;
        boxSize.y -= (buffer * 2 + Physics.defaultContactOffset * 5);
        boxSize.x -= (buffer * 2 + Physics.defaultContactOffset * 5);
        box.size = boxSize;
        //coll.radius -= buffer;
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
            //mat.SetColor("_EmissionColor", Color.black);
            aerialJumpCount = numAerialJumps;
        }
        else if (currentPlayerState == PlayerState.Aerial)
        {
            //mat.SetColor("_EmissionColor", aerialEmissiveColor);
        }

        //mat.color = Color.blue;

        //if (currentActions.Overlaps(jump.Values))
        //{
        //    mat.color = Color.red;
        //}
        //if (currentActions.Overlaps(dodge.Values))
        //{
        //    mat.color = Color.green;
        //}
        //if (currentActions.Contains(land))
        //{
        //    mat.color = Color.gray;
        //}
    }

    private void FixedUpdate()
    {
        ProcessInput();

        CollisionCheck();
        GroundUpdate();

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

        inputs.jump = Input.GetButton("Jump");

        //inputs.verticalAlt = (Input.GetAxis("VerticalAlt") * control) + (inputs.verticalAlt * (1.0f - control));
        //inputs.horizontalAlt = (Input.GetAxis("HorizontalAlt") * control) + (inputs.horizontalAlt * (1.0f - control));
        //if (Input.GetButtonDown("Dodge"))
        //{
        //    events.dodge = true;
        //}
    }

    void ProcessInput()
    {
        bool moveInput = inputs.horizontal != 0 || inputs.vertical != 0;
        bool canMove = (inputs.horizontal > 0 && !collision.right) || (inputs.horizontal < 0 && !collision.left);
        if (moveInput && canMove)
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

    void CollisionCheck()
    {
        float height = capsule.height + buffer * 2;
        float radius = capsule.radius + buffer;

        float vCastDistance = height / 2f;
        Vector3 vHalfExtents = new Vector3(capsule.radius - Physics.defaultContactOffset, Physics.defaultContactOffset, 0.5f);
        collision.ground = Physics.BoxCast(transform.position, vHalfExtents, Vector3.down, out RaycastHit groundHit, Quaternion.identity, vCastDistance) && rb.velocity.y <= 0;
        collision.ceiling = Physics.BoxCast(transform.position, vHalfExtents, Vector3.up, out RaycastHit ceilingHit, Quaternion.identity, vCastDistance) && rb.velocity.y >= 0;

        float hCastDistance = radius; 
        Vector3 hHalfExtents = new Vector3(Physics.defaultContactOffset, capsule.height / 2 - Physics.defaultContactOffset, 0.5f);
        collision.right = Physics.BoxCast(transform.position, hHalfExtents, Vector3.right, out RaycastHit rightHit, Quaternion.identity, hCastDistance) && rb.velocity.x >= 0;
        collision.left = Physics.BoxCast(transform.position, hHalfExtents, Vector3.left, out RaycastHit leftHit, Quaternion.identity, hCastDistance) && rb.velocity.x <= 0;

        //Debug.Log($"{ground}, {ceiling}, {left}, {right}");

        if (collision.ceiling)
        {
            //Debug.Log("ceiling");
            Vector3 ceilingPosition = transform.position;
            ceilingPosition.y = ceilingHit.point.y - vCastDistance - Physics.defaultContactOffset;
            transform.position = ceilingPosition;
            Vector3 fixedVelocity = rb.velocity;
            fixedVelocity.y = 0f;
            rb.velocity = fixedVelocity;
        }

        if (collision.right)
        {
            //Debug.Log("right");
            Vector3 rightPosition = transform.position;
            rightPosition.x = rightHit.point.x - hCastDistance - Physics.defaultContactOffset;
            transform.position = rightPosition;
            Vector3 fixedVelocity = rb.velocity;
            fixedVelocity.x = 0f;
            rb.velocity = fixedVelocity;
        }

        if (collision.left)
        {
            //Debug.Log("left");
            Vector3 leftPosition = transform.position;
            leftPosition.x = leftHit.point.x + hCastDistance + Physics.defaultContactOffset;
            transform.position = leftPosition;
            Vector3 fixedVelocity = rb.velocity;
            fixedVelocity.x = 0f;
            rb.velocity = fixedVelocity;
        }

        if (collision.ground)
        {
            //Debug.Log("ground");
            if (currentPlayerState == PlayerState.Aerial) events.land = true;
            currentPlayerState = PlayerState.Grounded;
            Vector3 landingPosition = transform.position;
            landingPosition.y = groundHit.point.y + vCastDistance + Physics.defaultContactOffset;
            transform.position = landingPosition;
            Vector3 landingVelocity = rb.velocity;
            landingVelocity.y = 0f;
            rb.velocity = landingVelocity;
            activeGravity = Vector3.zero;
        }
        else
        {
            if (currentPlayerState == PlayerState.Grounded) events.falloff = true;
            currentPlayerState = PlayerState.Aerial;
            activeGravity = gravity;
        }
    }

    private void GroundUpdate()
    {
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