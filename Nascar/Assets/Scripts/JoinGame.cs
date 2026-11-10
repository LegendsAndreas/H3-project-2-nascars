using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinGame : MonoBehaviour
{
    public Button JoinGameButton;
    public TextMeshProUGUI PlayerListText;
    
    void Start()
    {
        GameManager script = GameObject.Find("GameManager").GetComponent<GameManager>();
        JoinGameButton.onClick.AddListener(script.StartGame);
    }

    public void UpdatePlayerList(GameObject[] cars)
    {
        PlayerListText.text = "Players:\n";
        foreach (GameObject player in cars)
        {
            PlayerListText.text += "- " + player.name + " - " + player.GetComponentInChildren<TextMeshPro>().text + "\n";
        }
    }
}
