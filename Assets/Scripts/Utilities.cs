using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

public struct param
{
    public Vector3 pose;
    public Vector3 euler_rot;
    public Quaternion quat_rot;
    public float fov;
    public float k1;
    public float k2;
    public float p1;
    public float p2;
    public float k3;
}
public enum fitness_method
{
    TemplateMatching,
    SIFT,
    SURF,
    ORB,
    Coord
}

public enum HyperScheduleMethod
{
    Const,
    Linear,
    Cyclic,
    Exponential
}

public static class Utilities
{
    public static Color Palette(int idx)
    {
        Color[] colors = {
            new Color(0.90f, 0.10f, 0.30f), // Red
            new Color(0.24f, 0.70f, 0.30f), // Blue
            new Color(1.00f, 0.88f, 0.10f), // Yellow
            new Color(0.00f, 0.51f, 0.78f), // Green
            new Color(0.96f, 0.51f, 0.19f), // Orange
            new Color(0.57f, 0.12f, 0.71f), // Purple
            new Color(0.27f, 0.94f, 0.94f), // Cyan
            new Color(0.94f, 0.20f, 0.90f), // Magenta
            new Color(0.82f, 0.96f, 0.24f), // Lime
            new Color(0.00f, 0.50f, 0.50f)  // Teal
        };

        // 인덱스를 0~9 범위로 제한하여 순환하도록 함
        idx = Mathf.Abs(idx) % colors.Length;
        return colors[idx];
    }

    public static Scalar Color2Scalar(Color color)
    {
        // OpenCV에서는 BGR 순서이므로 변환
        return new Scalar(
            (int)(color.b * 255), // Blue
            (int)(color.g * 255), // Green
            (int)(color.r * 255), // Red
            (int)(color.a * 255)  // Alpha (Optional)
        );
    }

    public static Vector3 NormalizeScale(Vector3 original, float div)
    {
        return original / div;
    }
    public static Texture2D CopyTexture(Texture2D original)
    {
        // 새로운 Texture2D 생성 (RGBA32 형식으로 변환하여 독립적 복사)
        Texture2D copy = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false);

        // 원본의 픽셀 데이터를 가져와서 복사
        copy.SetPixels(original.GetPixels());
        copy.Apply();

