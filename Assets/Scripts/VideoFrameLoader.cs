using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class VideoFrameLoader : MonoBehaviour
{
    public static VideoFrameLoader Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // �� ��ȯ���� ����
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadFrameFromVideo(string filePath, Action<Texture2D> onComplete)
    {
        StartCoroutine(LoadFrameCoroutine(filePath, onComplete));
    }

    private IEnumerator LoadFrameCoroutine(string filePath, Action<Texture2D> onComplete)
    {
        GameObject go = new GameObject("TempVideoPlayer");
        VideoPlayer videoPlayer = go.AddComponent<VideoPlayer>();

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = filePath;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.skipOnDrop = false;
        videoPlayer.playOnAwake = false;

        videoPlayer.Prepare();

        Debug.Log("check");
        bool videoErrorOccurred = false;

        videoPlayer.errorReceived += (VideoPlayer source, string message) => {
            //Debug.LogError("VideoPlayer Error: " + message);
            videoErrorOccurred = true;
        };

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
        {
            Debug.Log("preparing");
            if (videoErrorOccurred)
            {
                Debug.Log("error");
                break; // �Ǵ� ������ ���� ó��
            }
            yield return null;
        }

        Debug.Log("check1");
        long middleFrame = (long)videoPlayer.frameCount / 2;
        videoPlayer.frame = middleFrame;
        videoPlayer.Play();

        // �������� �ε�ǵ��� ��ٸ�
        while (videoPlayer.frame < middleFrame + 1 && videoPlayer.isPlaying)
            yield return null;

        // RenderTexture�� ����
        RenderTexture rt = new RenderTexture((int)videoPlayer.texture.width, (int)videoPlayer.texture.height, 0);
        Graphics.Blit(videoPlayer.texture, rt);

        // Texture2D�� �ȼ� ����
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // ���ҽ� ����
        RenderTexture.active = null;
        videoPlayer.Stop();
        Destroy(rt);
        Destroy(go);

        onComplete?.Invoke(tex);
        Debug.Log("video loaded");
    }
}
