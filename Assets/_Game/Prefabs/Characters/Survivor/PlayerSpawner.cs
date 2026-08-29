using Cinemachine;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject _spawnablePlayer;
    public InputHandler _inputHandler;
    public PlayerCoreUI playerCore;

    private void Awake()
    {
        InputHandler inputHandler = Instantiate(_inputHandler);
        GameObject playerGO = Instantiate(_spawnablePlayer);
        PlayerCoreUI playerCoreUI = new PlayerCoreUI();

        CharacterBrain characterBrain = _spawnablePlayer.GetComponent<CharacterBrain>();
        CharacterLocomotion characterLocomotion = _spawnablePlayer.GetComponent<CharacterLocomotion>();

        characterBrain._subject = inputHandler;
        characterLocomotion._aimTarget = playerCoreUI._aimTarget.transform;

        playerCore.GetComponentInChildren<CinemachineVirtualCamera>().Follow = characterLocomotion._cinemachineTarget;
    }
}
