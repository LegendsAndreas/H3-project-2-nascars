using UnityEngine;
using UnityEngine.UI;

public class TestScript : MonoBehaviour
{
    public SoundManager SoundManager;
    public Button TestButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TestButton.onClick.AddListener(SoundManager.PlayCarStartUpSound);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
