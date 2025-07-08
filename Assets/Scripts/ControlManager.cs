using UnityEngine;

public class ControlManager : MonoBehaviour
{
    private static ControlManager instance;
    public static Texture2D img;
    public static DataManager.DataContainer data;
    public static param gt_cam;

    public static string current_state; // choose among new_data, run1, run2, ...
    public static string current_env = "Gongeoptap"; // Scene name
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            
        }
        else
        {
            Destroy(gameObject);
        }
    }


}
