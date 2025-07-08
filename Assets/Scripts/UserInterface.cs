using Ookii.Dialogs;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using System.Text.RegularExpressions;
public class UserInterface : MonoBehaviour
{
    [Header("Control parameters")]
    public float cam_rotate_speed = 5f;
    public float cam_zoom_scale = 10f;
    public float cam_translate_speed = 5f;

    public float measure_scale = 1.5f;

    [Header("Requirements")]
    public GA_optimizer optimizer;
    
    //public Transform measurer;
    public RawImage gt_image;
    public RawImage dt_image;

    public Camera render_cam;

    public GameObject make_virtual;
    public GameObject change2GT;
    public GameObject change2DT;
    public GameObject load_measures;
    public GameObject load_camparam;
    public GameObject change2overlay;
    public GameObject Optim_button;
    public Slider DT_Transparency;
    public GameObject PauseButton;
    public GameObject ExportButton;
    public GameObject CamInfoUI;
    public TMPro.TextMeshProUGUI cam_info;
    public GameObject LogWindow;
    public TMPro.TextMeshProUGUI log_info;
    public GameObject rightTop;
    public GameObject rightBottom;
    public GameObject leftBottom;
    public GameObject InitParam;
    public TMPro.TMP_InputField NumIter;
    public TMPro.TMP_InputField NumPop;
    public TMPro.TMP_InputField NumExchange;
    public TMPro.TMP_InputField PosExpand;
    public TMPro.TMP_InputField RotExpand;
    public TMPro.TMP_InputField FovExpand;
    public TMPro.TMP_InputField RadDistExpand;
    public TMPro.TMP_InputField TanDistExpand;
    public TMPro.TMP_InputField f_img_weight;
    public TMPro.TMP_InputField f_DT_weight;
    public TMPro.TMP_InputField f_entropy_weight;
    public GameObject NumIter_obj;
    public GameObject NumPop_obj;
    public GameObject NumExchange_obj;
    public GameObject PosExpand_obj;
    public GameObject RotExpand_obj;
    public GameObject FovExpand_obj;
    public GameObject RadDistExpand_obj;
    public GameObject TanDistExpand_obj;
    public GameObject f_img_weight_obj;
    public GameObject f_DT_weight_obj;
    public GameObject f_entropy_weight_obj;
    public Toggle is_virtual;
    public TMPro.TMP_InputField ExpName;
    public TMPro.TMP_Dropdown HyperSheduleDrop;
    public Toggle is_exp;
    public TMPro.TMP_InputField k1_dist;
    public TMPro.TMP_InputField k2_dist;
    public TMPro.TMP_InputField p1_dist;
    public TMPro.TMP_InputField p2_dist;
    public TMPro.TMP_InputField k3_dist;
    public GameObject redraw;
    public TMPro.TMP_InputField focal_x;
    public TMPro.TMP_InputField focal_y;
    public TMPro.TMP_InputField center_x;
    public TMPro.TMP_InputField center_y;
    public Toggle ApplyCamMat;

    public Transform Measurer_par;
    public Transform Visual_par;

    public EventSystem eventSystem;         // Unity의 EventSystem (필수)
    public GraphicRaycaster graphicRaycaster; // UI 요소 감지를 위한 GraphicRaycaster

    private param best_par;

    private Texture2D img_raw;
    private Vector3 resolution;

    private Dictionary<int, Vector2> coord2D = new Dictionary<int, Vector2>();
    private string interface_state = ""; // choose among DT, GT, overlay, optimizing

