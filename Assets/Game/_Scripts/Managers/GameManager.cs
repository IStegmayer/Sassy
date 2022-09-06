using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : StaticInstance<GameManager>
{
    [SerializeField] private GameObject _sassyPrefab;
    [SerializeField] private Transform _spawnPoint;
    public static event Action<GameState> OnBeforeStateChanged;
    public static event Action<GameState> OnAfterStateChanged;
    public GameState State { get; private set; }

    // Kick the game off with the first state
    void Start() => ChangeState(GameState.GameStarting);

    public void ChangeState(GameState newState)
    {
        OnBeforeStateChanged?.Invoke(newState);

        State = newState;
        switch (newState)
        {
            case GameState.GameStarting:
                HandleStarting();
                break;
            case GameState.Playing:
                HandlePlaying();
                break;
            case GameState.GameOver:
                HandleGameOver();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }

        OnAfterStateChanged?.Invoke(newState);

        //Debug.Log($"New state: {newState}");
    }
    
    private void HandleStarting()
    {
        // Do some start setup, could be environment, cinematics etc
        // Eventually call ChangeState again with your next state
        AudioSystem.Instance.PlayMusic("swanLake");
        //TODO: delete this, this should be done on sassy instantiating
        ChangeState(GameState.Playing);
    }

    private void HandleGameOver()
    {
    }

    private void HandlePlaying()
    {
    }

    public void PlayerDeath(int id)
    {
        // AudioSystem.Instance.PlaySound("killPlayer", transform.position);
    }

    private void InstanceSassy(int lvl)
    {
        GameObject sassyInstance = Instantiate(_sassyPrefab, _spawnPoint.position, Quaternion.identity);
        //NOTE: this is for testing, will have to add a start button
        if (sassyInstance) ChangeState(GameState.Playing);
    }
}


/// <summary>
/// This is obviously an example and I have no idea what kind of game you're making.
/// You can use a similar manager for controlling your menu states or dynamic-cinematics, etc
/// </summary>
[Serializable]
public enum GameState
{
    GameStarting = 0,
    Playing = 1,
    GameOver = 2,
}