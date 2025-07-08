using Ookii.Dialogs;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneSelectUI : MonoBehaviour
{
    public GameObject LogWindow;
    public TMPro.TextMeshProUGUI log_info;
    public RawImage data_upload_image;
    public GameObject RunButtonPrefab;

    VistaOpenFileDialog OpenDialog;
    Stream openStream = null;

    private DataManager data_manager;

    // Start is called before the first frame update
    void Start()
    {
        OpenDialog = new VistaOpenFileDialog();
        OpenDialog.Filter = "Frame Files|*.jpg;*.jpeg;*.png;*.avi;*.mov;*.mp4|All files  (*.*)|*.*";
        OpenDialog.FilterIndex = 1;
        OpenDialog.Title = "image selector";

        // Search run folders
        data_manager = new DataManager();
        var runFolders = data_manager.RunDirList();

        int index = 0;
        foreach (string folder in runFolders)
        {
            string folderName = Path.GetFileName(folder);
            GameObject newButton = Instantiate(RunButtonPrefab, RunButtonPrefab.transform.parent);
            newButton.SetActive(true);
            newButton.GetComponentInChildren<TextMeshProUGUI>().text = folderName;
            newButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => LoadData(folder));

            RectTransform button_rect = newButton.GetComponent<RectTransform>();
            button_rect.anchoredPosition = new Vector2(button_rect.anchoredPosition.x, button_rect.anchoredPosition.y - index * (button_rect.sizeDelta.y + 3));
            index++;
        }

    }

    void LoadData(string folderPath)
    {
        ControlManager.img = DataManager.Load_Image(folderPath);
        ControlManager.data = DataManager.Load_Data(folderPath);
        ControlManager.gt_cam = DataManager.Load_GT_cam(folderPath);
        ControlManager.current_state = Path.GetFileName(folderPath);

        //Draw 2D Coordinate
        float imgsize = Mathf.Min(ControlManager.data.info.width, ControlManager.data.info.height);
        Texture2D texture = Utilities.CopyTexture(ControlManager.img);
        OpenCvSharp.Mat img = OpenCvSharp.Unity.TextureToMat(texture);
        foreach (var data_entry in ControlManager.data.data)
        {
            Color color = Utilities.Palette(data_entry.id);
            OpenCvSharp.Scalar cv_color = Utilities.Color2Scalar(color);
            OpenCvSharp.Cv2.PutText(img, "ID: " + data_entry.id,
                new OpenCvSharp.Point((int)data_entry.x + 5, texture.height - 1 * (int)data_entry.y - 5),
                OpenCvSharp.HersheyFonts.HersheySimplex, imgsize / 500, cv_color, (int)(imgsize / 300));

            OpenCvSharp.Cv2.Circle(img, new OpenCvSharp.Point((int)data_entry.x, texture.height - 1 * (int)data_entry.y),
                (int)(imgsize / 100), cv_color, -1);
        }
        texture = OpenCvSharp.Unity.MatToTexture(img);
        texture.Apply();
        data_upload_image.texture = texture;
    }

    public void UploadImage()
    {
        string file_name = "";
        if (OpenDialog.ShowDialog() == DialogResult.OK)
        {
            if ((openStream = OpenDialog.OpenFile()) != null)
            {
                openStream.Close();
                file_name = OpenDialog.FileName;
            }
            else return;
        }
        else return;

        if (Utilities.IsVideo(file_name))
        {
            VideoFrameLoader.Instance.LoadFrameFromVideo(file_name, (Texture2D frame) =>
            {
                ControlManager.img = frame;
                data_upload_image.texture = ControlManager.img;
            });
        }
        else if (Utilities.IsImage(file_name))
        {
            ControlManager.img = Utilities.LoadImage(file_name);
            data_upload_image.texture = ControlManager.img;
        }
        else
        {
            StartCoroutine(logging("Upload image or video", 2, Color.yellow));
        }
        ControlManager.current_state = "new_data";
    }
    
    public void SelectEnv(string env_name)
    {
        ControlManager.current_env = env_name;
        StartCoroutine(logging("Selected Digital Twin: "+env_name, 2, Color.black));
    }

    IEnumerator logging(string log, float wait_t, Color color)
    {
        LogWindow.SetActive(true);
        log_info.text = log;
        log_info.color = color;
        yield return new WaitForSeconds(wait_t);
        LogWindow.SetActive(false);
    }

    public void Change2SelectScene() { SceneManager.LoadScene(ControlManager.current_env); }
    public void ChangeScene(string scene_name) { SceneManager.LoadScene(scene_name); }
}
