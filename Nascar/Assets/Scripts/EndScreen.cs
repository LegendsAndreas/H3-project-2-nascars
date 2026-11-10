using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndScreen : MonoBehaviour
{
    public Button RestartButton;
    public TextMeshProUGUI ScoresText;
    
    void Start()
    {
        GameManager script = GameObject.Find("GameManager").GetComponent<GameManager>();
        RestartButton.onClick.AddListener(script.RestartGame);
    }

    public void EndGame(Car winnerCar)
    {
        ScoresText.text = $"Winner: {winnerCar.DisplayName} - Laps: {winnerCar.Laps}\n\n";
        GameObject[] cars = GameObject.FindGameObjectsWithTag("Car");
        foreach (GameObject player in cars)
        {
            if (player == winnerCar.gameObject) continue; // Skip the winner since it's already displayed
            ScoresText.text += "- " + player.name + " - " + player.GetComponent<Car>().DisplayName + " - Laps: " + player.GetComponent<Car>().Laps + "\n";
        }
        gameObject.SetActive(true);
    }
}
