using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonToScene : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private string nameOfScene;
    void Start()
    {
        button.onClick.AddListener(GoToScene);
    }

    void GoToScene()
    {
        SceneManager.LoadScene(nameOfScene);
    }
}