    VistaOpenFileDialog OpenDialog;
    Stream openStream = null;
    Experiments exp_saver;
    // Start is called before the first frame update
    private void Start()
    {
        OpenDialog = new VistaOpenFileDialog();
        OpenDialog.Filter = "json file|*.json;*.JSON|All files  (*.*)|*.*";
        OpenDialog.FilterIndex = 1;
        OpenDialog.Title = "Measurement selector";

        img_raw = ControlManager.img;
        resolution = new Vector3(img_raw.width, img_raw.height, 32);

        GetComponent<CanvasScaler>().referenceResolution = resolution;
        CamParameters.SetRT_resolution(resolution);

        //gt raw image
        gt_image.texture = Utilities.CopyTexture(img_raw);
        optimizer.SetGT_Image(gt_image);
        gt_image.rectTransform.sizeDelta = resolution;

        //dt raw image
        RenderTexture rt = new RenderTexture((int)resolution.x, (int)resolution.y, (int)resolution.z);
        render_cam.targetTexture = rt;
        dt_image.texture = rt;
        dt_image.rectTransform.sizeDelta = new Vector2(img_raw.width / 7, img_raw.height / 7);

        float scaler = Mathf.Min(720 / resolution.x, 480 / resolution.y);   // canvas scale
        rightBottom.transform.localScale = Utilities.NormalizeScale(rightBottom.transform.localScale, scaler);
        rightTop.transform.localScale = Utilities.NormalizeScale(rightTop.transform.localScale, scaler);
        leftBottom.transform.localScale = Utilities.NormalizeScale(leftBottom.transform.localScale, scaler);

        ChangeToGT();

        if (ControlManager.current_state != "new_data")
        {
            foreach(DataManager.DataEntry point in ControlManager.data.data)
            {
                coord2D[point.id] = new Vector2(point.x, point.y);
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(tmp.GetComponent<Collider>());
                tmp.transform.localScale = new Vector3(measure_scale, measure_scale, measure_scale);
                tmp.transform.position = ControlManager.data.measurers[point.id];
                tmp.transform.parent = Measurer_par;
            }

            best_par = Utilities.SerialCam2param(ControlManager.data.camparam);
            render_cam.transform.position = best_par.pose;
            render_cam.transform.eulerAngles = best_par.euler_rot;
            render_cam.fieldOfView = best_par.fov;
            k1_dist.text = best_par.k1.ToString(); k2_dist.text = best_par.k2.ToString();
            p1_dist.text = best_par.p1.ToString(); p2_dist.text = best_par.p2.ToString();
            k3_dist.text = best_par.k3.ToString();
            SetMeasureColor(Measurer_par);
            DrawPoint2D(coord2D);
            
        }

        // Enum 값을 가져와서 드롭다운에 넣기
        HyperSheduleDrop.ClearOptions();
        var enumNames = System.Enum.GetNames(typeof(HyperScheduleMethod));
        HyperSheduleDrop.AddOptions(new System.Collections.Generic.List<string>(enumNames));

        // 초기 선택값 설정 (선택사항)
        HyperSheduleDrop.value = (int)HyperScheduleMethod.Const;
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log(render_cam.transform.worldToLocalMatrix);
        if (interface_state=="GT")
        {
            if (Input.GetMouseButtonDown(0)) Click_GT_image();
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R)) RemoveCoord2D(coord2D.Count - 1);
        }
        else if (interface_state == "DT")
        {
            if (!IsSpecificUIClicked(gt_image.gameObject)) MoveRenderCam();
            if (Input.GetMouseButtonDown(0)) Click_DT_image();
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R)) RemoveMeasure(Measurer_par.childCount - 1);
        }
        else if (interface_state == "overlay")
        {
            MoveRenderCam();
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.D)) PrintFitness();
        }
        else if (interface_state == "optimizing")
        {
            cam_info.text = optimizer.GetCamInfo();
            Visualize();
            if (is_exp.isOn) exp_saver.evaluation(optimizer, is_virtual.isOn);
        }

        Resources.UnloadUnusedAssets();
    }

    public void ChangeScene()
    {
        SceneManager.LoadScene("SceneSelect");
    }
    public void ChangeTranparency()
    {
        gt_image.color = new Color(gt_image.color.r, gt_image.color.g, gt_image.color.b, DT_Transparency.value);
    }
    public void LoadMeasurement()
    {
        string json_path = "";
        if (OpenDialog.ShowDialog() == DialogResult.OK)
        {
            if ((openStream = OpenDialog.OpenFile()) != null)
            {
                openStream.Close();
                json_path = OpenDialog.FileName;
            }
            else return;
        }
        else return;

        for (int i = Measurer_par.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(Measurer_par.GetChild(i).gameObject);
        }
        DataManager.Load_MeasuresJson(json_path, Measurer_par, measure_scale);
        SetMeasureColor(Measurer_par);
    }
    public void LoadCamParam()
    {
        string json_path = "";
        if (OpenDialog.ShowDialog() == DialogResult.OK)
        {
            if ((openStream = OpenDialog.OpenFile()) != null)
            {
                openStream.Close();
                json_path = OpenDialog.FileName;
            }
            else return;
        }
        else return;

        var data_container = DataManager.Load_DataJson(json_path);
        best_par = Utilities.SerialCam2param(data_container.camparam);
        render_cam.transform.position = best_par.pose;
        render_cam.transform.eulerAngles = best_par.euler_rot;
        render_cam.fieldOfView = best_par.fov;
        k1_dist.text = best_par.k1.ToString(); k2_dist.text = best_par.k2.ToString();
        p1_dist.text = best_par.p1.ToString(); p2_dist.text = best_par.p2.ToString();
        k3_dist.text = best_par.k3.ToString();
        DrawPoint2D(coord2D);
    }
    public void pause_optim()
    {
        if (optimizer.ToggleIteration())
        {
            PauseButton.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text = "Resume";
        }
        else
        {
            PauseButton.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text = "Pause";
        }
    }
    public void export_param()
    {
        DataManager data_manager = new DataManager();
        Vector2[] points = coord2D.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();

        CamParameters cam_param = optimizer.GetBestCam();
        string run_path = data_manager.Save_Data(img_raw, points, cam_param.param, Measurer_par);

        StartCoroutine(logging("Saved at "+ run_path, 4, Color.black));
    }

    public void Redraw2D()
    {
        float.TryParse(Regex.Replace(k1_dist.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out best_par.k1);
        float.TryParse(Regex.Replace(k2_dist.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out best_par.k2);
        float.TryParse(Regex.Replace(p1_dist.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out best_par.p1);
        float.TryParse(Regex.Replace(p2_dist.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out best_par.p2);
        float.TryParse(Regex.Replace(k3_dist.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out best_par.k3);

        DrawPoint2D(coord2D);
    }

    public void ApplyCamMatrix()
    {
        if (ApplyCamMat.isOn)
        {
            float fx, fy, cx, cy;
            float.TryParse(Regex.Replace(focal_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fx);
            float.TryParse(Regex.Replace(focal_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fy);
            float.TryParse(Regex.Replace(center_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cx);
            float.TryParse(Regex.Replace(center_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cy);

            if(fx==0 || fy==0 || cx == 0 || cy == 0)
            {
                StartCoroutine(logging("Enter all parameters(fx,fy,cx,cy) of camera matrix", 1.5f, Color.red));
                ApplyCamMat.isOn = false;
                return;
            }
            render_cam.projectionMatrix = Utilities.PerspectiveOffCenter(cx, cy, fx, fy, resolution.x, resolution.y);

            focal_x.interactable = false;
            focal_y.interactable = false;
            center_x.interactable = false;
            center_y.interactable = false;
        }
        else
        {
            focal_x.interactable = true;
            focal_y.interactable = true;
            center_x.interactable = true;
            center_y.interactable = true;

            render_cam.ResetProjectionMatrix();
        }
        
    }

    public void SaveVirtual()
    {
        DataManager data_manager = new DataManager();

        var img = Utilities.RenderToTexture2D(render_cam.activeTexture);

        Vector2[] points = new Vector2[Measurer_par.childCount];
        for(int i =0; i < Measurer_par.childCount;i++)
        {
            points[i] = render_cam.WorldToScreenPoint(Measurer_par.GetChild(i).position);
        }

        param par = new param();
        par.pose = render_cam.transform.position;
        par.quat_rot = render_cam.transform.rotation;
        par.euler_rot = render_cam.transform.eulerAngles;
        par.fov = render_cam.fieldOfView;

        par.k1 = best_par.k1; par.k2 = best_par.k2;
        par.p1 = best_par.p1; par.p2 = best_par.p2;
        par.k3 = best_par.k3;

        string run_path = data_manager.Save_Data(img, points, par, Measurer_par);

        StartCoroutine(logging("Saved at " + run_path, 4, Color.black));
    }
    public void Optimize()
    {
        if (coord2D.Count != Measurer_par.childCount)
        {
            StartCoroutine(logging("Select same number of keypoints in image and DT", 4, Color.red));
            return;
        }

        ChangeToOveray();

        best_par.pose = render_cam.transform.position;
        best_par.euler_rot = render_cam.transform.eulerAngles;
        best_par.fov = render_cam.fieldOfView;
        optimizer.SetInitials(best_par, resolution, gt_image, Measurer_par, coord2D);

        var NumIter_ = int.Parse(Regex.Replace(NumIter.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var NumPop_ = int.Parse(Regex.Replace(NumPop.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var NumExchange_ = int.Parse(Regex.Replace(NumExchange.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var PosEx_ = float.Parse(Regex.Replace(PosExpand.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var RotEx_ = float.Parse(Regex.Replace(RotExpand.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var FovEx_ = float.Parse(Regex.Replace(FovExpand.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var RaDistEx_ = float.Parse(Regex.Replace(RadDistExpand.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var TaDistEx_ = float.Parse(Regex.Replace(TanDistExpand.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var f_img_weight_ = float.Parse(Regex.Replace(f_img_weight.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var f_DT_weight_ = float.Parse(Regex.Replace(f_DT_weight.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        var f_entropy_weight_ = float.Parse(Regex.Replace(f_entropy_weight.text, @"[\u200B-\u200D\uFEFF]", "").Trim());
        HyperScheduleMethod hyper_method_ = (HyperScheduleMethod)HyperSheduleDrop.value;

        optimizer.InitGAParam(num_iteration_: NumIter_, num_population_:NumPop_,
            num_exchange_: NumExchange_, PosExpandRatio_: PosEx_,
            RotExpandRatio_: RotEx_, FovExpandRatio_: FovEx_,
            distort_k_ExpandRatio_: RaDistEx_, distort_p_ExpandRatio_: TaDistEx_, f_img_w: f_img_weight_,
            f_DT_w: f_DT_weight_, f_entropy_w: f_entropy_weight_, hyper_method_: hyper_method_);

        optimizer.iterate();
        dt_image.gameObject.SetActive(false);

        ButtonsActivation(ControlTransparency: true, Pause: true, Export: true, CamInfo: true);

        gt_image.transform.SetAsLastSibling();
        rightTop.transform.SetAsLastSibling();
        rightBottom.transform.SetAsLastSibling();
        leftBottom.transform.SetAsLastSibling();
        interface_state = "optimizing";

        for (int i = Visual_par.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(Visual_par.GetChild(i).gameObject);
        }
        for (int i=0; i < coord2D.Count; i++)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(tmp.GetComponent<Collider>());
            tmp.transform.localScale = new Vector3(measure_scale, measure_scale, measure_scale);
            tmp.transform.parent = Visual_par;
            SetMeasureColor(Visual_par);
        }

        if (is_exp.isOn)
        {
            string exp_name = ExpName.text;
            // experiement name: <environment>_<camera>_<hyper schedule>_<fit weight>_
            exp_saver = new Experiments("Assets/Experiments", exp_name);
        }
    }

    public void ChangeToOveray()
    {
        dt_image.rectTransform.anchoredPosition = new Vector2(0, 0);
        dt_image.rectTransform.sizeDelta = resolution;
        dt_image.rectTransform.localScale = new Vector3(1, 1, 1);
        dt_image.rectTransform.rotation = Quaternion.identity;
        dt_image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dt_image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dt_image.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        gt_image.rectTransform.anchoredPosition = new Vector2(0, 0);
        gt_image.rectTransform.sizeDelta = resolution;
        gt_image.rectTransform.localScale = new Vector3(1, 1, 1);
        gt_image.rectTransform.rotation = Quaternion.identity;
        gt_image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        gt_image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        gt_image.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        gt_image.color = new Color(gt_image.color.r, gt_image.color.g, gt_image.color.b, 0.5f);

        ButtonsActivation(Change2GT: true, Change2DT:true, Change2Overlay:true, Optimize: true,
            ControlTransparency: true, LoadMeasures:true, LoadCamParam:true, Init_Param: true, MakeVirtual: true,
            IsVirtual: true, ExpNameText: true, IsExperim: true, IsDistort:true, ReDraw: true,
            FX: true, FY: true, CX: true, CY: true, CamMat: true);
        dt_image.transform.SetSiblingIndex(0);

        interface_state = "overlay";
    }

    public void ChangeToDT()
    {

        if (interface_state == "GT")
        {
            //gt raw image
            gt_image.rectTransform.anchoredPosition = dt_image.rectTransform.anchoredPosition;
            gt_image.rectTransform.sizeDelta = dt_image.rectTransform.sizeDelta;
            gt_image.rectTransform.localScale = dt_image.rectTransform.localScale;
            gt_image.rectTransform.rotation = dt_image.rectTransform.rotation;
            gt_image.rectTransform.anchorMin = dt_image.rectTransform.anchorMin;
            gt_image.rectTransform.anchorMax = dt_image.rectTransform.anchorMax;
            gt_image.rectTransform.pivot = dt_image.rectTransform.pivot;
        }
        else if (interface_state == "DT") return;
        else
        {
            gt_image.rectTransform.sizeDelta = resolution / 6;
            gt_image.rectTransform.anchorMin = new Vector2(0, 1);
            gt_image.rectTransform.anchorMax = new Vector2(0, 1);
            gt_image.rectTransform.pivot = new Vector2(0, 1);
            gt_image.rectTransform.anchoredPosition = new Vector2(50, -50);
            gt_image.rectTransform.localScale = new Vector3(1, 1, 1);

        }

        //dt raw image
        dt_image.rectTransform.anchoredPosition = new Vector2(0, 0);
        dt_image.rectTransform.sizeDelta = resolution;
        dt_image.rectTransform.localScale = new Vector3(1, 1, 1);
        dt_image.rectTransform.rotation = Quaternion.identity;
        dt_image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dt_image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dt_image.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        ButtonsActivation(Change2DT: true, Change2GT: true, Change2Overlay: true, Optimize: true,
            gt_window: true, LoadMeasures: true, LoadCamParam: true, Init_Param: true, MakeVirtual: true,
            IsVirtual: true, ExpNameText: true, IsExperim: true, IsDistort: true, ReDraw: true,
            FX: true, FY: true, CX: true, CY: true, CamMat: true);
        dt_image.transform.SetSiblingIndex(0);
        interface_state = "DT";
    }

    public void ChangeToGT()
    {
        if (interface_state == "DT")
        {
            //dt raw image
            dt_image.rectTransform.anchoredPosition = gt_image.rectTransform.anchoredPosition;
            dt_image.rectTransform.sizeDelta = gt_image.rectTransform.sizeDelta;
            dt_image.rectTransform.localScale = gt_image.rectTransform.localScale;
            dt_image.rectTransform.rotation = gt_image.rectTransform.rotation;
            dt_image.rectTransform.anchorMin = gt_image.rectTransform.anchorMin;
            dt_image.rectTransform.anchorMax = gt_image.rectTransform.anchorMax;
            dt_image.rectTransform.pivot = gt_image.rectTransform.pivot;
        }
        else if (interface_state == "GT") return;
        else
        {
            dt_image.rectTransform.sizeDelta = resolution/6;
            dt_image.rectTransform.anchorMin = new Vector2(0, 1);
            dt_image.rectTransform.anchorMax = new Vector2(0, 1);
            dt_image.rectTransform.pivot = new Vector2(0,1);
            dt_image.rectTransform.anchoredPosition = new Vector2(50, -50);
            dt_image.rectTransform.localScale = new Vector3(1, 1, 1);

        }

        //gt raw image
        gt_image.rectTransform.anchoredPosition = new Vector2(0,0);
        gt_image.rectTransform.sizeDelta = resolution;
        gt_image.rectTransform.localScale = new Vector3(1,1,1);
        gt_image.rectTransform.rotation = Quaternion.identity;
        gt_image.rectTransform.anchorMin = new Vector2(0.5f,0.5f);
        gt_image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        gt_image.rectTransform.pivot = new Vector2(0.5f,0.5f);

        ButtonsActivation(Change2DT: true, Change2GT:true,Change2Overlay:true,Optimize:true,
            dt_window: true, LoadMeasures:true, LoadCamParam: true, Init_Param:true, MakeVirtual: true,
            IsVirtual: true, ExpNameText: true, IsExperim: true, IsDistort: true, ReDraw: true,
            FX: true, FY: true, CX: true, CY: true, CamMat: true);
        gt_image.transform.SetSiblingIndex(0);
        interface_state = "GT";
    }

    void PrintFitness()
    {
        if (coord2D.Count == Measurer_par.childCount)
        {
            best_par.pose = render_cam.transform.position;
            best_par.euler_rot = render_cam.transform.eulerAngles;
            best_par.quat_rot = render_cam.transform.rotation;
            best_par.fov = render_cam.fieldOfView;

            List<Transform> tmp_measures = new List<Transform>();
            for (int i = 0; i < Measurer_par.childCount; i++)
            {
                tmp_measures.Add(Measurer_par.GetChild(i));
            }

            float f_image, f_DT, f_entropy;
            string fitness_info;
            if (ApplyCamMat.isOn)
            {
                float fx_, fy_, cx_, cy_;
                float.TryParse(Regex.Replace(focal_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fx_);
                float.TryParse(Regex.Replace(focal_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fy_);
                float.TryParse(Regex.Replace(center_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cx_);
                float.TryParse(Regex.Replace(center_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cy_);

                f_image = GA_optimizer.CoordLoss2D(best_par, render_cam, tmp_measures, coord2D, new Vector2(cx_, cy_),
                    fx: fx_,fy:fy_,w:resolution.x,h:resolution.y);
                f_DT = GA_optimizer.CoordLoss3D(best_par, render_cam, tmp_measures, coord2D, new Vector2(cx_, cy_),
                    fx: fx_, fy: fy_, w: resolution.x, h: resolution.y);
                f_entropy = GA_optimizer.DistanceEntropy(best_par, render_cam, tmp_measures, coord2D, new Vector2(cx_, cy_),
                    fx: fx_, fy: fy_, w: resolution.x, h: resolution.y);
            }
            else
            {
                f_image = GA_optimizer.CoordLoss2D(best_par, render_cam, tmp_measures, coord2D, resolution/2);
                f_DT = GA_optimizer.CoordLoss3D(best_par, render_cam, tmp_measures, coord2D, resolution/2);
                f_entropy = GA_optimizer.DistanceEntropy(best_par, render_cam, tmp_measures, coord2D, resolution/2);
            }
            fitness_info = "f_img: " + f_image.ToString() + "  ,  f_DT: " + f_DT.ToString() + "  ,  f_ent: " + f_entropy.ToString();

            if (is_virtual.isOn)
            {
                float pos_error = (best_par.pose - ControlManager.gt_cam.pose).magnitude;
                float angle_error = Quaternion.Angle(best_par.quat_rot, ControlManager.gt_cam.quat_rot);  // degree
                float fov_error = Mathf.Abs(best_par.fov - ControlManager.gt_cam.fov);

                fitness_info += "\npos_error: "+ pos_error.ToString() + "  ,  angle_error: " + angle_error.ToString()
                    + "  ,  fov_error: " + fov_error.ToString();
            }

            StartCoroutine(logging(fitness_info, 10, Color.black));
        }
        else
        {
            StartCoroutine(logging("Select same number of keypoints in image and DT", 4, Color.red));
        }
    }

    void Visualize()
    {
        if (!Utilities.IsEqualParam(best_par, optimizer.GetBestCam().param))    // Undistortpoint 손봐야함. (정확하게, p 있으면 오차 커짐)
        {
            best_par = optimizer.GetBestCam().param;
            DrawPoint2D(coord2D);
        }
        Dictionary<int, Vector2> un_pts = new Dictionary<int, Vector2>();
        foreach (var key_value in coord2D)
        {
            CamParameters cam_param = optimizer.GetBestCam();
            Vector2 undistort_pt = Utilities.UndistortedPoint(coord2D[key_value.Key], cam_param.param, resolution / 2);
            un_pts[key_value.Key] = undistort_pt;
            Ray ray = cam_param.cam.ScreenPointToRay(undistort_pt);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) // Raycast에 충돌이 있으면
            {
                Visual_par.GetChild(key_value.Key).position = hit.point;
            }
        }
        //if (!Utilities.IsEqualParam(best_par, optimizer.GetBestCam().param))
        //{

        //    best_par = optimizer.GetBestCam().param;
        //    DrawPoint2D(un_pts);
        //}
        SetMeasureColor(Visual_par);
    }

    void Click_DT_image()
    {
        if (IsSpecificUIClicked(dt_image.gameObject) && !IsSpecificUIClicked(gt_image.gameObject)
            && IsClickNon_image())
        {
            // 화면 좌표를 RawImage의 로컬 좌표로 변환
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dt_image.rectTransform,
                Input.mousePosition,
                null,
                out localPos);

            // RenderTexture 좌표 계산
            Vector2 textureCoord = new Vector2(localPos.x + img_raw.width / 2, localPos.y + img_raw.height / 2);

            // Raycast 처리: 클릭 위치에서 Ray 발사
            Ray ray = render_cam.ScreenPointToRay(textureCoord);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))  // Raycast에 충돌이 있으면
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(tmp.GetComponent<Collider>());
                tmp.transform.localScale = new Vector3(measure_scale, measure_scale, measure_scale);
                tmp.transform.position = hit.point;
                tmp.transform.parent = Measurer_par;
                SetMeasureColor(Measurer_par);
                //Debug.Log("3D coordinate: " + hit.point);
            }
        }
        else return;
    }

    void Click_GT_image()
    {
        if (IsSpecificUIClicked(gt_image.gameObject) && !IsSpecificUIClicked(dt_image.gameObject)
            && IsClickNon_image())
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gt_image.rectTransform,
                Input.mousePosition,
                null, // Screen Space - Overlay일 경우 null
                out localPoint))
            {
                // RawImage의 RectTransform에서 로컬 좌표를 정규화된 좌표(0~1)로 변환
                Rect rect = gt_image.rectTransform.rect;
                float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
                float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

                int key = 0;
                if (coord2D.Count > 0) key = Mathf.Max(coord2D.Keys.ToArray()) + 1;

                // Texture 크기에 맞춰 픽셀 좌표 계산
                int pixelX = Mathf.RoundToInt(normalizedX * img_raw.width);
                int pixelY = Mathf.RoundToInt(normalizedY * img_raw.height);
                //Debug.Log("2D coordinate: " + normalizedX);

                if (ApplyCamMat.isOn)
                {
                    float fx, fy, cx, cy;
                    float.TryParse(Regex.Replace(focal_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fx);
                    float.TryParse(Regex.Replace(focal_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fy);
                    float.TryParse(Regex.Replace(center_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cx);
                    float.TryParse(Regex.Replace(center_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cy);

                    Vector2 distort_pt = Utilities.DistortedPoint(new Vector2(pixelX, pixelY), best_par,
                        new Vector2(cx, cy), fx, fy, resolution.x, resolution.y);
                    coord2D[key] = distort_pt;     // coord2D는 undistort 되기 전의 좌표
                }
                else
                {
                    Vector2 distort_pt = Utilities.DistortedPoint(new Vector2(pixelX, pixelY), best_par, resolution / 2);
                    coord2D[key] = distort_pt;     // coord2D는 undistort 되기 전의 좌표
                }
                
                DrawPoint2D(coord2D);
            }
        }
    }

    bool IsClickNon_image()
    {
        return !IsSpecificUIClicked(change2overlay) && !IsSpecificUIClicked(change2GT) && !IsSpecificUIClicked(change2DT) &&
            !IsSpecificUIClicked(Optim_button) && !IsSpecificUIClicked(load_measures) && !IsSpecificUIClicked(load_camparam) &&
            !IsSpecificUIClicked(NumIter_obj) && !IsSpecificUIClicked(NumPop_obj) && !IsSpecificUIClicked(NumExchange_obj) &&
            !IsSpecificUIClicked(PosExpand_obj) && !IsSpecificUIClicked(RotExpand_obj) && !IsSpecificUIClicked(FovExpand_obj) &&
            !IsSpecificUIClicked(RadDistExpand_obj) && !IsSpecificUIClicked(TanDistExpand_obj) && !IsSpecificUIClicked(NumIter.gameObject) &&
            !IsSpecificUIClicked(NumPop.gameObject) && !IsSpecificUIClicked(NumExchange.gameObject) && !IsSpecificUIClicked(PosExpand.gameObject) &&
            !IsSpecificUIClicked(RotExpand.gameObject) && !IsSpecificUIClicked(FovExpand.gameObject) && !IsSpecificUIClicked(RadDistExpand.gameObject) &&
            !IsSpecificUIClicked(TanDistExpand.gameObject) && !IsSpecificUIClicked(f_img_weight.gameObject) && !IsSpecificUIClicked(f_DT_weight.gameObject)
            && !IsSpecificUIClicked(f_entropy_weight.gameObject) && !IsSpecificUIClicked(f_img_weight_obj) && !IsSpecificUIClicked(f_DT_weight_obj)
            && !IsSpecificUIClicked(f_entropy_weight_obj) && !IsSpecificUIClicked(make_virtual) && !IsSpecificUIClicked(is_virtual.gameObject)
            && !IsSpecificUIClicked(ExpName.gameObject) && !IsSpecificUIClicked(is_exp.gameObject) && !IsSpecificUIClicked(HyperSheduleDrop.gameObject)
            && !IsSpecificUIClicked(k1_dist.gameObject) && !IsSpecificUIClicked(k2_dist.gameObject) && !IsSpecificUIClicked(p1_dist.gameObject)
            && !IsSpecificUIClicked(p2_dist.gameObject) && !IsSpecificUIClicked(k3_dist.gameObject) && !IsSpecificUIClicked(redraw)
            && !IsSpecificUIClicked(focal_x.gameObject) && !IsSpecificUIClicked(focal_y.gameObject) && !IsSpecificUIClicked(center_x.gameObject)
            && !IsSpecificUIClicked(center_y.gameObject) && !IsSpecificUIClicked(ApplyCamMat.gameObject);
    }

    void SetMeasureColor(Transform measure_par)
    {
        if (measure_par == null) return;

        for (int i = 0; i < measure_par.childCount; i++)
        {
            Transform child = measure_par.GetChild(i);
            Renderer renderer = child.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.material.color = Utilities.Palette(i);
            }
        }
    }

    void RemoveMeasure(int rmidx)
    {
        if (Measurer_par.childCount <= 0)
        {
            StartCoroutine(logging("All measurements are already removed", 3, Color.red));
            return;
        }
        DestroyImmediate(Measurer_par.GetChild(rmidx).gameObject);
    }

    void RemoveCoord2D(int rmkey)
    {
        if (coord2D.Count <= 0)
        {
            StartCoroutine(logging("All 2D keypoints are already removed", 3, Color.red));
            return;
        }

        coord2D.Remove(rmkey);

        var keys = coord2D.Keys.OrderBy(k => k).ToList(); // 키를 오름차순 정렬
        Dictionary<int, Vector2> newCoord2D = new Dictionary<int, Vector2>();

        for (int i = 0; i < keys.Count; i++)
        {
            newCoord2D[i] = coord2D[keys[i]]; // 새로운 키로 값 복사
        }

        coord2D.Clear();
        foreach (var pair in newCoord2D)
        {
            coord2D[pair.Key] = pair.Value; // 기존 Dictionary 업데이트
        }

        DrawPoint2D(coord2D);
    }

    private void ButtonsActivation(bool Change2GT = false, bool Change2DT = false, bool Change2Overlay = false,
        bool Optimize = false, bool ControlTransparency = false, bool Pause = false, bool Export = false,
        bool CamInfo = false, bool gt_window = false, bool dt_window = false, bool LoadMeasures = false,
        bool LoadCamParam = false, bool Init_Param = false, bool MakeVirtual = false, bool IsVirtual = false,
        bool ExpNameText = false, bool IsExperim = false, bool IsDistort = false, bool ReDraw = false,
        bool FX=false, bool FY = false, bool CX = false, bool CY = false, bool CamMat=false)
    {
        make_virtual.SetActive(MakeVirtual);
        change2GT.SetActive(Change2GT);
        change2DT.SetActive(Change2DT);
        change2overlay.SetActive(Change2Overlay);
        Optim_button.SetActive(Optimize);
        DT_Transparency.gameObject.SetActive(ControlTransparency);
        PauseButton.SetActive(Pause);
        ExportButton.SetActive(Export);
        CamInfoUI.SetActive(CamInfo);
        dt_image.GetComponent<WindowManager>().enabled = dt_window;
        gt_image.GetComponent<WindowManager>().enabled = gt_window;
        load_measures.SetActive(LoadMeasures);
        load_camparam.SetActive(LoadCamParam);
        InitParam.SetActive(Init_Param);
        is_virtual.gameObject.SetActive(IsVirtual);
        is_exp.gameObject.SetActive(IsExperim);
        ExpName.gameObject.SetActive(ExpNameText);
        k1_dist.gameObject.SetActive(IsDistort); k2_dist.gameObject.SetActive(IsDistort); 
        p1_dist.gameObject.SetActive(IsDistort); p2_dist.gameObject.SetActive(IsDistort);
        k3_dist.gameObject.SetActive(IsDistort);
        redraw.SetActive(ReDraw);
        focal_x.gameObject.SetActive(FX);
        focal_y.gameObject.SetActive(FY);
        center_x.gameObject.SetActive(CX);
        center_y.gameObject.SetActive(CY);
        ApplyCamMat.gameObject.SetActive(CamMat);
    }

    void DrawPoint2D(Dictionary<int, Vector2> points, param param_= new param())
    {

        float imgsize = Mathf.Min(resolution.x, resolution.y);
        Texture2D texture = Utilities.CopyTexture(img_raw);
        //texture = Utilities.UndistortTexture(texture, resolution,
        //    best_par.fov, best_par.k1, best_par.k2, best_par.p1, best_par.p2, best_par.k3);
        OpenCvSharp.Mat img = OpenCvSharp.Unity.TextureToMat(texture);
        foreach (var key_value in points)
        {
            Color color = Utilities.Palette(key_value.Key);
            OpenCvSharp.Scalar cv_color = Utilities.Color2Scalar(color);
            OpenCvSharp.Cv2.PutText(img, "ID: " + key_value.Key,
                new OpenCvSharp.Point((int)key_value.Value.x + 5, texture.height - 1 * (int)key_value.Value.y - 5),
                OpenCvSharp.HersheyFonts.HersheySimplex, imgsize/500, cv_color, (int)(imgsize/300));

            OpenCvSharp.Cv2.Circle(img, new OpenCvSharp.Point((int)key_value.Value.x, texture.height - 1 * (int)key_value.Value.y),
                (int)(imgsize / 100), cv_color, -1);
        }
        texture = OpenCvSharp.Unity.MatToTexture(img);
        texture.Apply();

        if (ApplyCamMat.isOn)
        {
            float fx, fy, cx, cy;
            float.TryParse(Regex.Replace(focal_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fx);
            float.TryParse(Regex.Replace(focal_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out fy);
            float.TryParse(Regex.Replace(center_x.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cx);
            float.TryParse(Regex.Replace(center_y.text, @"[\u200B-\u200D\uFEFF]", "").Trim(), out cy);

            gt_image.texture = Utilities.UndistortTexture(texture, new Vector2(cx, cy), best_par.fov,
                best_par.k1, best_par.k2, best_par.p1, best_par.p2, best_par.k3, fx, fy);
        }
        else
        {
            gt_image.texture = Utilities.UndistortTexture(texture, resolution / 2,
                best_par.fov, best_par.k1, best_par.k2, best_par.p1, best_par.p2, best_par.k3);
        }
    }


    bool IsSpecificUIClicked(GameObject uiObject)
    {
        if (uiObject == null) return false; // UI 오브젝트가 설정되지 않으면 false 반환

        // 마우스 클릭 위치 기반 이벤트 데이터 생성
        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition // 현재 마우스 위치
        };

        // Raycast 결과를 저장할 리스트
        List<RaycastResult> results = new List<RaycastResult>();

        // UI 요소에 대한 Raycast 수행
        graphicRaycaster.Raycast(eventData, results);

        // 특정 UI(GameObject)가 Raycast 결과에 포함되어 있는지 확인
        foreach (RaycastResult result in results)
        {
            if (result.gameObject == uiObject)
            {
                return true; // 특정 UI가 클릭되었음
            }
        }

        return false; // 특정 UI가 클릭되지 않음
    }

    private void MoveRenderCam()
    {
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x >= 0 && mousePos.x <= UnityEngine.Screen.width &&
               mousePos.y >= 0 && mousePos.y <= UnityEngine.Screen.height)
        {
            camRotate();
            camTranlate();
            camZoom();
        }
    }
    void camTranlate()
    {
        if (Input.GetMouseButton(2))
        {
            Vector2 m_Input;
            m_Input.x = Input.GetAxis("Mouse X") * cam_translate_speed;
            m_Input.y = Input.GetAxis("Mouse Y") * cam_translate_speed;


            if (m_Input.magnitude != 0)
            {
                render_cam.transform.position -= render_cam.transform.right * m_Input.x + render_cam.transform.up * m_Input.y;

            }
        }
    }

    void camRotate()
    {
        if (Input.GetMouseButton(1))
        {
            Vector2 m_Input;
            m_Input.x = Input.GetAxis("Mouse X") * cam_rotate_speed;
            m_Input.y = Input.GetAxis("Mouse Y") * cam_rotate_speed;

            if (m_Input.magnitude != 0)
            {
                render_cam.transform.Rotate(render_cam.transform.right, -m_Input.y, Space.World);
                render_cam.transform.Rotate(Vector3.up, m_Input.x, Space.World);
            }

            Vector3 k_Input = new Vector3(0, 0, 0);
            if (k_Input.magnitude != 0)
            {
                render_cam.transform.position += render_cam.transform.forward * k_Input.x + render_cam.transform.right * k_Input.y + render_cam.transform.up * k_Input.z;
            }
        }
    }

    void camZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            render_cam.transform.position = new Vector3(render_cam.transform.position.x, render_cam.transform.position.y, render_cam.transform.position.z) + render_cam.transform.forward * scroll * cam_zoom_scale;
        }
    }

    IEnumerator logging(string log, float wait_t, Color color)
    {
        LogWindow.SetActive(true);
        log_info.text = log;
        log_info.color = color;
        yield return new WaitForSeconds(wait_t);
        LogWindow.SetActive(false);
    }
}
