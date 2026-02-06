using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ManagerofScenes : MonoBehaviour
{
    // Dropdown
    public TMP_Dropdown dropdown;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Add listener to the dropdown
        dropdown.onValueChanged.AddListener(delegate {
            DropdownValueChanged(dropdown);
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Method to handle dropdown value change
    void DropdownValueChanged(TMP_Dropdown change)
    {        // Get the selected value from the dropdown
        int selectedValue = change.value;
        string sceneName = change.options[selectedValue].text;
        LoadScene(sceneName);
    }


}
