using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinGame : MonoBehaviour
{
    public Button JoinGameButton;
    public TextMeshProUGUI _playerListText;
    
    void Start()
    {
        GameManager script = GameObject.Find("GameManager").GetComponent<GameManager>();
        JoinGameButton.onClick.AddListener(script.StartGame);
    }

    public void UpdatePlayerList(GameObject[] cars)
    {
        _playerListText.text = "Players:\n";
        foreach (GameObject player in cars)
        {
            _playerListText.text += "- " + player.name + " - " + player.GetComponentInChildren<TextMeshPro>().text + "\n";
        }
    }
}
