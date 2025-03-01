using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private GameObject _spawnPoint;
    private GameObject _player;

    private void Start()
    {
        _spawnPoint = GameObject.FindGameObjectWithTag("SpawnPoint");
        _player = GameObject.FindGameObjectWithTag("Player");
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    #region RuntimeInitalizeOnLoad
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        var obj = new GameObject("GameManager");
        obj.AddComponent<GameManager>();
    }

    #endregion
    
    #region PlayerManagement

    public void KillPlayer()
    {
        if (_player == null)
            _player = GameObject.FindGameObjectWithTag("Player");

        if (_player == null) return;
        _player.gameObject.SetActive(false);
        Invoke(nameof(RespawnPlayer), 2f);
    }
    public void RespawnPlayer()
    {
        if (_spawnPoint == null)
            _spawnPoint = GameObject.FindGameObjectWithTag("SpawnPoint");

        if (_player == null)
            _player = GameObject.FindGameObjectWithTag("Player");

        if (_player == null || _spawnPoint == null) return;
        _player.transform.position = _spawnPoint.transform.position;
        _player.gameObject.SetActive(true);
    }
    #endregion
}