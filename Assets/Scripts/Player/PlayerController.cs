using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 15f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    // State Tracking
    private bool isGrounded;
    private Rigidbody2D rb;
    private float horizontalInput;
    private bool jumpInput;

    private void Start()
    {
        // Cache the Rigidbody2D to avoid expensive GetComponent calls every frame
        rb = GetComponent<Rigidbody2D>();
    }

    // Input gathering & state polling 
    private void Update()
    {
        // 1. GROUND CHECK: Draw an invisible circle at the feet to detect the floor
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Buffer the jump command ONLY if the player is firmly on the ground
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            jumpInput = true;
        }
    }

    // Rigidbody physics calculations 
    private void FixedUpdate()
    {
        // Apply horizontal velocity while preserving the existing Y velocity
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

        // Execute the jump physics
        if (jumpInput)
        {
            
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpInput = false; // Immediately reset the flag so we don't jump continuously
        }
    }

    // This draws a red circle in the Editor so you can visually verify your GroundCheck radius!
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}