        return copy;
    }


    public static Texture2D LoadImage(string filePath)
    {
        // 파일을 바이트 배열로 읽기
        byte[] fileData = File.ReadAllBytes(filePath);

        // Texture2D 객체 생성
        Texture2D texture = new Texture2D(2, 2); // 기본 크기로 생성 후 로드될 이미지에 맞게 크기 조정됨
        texture.LoadImage(fileData); // 바이트 데이터를 통해 이미지 로드
        return texture;
    }

    //public static IEnumerator Texture2D LoadFrameFromVideo(string filePath)
    //{
    //    var tmp_obj = new GameObject("tmp_VideoPlayer");
    //    VideoPlayer videoPlayer = tmp_obj.AddComponent<VideoPlayer>();
    //    videoPlayer.source = VideoSource.Url;
    //    videoPlayer.url = "file://"+filePath;
    //    videoPlayer.renderMode = VideoRenderMode.RenderTexture;

    //    videoPlayer.playOnAwake = false;
    //    videoPlayer.isLooping = false;
    //    videoPlayer.skipOnDrop = false;

    //    videoPlayer.Prepare();
    //    while (!videoPlayer.isPrepared)
    //    {
            
    //    }

    //    int width = (int)videoPlayer.width;
    //    int height = (int)videoPlayer.height;

    //    RenderTexture rt = new RenderTexture(width, height, 0);
    //    videoPlayer.targetTexture = rt;

    //    videoPlayer.Play();
    //    videoPlayer.Pause(); // 정지해서 첫 프레임만 확보
    //    //yield return new WaitForEndOfFrame();

    //    RenderTexture.active = rt;
    //    Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
    //    tex.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
    //    tex.Apply();

    //    RenderTexture.active = null;

    //    videoPlayer.targetTexture = null;
    //    UnityEngine.Object.Destroy(rt);
    //    UnityEngine.Object.Destroy(rt);
    //    return tex;
    //    //using (var capture = new VideoClip(filePath))
    //    //{
    //    //    if (!capture.IsOpened())
    //    //    {
    //    //        Debug.LogError("비디오를 열 수 없습니다: " + filePath);
    //    //        return null;
    //    //    }

    //    //    Mat frame = new Mat();
    //    //    capture.Read(frame); // 첫 번째 프레임 읽기

    //    //    if (frame.Empty())
    //    //    {
    //    //        Debug.LogError("프레임이 비어 있습니다.");
    //    //        return null;
    //    //    }

    //    //    return OpenCvSharp.Unity.MatToTexture(frame);
    //    //}
    //}

    public static bool IsImage(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp" };
        return Array.Exists(imageExtensions, ext => ext == extension);
    }

    public static bool IsVideo(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        string[] videoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".mpeg" };
        return Array.Exists(videoExtensions, ext => ext == extension);
    }

    public static void SavePNG(Texture2D texture, string filePath)
    {
        Texture2D newTexture;
        // Texture2D를 png로 저장
        if (texture.format == TextureFormat.RGBA32 || texture.format == TextureFormat.RGB24 || texture.format == TextureFormat.ARGB32)
        {
            newTexture = texture;
        }
        else
        {
            newTexture = Utilities.ConvertToRGBA32(texture);

        }

        // 텍스처를 PNG로 인코딩
        byte[] pngData = newTexture.EncodeToPNG();
        if (pngData != null)
        {
            // 파일로 저장
            File.WriteAllBytes(filePath, pngData);
            Debug.Log("PNG saved successfully at: " + filePath);
        }
        else
        {
            Debug.LogError("Failed to encode texture to PNG.");
        }
    }

    public static Texture2D ConvertToRGBA32(Texture2D texture)
    {
        // 새로운 RGBA32 텍스처 생성
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        newTexture.SetPixels(texture.GetPixels());
        newTexture.Apply();
        return newTexture;
    }

    public static Texture2D RenderToTexture2D(this RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

    public static Mat RenderToMat(this RenderTexture rTex)
    {
        var tex = RenderToTexture2D(rTex);
        Mat img = OpenCvSharp.Unity.TextureToMat(tex);
        return img;
    }

    public static Mat RenderToEdge(this RenderTexture rTex)
    {
        var tex = RenderToTexture2D(rTex);
        Mat img = OpenCvSharp.Unity.TextureToMat(tex);
        img = img.Canny(0, 300);
        return img;
    }

    public static Mat Undistort(Mat image, Vector2 principalPoint, float fov,
        float k1, float k2, float p1, float p2, float k3, float fx = 0, float fy = 0)   // fov in degree, vertical
    {
        if (fov == 0) return image;   // 왜곡 없음
        if (fx == 0 || fy == 0)
        {
            float focalLength = principalPoint.y / Mathf.Atan(Mathf.Deg2Rad * fov / 2);
            fx = focalLength; fy = focalLength;
        }
        // 카메라 내부 파라미터 (예시값)
        Mat cameraMatrix = new Mat(3, 3, MatType.CV_64FC1, new double[]
        {
            fx, 0, principalPoint.x,
            0, fy, principalPoint.y,
            0, 0, 1
        });

        // 왜곡 계수 (k1, k2, p1, p2, k3)
        Mat distCoeffs = new Mat(1, 5, MatType.CV_64FC1, new double[]
        {
            k1, k2, p1, p2, k3
        });

        // 왜곡 보정 이미지 생성
        Mat undistorted = new Mat();
        Cv2.Undistort(image, undistorted, cameraMatrix, distCoeffs);

        return undistorted;
    }

    public static Texture2D UndistortTexture(Texture2D image, Vector2 principalPoint, float fov,
        float k1, float k2, float p1, float p2, float k3, float fx = 0, float fy = 0)   // fov in radian, vertical
    {
        Mat image_ = OpenCvSharp.Unity.TextureToMat(image);
        Mat undistorted = Undistort(image_, principalPoint, fov, k1, k2, p1, p2, k3, fx, fy);
        return OpenCvSharp.Unity.MatToTexture(undistorted);
    }

    public static Vector2 UndistortedPoint(Vector2 original, param param_, Vector2 principalPoint,
         float fx = 0, float fy = 0, float w = 0, float h = 0)
    {
        if (param_.fov == 0) return original;
        float k1= param_.k1;
        float k2 = param_.k2;
        float p1 = param_.p1;
        float p2 = param_.p2;
        float k3 = param_.k3;

        //Debug.Log(", fx: "+ fx.ToString() + ", fy: " + fy.ToString() + ", w: " + w.ToString() + ", h: " + h.ToString());
        if (fx == 0 || fy == 0 || w == 0 || h == 0)
        {
            float focalLength = principalPoint.y / Mathf.Atan(Mathf.Deg2Rad * param_.fov / 2);
            fx = focalLength; fy = focalLength;
            w = principalPoint.x * 2; h = principalPoint.y * 2;
        }

        // 카메라 내부 파라미터
        Mat cameraMatrix = new Mat(3, 3, MatType.CV_64FC1, new double[]
        {
            fx, 0, principalPoint.x,
            0, fy, principalPoint.y,
            0, 0, 1
        });

        // 왜곡 계수 (k1, k2, p1, p2, k3)
        Mat distCoeffs = new Mat(1, 5, MatType.CV_64FC1, new double[]
        {
            k1, k2, p1, p2, k3
        });

        Mat distorted = new Mat(1, 1, MatType.CV_32FC2);
        distorted.Set(0, 0, new Vec2f(original.x, h-original.y));

        Mat undistorted = new Mat();
        Cv2.UndistortPoints(distorted, undistorted, cameraMatrix, distCoeffs);
        Vec2f normalized = undistorted.At<Vec2f>(0, 0);

        // 3. Convert back to pixel coordinates
        float u = normalized.Item0 * fx + principalPoint.x;
        float v = normalized.Item1 * fy + principalPoint.y;
        v = h - v;
        return new Vector2(u, v);
    }

    public static Vector2 DistortedPoint(Vector2 undistorted, param param_, Vector2 principalPoint,
        float fx = 0, float fy = 0, float w = 0, float h = 0)
    {
        if (param_.fov == 0) return undistorted;
        float k1 = param_.k1;
        float k2 = param_.k2;
        float p1 = param_.p1;
        float p2 = param_.p2;
        float k3 = param_.k3;
        if (fx == 0 || fy == 0 || w == 0 || h == 0)
        {
            float focalLength = principalPoint.y / Mathf.Atan(Mathf.Deg2Rad * param_.fov / 2);
            fx = focalLength; fy = focalLength;
            w = principalPoint.x * 2; h = principalPoint.y * 2;
        }

        float x_u = undistorted.x;
        float y_u = h - undistorted.y;

        // 1. Normalize the undistorted coordinate
        float x = (x_u - principalPoint.x) / fx;
        float y = (y_u - principalPoint.y) / fy;

        // 2. Apply distortion model
        float r2 = x * x + y * y;
        float radial = 1 + k1 * r2 + k2 * r2 * r2 + k3 * r2 * r2 * r2;

        float deltaX = 2 * p1 * x * y + p2 * (r2 + 2 * x * x);
        float deltaY = p1 * (r2 + 2 * y * y) + 2 * p2 * x * y;

        float x_d = x * radial + deltaX;
        float y_d = y * radial + deltaY;

        // 3. Convert back to pixel coordinates
        float u = x_d * fx + principalPoint.x;
        float v = y_d * fy + principalPoint.y;
        v = h - v;

        return new Vector2(u, v);
    }

    public static Matrix4x4 PerspectiveOffCenter(float cx, float cy, float fx, float fy,
                               float w, float h, float near = 0.3f, float far = 1000)
    {
        float left = -cx * near / fx;
        float right = (w - cx) * near / fx;
        float bottom = -(h - cy) * near / fy;
        float top = cy * near / fy;

        return Matrix4x4.Frustum(left, right, bottom, top, near, far);
    }

    public static void SortWithIndices<T, U>(this List<T> fitness, List<U> follower) where T : IComparable<T>
    {
        for (int i = 1; i < fitness.Count; i++)
        {
            T key = fitness[i];
            int j = i - 1;
            while (j >= 0 && key.CompareTo(fitness[j]) < 0)     // key < fitness[j]
            {
                Swap<T>(fitness, j, j + 1);
                Swap<U>(follower, j, j + 1);
                j--;
            }
        }
    }

    public static void Swap<T>(this List<T> list, int from, int to)
    {
        T tmp = list[from];
        list[from] = list[to];
        list[to] = tmp;
    }

    public static param RandParam(float p_min, float p_max, float r_min, float r_max, float fov_min, float fov_max)
    {
        Vector3 rand_pos = new Vector3(UnityEngine.Random.Range(p_min, p_max), UnityEngine.Random.Range(p_min, p_max), UnityEngine.Random.Range(p_min, p_max));
        Vector3 rand_rot = new Vector3(UnityEngine.Random.Range(r_min, r_max), UnityEngine.Random.Range(r_min, r_max), UnityEngine.Random.Range(r_min, r_max));
        float rand_fov = UnityEngine.Random.Range(fov_min, fov_max);

        param param = new param();
        param.pose = rand_pos;
        param.euler_rot = rand_rot;
        param.quat_rot = Quaternion.Euler(rand_rot);
        param.fov = rand_fov;

        return param;
    }

    public static param RandParam(Vector3 pos, Vector3 rot, float fov,
        float k1=0.2f, float k2 = 0, float p1 = 0, float p2 = 0, float k3 = 0)
    {
        Vector3 rand_pos = new Vector3(UnityEngine.Random.Range(-pos.x,pos.x), UnityEngine.Random.Range(-pos.y,pos.y), UnityEngine.Random.Range(-pos.z, pos.z));
        Vector3 rand_rot = new Vector3(UnityEngine.Random.Range(-rot.x, rot.x), UnityEngine.Random.Range(-rot.y, rot.y), UnityEngine.Random.Range(-rot.z, rot.z));
        float rand_fov = UnityEngine.Random.Range(-fov, fov);

        param param = new param();
        param.pose = rand_pos;
        param.euler_rot = rand_rot;
        param.quat_rot = Quaternion.Euler(rand_rot);
        param.fov = rand_fov;
        param.k1 = UnityEngine.Random.Range(-k1, k1);
        param.k2 = UnityEngine.Random.Range(-k2, k2);
        param.p1 = UnityEngine.Random.Range(-p1, p1);
        param.p2 = UnityEngine.Random.Range(-p2, p2);
        param.k3 = UnityEngine.Random.Range(-k3, k3);
        return param;
    }

    public static param AddParam(param par1, param par2)
    {
        param param = new param();
        param.pose = par1.pose+par2.pose;
        param.euler_rot = par1.euler_rot + par2.euler_rot;
        param.quat_rot = Quaternion.Euler(param.euler_rot);
        param.fov = par1.fov + par2.fov;
        param.k1 = par1.k1 + par2.k1;
        param.k2 = par1.k2 + par2.k2;
        param.p1 = par1.p1 + par2.p1;
        param.p2 = par1.p2 + par2.p2;
        param.k3 = par1.k3 + par2.k3;
        return param;
    }

    public static bool IsEqualParam(param par1, param par2)
    {
        if (par1.pose != par2.pose) return false;
        else if (par1.euler_rot != par2.euler_rot) return false;
        else if (par1.fov != par2.fov) return false;
        else if (par1.k1 != par2.k1) return false;
        else if (par1.k2 != par2.k2) return false;
        else if (par1.p1 != par2.p1) return false;
        else if (par1.p2 != par2.p2) return false;
        else if (par1.k3 != par2.k3) return false;
        return true;
    }

    public static param SerialCam2param(DataManager.CamParam camparam)
    {
        param param_ = new param();
        param_.pose = camparam.position;
        param_.euler_rot = camparam.rotation;
        param_.quat_rot = Quaternion.Euler(param_.euler_rot);
        param_.fov = camparam.fov;
        param_.k1 = camparam.k1;
        param_.k2 = camparam.k2;
        param_.p1 = camparam.p1;
        param_.p2 = camparam.p2;
        param_.k3 = camparam.k3;

        return param_;
    }

    public static float Lerp(float a, float b, float inter)
    {
        return a + inter * (b - a);
    }

    public static int RandomIndexByFitness(this List<CamParameters> cam_list, int except_idx = -1, float bias = 10)
    {
        float randf= UnityEngine.Random.Range(0f, 1f);
        float[] fits = new float[cam_list.Count];
        float min_fit = float.MaxValue;
        for(int i = 0;i<cam_list.Count;i++)
        {
            fits[i] = cam_list[i].fitness;
            if (fits[i] == float.MinValue)
            {
                return cam_list.Count-1;
            }
            if (min_fit > fits[i])
            {
                min_fit = fits[i];
            }
        }
        float fit_sum = 0f;
        for(int i = 0; i< fits.Length;i++)
        {
            fits[i] -= min_fit;
            fits[i] += bias;
            if (except_idx == i) fits[i] = 0;
            fit_sum += fits[i];
        }

        float cumulative_sum = 0;
        int idx = 0;
        foreach(float f in fits)
        {
            cumulative_sum += f/fit_sum;
            if (cumulative_sum > randf)
                break;
            idx++;
        }

        return idx;
    }

    public static void SaveInformation(this List<CamParameters> cam_list, string info, string file_path = null)
    {
        info = "===================\n" + info + "\n";
        foreach (CamParameters cam_param in cam_list)
        {
            info += "\ncam name : "+cam_param.cam_tf.name;
            info += "\npos : "+cam_param.param.pose;
            info += "\nrot : " + cam_param.param.euler_rot;
            info += "\nfov : " + cam_param.param.fov;
            info += "\ndistance loss : " + cam_param.dist_loss;
            info += "\nmatch num : " + cam_param.match_num;
            info += "\nfitness : " + cam_param.fitness+"\n";
        }

        info += "===================\n";
        Debug.Log(info);

        if(file_path != null)
        {
            StreamWriter sw = new StreamWriter(file_path);
            sw.WriteLine(info);
            sw.Flush();
            sw.Close();
        }
    }

    public static float[] Softmax(float[] distances)
    {
        // 안정성을 위한 max 값 빼기 (overflow 방지)
        float max = distances.Max();
        float[] expValues = distances.Select(d => (float)Math.Exp(d - max)).ToArray();

        float sum = expValues.Sum();
        float[] softmax = expValues.Select(e => e / sum).ToArray();

        return softmax;
    }
}
