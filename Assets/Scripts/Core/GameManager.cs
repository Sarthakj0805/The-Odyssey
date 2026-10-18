using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public int playerLives = 3;
    [SerializeField] private Transform respawnPoint;
    private GameObject player;

    private void Awake()
    {
        // Implement a basic Singleton pattern to allow global access
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        player = GameObject.FindWithTag("Player");
    }

    public void PlayerDied()
    {
        playerLives--;

        if (playerLives <= 0)
        {
            SceneManager.LoadScene("GameOver");
        }
        else
        {
            RespawnPlayer();
        }
    }

    private void RespawnPlayer()
    {
        // Teleport player back and completely kill their momentum
        player.transform.position = respawnPoint.position;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}