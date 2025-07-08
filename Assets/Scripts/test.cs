using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
    public Transform target1;
    public Transform target2;

    public bool debug_dist = false;

    Vector3 x_unit_unity = new Vector3(-0.9537934f, 0.0830108922f, -0.2887685642f);
    Vector3 y_unit_unity = new Vector3(0.2786351f, 0.60399241f, -0.7466964532f);
    Vector3 z_unit_unity = new Vector3(0.11243008f, -0.792655f, -0.5992137f);
    // Start is called before the first frame update
    void Start()
    {
        // 1. 회전 행렬 만들기 (축은 열벡터)
        Matrix4x4 rotMatrix = new Matrix4x4();
        rotMatrix.SetColumn(0, x_unit_unity.normalized); // X축
        rotMatrix.SetColumn(1, y_unit_unity.normalized); // Y축
        rotMatrix.SetColumn(2, z_unit_unity.normalized); // Z축
        rotMatrix.SetColumn(3, new Vector4(0, 0, 0, 1)); // homogenous

        // 2. Quaternion으로 변환
        Quaternion rot = rotMatrix.rotation;

        // 3. EulerAngles (ZXY 순서 사용됨)
        Vector3 eulerAngles = rot.eulerAngles;

        Debug.Log("Unity EulerAngles (Inspector): " + eulerAngles);
        transform.eulerAngles = eulerAngles;

        float cx = 1798.95556f;
        float cy = 1482.76915f;
        float fx = 2232.8054f;
        float fy = 2261.6373f;
        Matrix4x4 projectionMatrix = Utilities.PerspectiveOffCenter(
            cx, cy, fx, fy, 3840, 2160, 0.3f, 1000);
        //GetComponent<Camera>().projectionMatrix = projectionMatrix;
    }

    // Update is called once per frame
    void Update()
    {
        if (debug_dist)
        {
            float dist = (target1.position - target2.position).magnitude;
            Debug.Log("distance between targets: " + dist.ToString() + "[m]");
        }
    }

    
}
