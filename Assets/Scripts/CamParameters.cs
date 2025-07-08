using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;


    

public class CamParameters
{
    public static Vector3 RT_resolution { get; private set; }

    public param param;

    public float fitness = float.MinValue;
    public float f_img = float.MinValue;
    public float f_DT = float.MinValue;
    public float f_entropy = float.MinValue;
    public float dist_loss = float.MaxValue;
    public int match_num = 0;

    public Transform cam_tf { get; private set; }
    public Camera cam { get; private set; }
    public RenderTexture rt { get; private set; }

    public RawImage ri;

    // particla swarm optimization
    public param velocity;
    public param local_optimal;
    public bool is_global;
    public float best_fitness = float.MinValue;

    public CamParameters(string name = "")
    {
        init(new Vector3(), new Vector3(), 0, name);
    }

    public CamParameters(Vector3 position, Quaternion rotation, float fov, string name = "")
    {
        init(position, rotation.eulerAngles, fov, name);
    }

    public CamParameters(Vector3 position, Vector3 eulerRotation, float fov, string name = "")
    {
        init(position, eulerRotation, fov, name);
    }

    public CamParameters(param param_, string name = "")
    {
        init(param_, name);
    }

    // used at GT
    public void AssignObjects(Transform tf, Camera cam, RenderTexture rt)
    {
        DestroyObjects();

        this.cam_tf = tf;
        this.cam = cam;

        this.rt = rt;

        ApplyParams();
    }

    // used at population
    public void InstantiateObjects(Transform tf, Camera cam, RenderTexture rt)
    {
        DestroyObjects();

        this.cam_tf = Object.Instantiate(tf);
        this.cam = Object.Instantiate(cam);
        this.rt = Object.Instantiate(rt);

        ApplyParams();
    }

    public void SetParams(Vector3 position, Vector3 eulerRotation, float fov, bool apply = false)
    {
        param.pose = position;
        param.euler_rot = eulerRotation;
        param.quat_rot = Quaternion.Euler(eulerRotation);
        param.fov = fov;

        if (apply)
        {
            ApplyParams();
        }
    }

    public void SetParams(param param, bool apply = false)
    {
        this.param = param;
        this.param.quat_rot = Quaternion.Euler(param.euler_rot);

        if (apply)
        {
            ApplyParams();
        }
    }

    public void SetFoV(float fov)
    {
        this.param.fov = fov;
        ApplyParams();
    }

    public void DestroyObjects()
    {
        Object.DestroyImmediate(this.cam_tf.gameObject);
        Object.DestroyImmediate(this.cam);
        Object.DestroyImmediate(this.rt);
        if (this.ri != null)
        {
            Object.DestroyImmediate(this.ri.gameObject);
        }
            
    }

    private void ApplyParams()
    {
        cam_tf.transform.position = param.pose;
        cam_tf.transform.eulerAngles = param.euler_rot;
        cam.fieldOfView = param.fov;
        //cam.fieldOfView = 60;
    }

    private void MakeObjects(string name)
    {
        var tmp_obj = new GameObject("cam" + name);
        tmp_obj.transform.position = param.pose;
        tmp_obj.transform.rotation = param.quat_rot;
        this.cam_tf = tmp_obj.transform;

        tmp_obj.AddComponent<Camera>();
        var tmp_cam = tmp_obj.GetComponent<Camera>();
        tmp_cam.fieldOfView = param.fov;
        this.cam = tmp_cam;

        var tmp_rt = new RenderTexture((int)RT_resolution.x, (int)RT_resolution.y, (int)RT_resolution.z);
        this.rt = tmp_rt;
        this.cam.targetTexture = tmp_rt;

    }

    private void init(Vector3 position, Vector3 eulerRotation, float fov, string name)
    {
        MakeObjects(name);
        SetParams(position, eulerRotation, fov, true);
    }
    private void init(param param_, string name)
    {
        MakeObjects(name);
        SetParams(param_, true);
    }

    //public void Initialize(Vector3 position, Vector3 eulerRotation, float fov, string name)
    //{
    //    SetParams(position, eulerRotation, fov, true);
    //}

    public static void SetRT_resolution(Vector3 resol) { RT_resolution = resol; }

}