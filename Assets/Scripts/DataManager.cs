using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

public class DataManager
{
    private string dir_path;

    public DataManager()
    {
        dir_path = Application.persistentDataPath;
    }
    public DataManager(string dir_path_)
    {
        dir_path = dir_path_;
    }

    [System.Serializable]
    public class CamParam
    {
        public Vector3 position;
        public Vector3 rotation;
        public float fov;
        // distortion 추가
        public float k1;
        public float k2;
        public float p1;
        public float p2;
        public float k3;
    }

    [System.Serializable]
    public class Info
    {
        public string img_path;
        public int height;
        public int width;
    }

    [System.Serializable]
    public class DataContainer
    {
        public string _comment = "data: (id, x, y), cam_param: (x,y,z)";
        public Info info;
        public List<DataEntry> data;
        public CamParam camparam;
        public List<Vector3> measurers;
    }

    [System.Serializable]
    public class DataEntry
    {
        public int id;
        public float x;
        public float y;
    }

    public string GetDirPath()
    {
        return dir_path;
    }
    public static param Load_GT_cam(string runPath)
    {
        string json_path = Path.Combine(runPath, "gt_cam.json");
        if (File.Exists(json_path))
        {
            var jsonString = File.ReadAllText(json_path);
            var container = JsonUtility.FromJson<DataContainer>(jsonString);
            Debug.Log(container.info.height);
            Debug.Log(container.camparam.position);
            return Utilities.SerialCam2param(container.camparam);
        }
        else
        {
            Debug.Log("no gt_cam.json exist");
            return new param();
        }
    }
    public static Texture2D Load_Image(string runPath)
    {
        DataContainer json_data = Load_Data(runPath);
        Texture2D img = Utilities.LoadImage(json_data.info.img_path);
        return img;
    }
    public static DataContainer Load_Data(string runPath)
    {
        string json_path = Path.Combine(runPath, "data.json");
        var jsonString = File.ReadAllText(json_path);
        return JsonUtility.FromJson<DataContainer>(jsonString);
    }
    public static DataContainer Load_DataJson(string json_path)
    {
        var jsonString = File.ReadAllText(json_path);
        return JsonUtility.FromJson<DataContainer>(jsonString);
    }

    public static Dictionary<int, Vector2> Load_2DCoord(string runPath)
    {
        DataContainer json_data = Load_Data(runPath);
        Dictionary<int, Vector2> coord2D = new Dictionary<int, Vector2>();
        foreach(var data_entry in json_data.data)
        {
            coord2D[data_entry.id] = new Vector2(data_entry.x, data_entry.y);
        }
        return coord2D;
    }

    public static void Load_Measures(string runPath, Transform measure_par, float scale = 1.5f)
    {
        DataContainer json_data = Load_Data(runPath);
        foreach (Vector3 point in json_data.measurers)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject.DestroyImmediate(tmp.GetComponent<Collider>());
            tmp.transform.localScale = new Vector3(scale, scale, scale);
            tmp.transform.position = point;
            tmp.transform.parent = measure_par;
        }
    }

    public static void Load_MeasuresJson(string jsonPath, Transform measure_par, float scale=1.5f)
    {
        var jsonString = File.ReadAllText(jsonPath);
        DataContainer json_data = JsonUtility.FromJson<DataContainer>(jsonString);
        foreach (Vector3 point in json_data.measurers)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject.DestroyImmediate(tmp.GetComponent<Collider>());
            tmp.transform.localScale = new Vector3(scale, scale, scale);
            tmp.transform.position = point;
            tmp.transform.parent = measure_par;
        }
    }

    public string[] RunDirList()
    {
        return Directory.GetDirectories(dir_path, "run*")
            .Select(path => new {
                Path = path,
                Num = ExtractNumber(Path.GetFileName(path))
            })
            .OrderBy(x => x.Num)
            .Select(x => x.Path)
            .ToArray();
    }

    int ExtractNumber(string folderName)
    {
        var match = Regex.Match(folderName, @"run(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }
    //public string[] RunDirList()
    //{
    //    return Directory.GetDirectories(dir_path, "run*");
    //}

    public string Save_Data(Texture2D image, Vector2[] points, param cam_param, Transform measurer) // run 경로 반환
    {
        string run_path = MakeRunDir();
        string img_path = Path.Combine(run_path, "image.png");
        string data_path = Path.Combine(run_path, "data.json");


        Utilities.SavePNG(image, img_path);

        // points, cam_param, measurer를 json 파일로 
        DataContainer container = new DataContainer
        {
            info = new Info { img_path = img_path, height = image.height, width = image.width },
            data = new List<DataEntry>(),
            camparam = new CamParam { 
                position= cam_param.pose,
                rotation = cam_param.euler_rot,
                fov = cam_param.fov,
                k1 = cam_param.k1,k2 = cam_param.k2,p1 = cam_param.p1,p2 = cam_param.p2,k3 = cam_param.k3,
            },
            measurers = new List<Vector3>()
        };
        for (int i = 0; i < points.Length; i++) // 데이터를 저장 (id, x, y)
        {
            container.data.Add(new DataEntry { id = i, x = points[i].x, y = points[i].y });
            container.measurers.Add(measurer.GetChild(i).position);
        }
        string json = JsonUtility.ToJson(container, true); // JSON 변환 및 파일 저장
        File.WriteAllText(data_path, json);

        return run_path;
    }


    private string MakeRunDir() { return MakeRunDir(dir_path); }
    static string MakeRunDir(string rootPath)
    {
        // rootPath가 존재하지 않으면 생성
        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
        }

        // run 폴더들 찾기 (예: run1, run2, run3 ...)
        var existingRunNumbers = Directory.GetDirectories(rootPath, "run*")
            .Select(dir => Path.GetFileName(dir))
            .Where(name => name.StartsWith("run") && int.TryParse(name.Substring(3), out _))
            .Select(name => int.Parse(name.Substring(3)))
            .OrderBy(num => num)
            .ToList();

        // 존재하지 않는 가장 작은 번호 찾기
        int newRunNumber = 1;
        while (existingRunNumbers.Contains(newRunNumber))
        {
            newRunNumber++;
        }

        // 새 run 폴더 생성
        string newRunPath = Path.Combine(rootPath, $"run{newRunNumber}");
        Directory.CreateDirectory(newRunPath);

        return newRunPath;
    }
}
