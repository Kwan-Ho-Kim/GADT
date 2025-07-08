using OpenCvSharp;
using OpenCvSharp.XFeatures2D;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.SceneManagement;

public class GA_optimizer : MonoBehaviour
{
    [Header("Best Camera")]
    private RenderTexture cam_rt;
    private Camera cam;
    //public Transform cam_tf;    // initial camera transform

    [Header("UI")]
    public RawImage gt_image;

    [Header("Init parameters")]
    public int num_iteration = 300;
    public int num_population = 50;

    [Header("Hyper parameters")]
    public int num_exchange = 20;
    public float PosRandomRange = 5f;
    public float RotRandomRange = 10f;
    public float FovRandomRange = 10f;

    public float PosExpandRatio = 2;
    public float RotExpandRatio = 2;
    public float FovExpandRatio = 2;
    public float distort_k_ExpandRatio = 0.1f;
    public float distort_p_ExpandRatio = 0;

    public float child_mutation_prob = 0.5f;
    public float component_mutation_prob = 0.5f;

    public int HPschedule_period = 50;
    public HyperScheduleMethod hyper_method;
    private int init_num_exchange = 20;
    private float init_PosRandomRange = 5f;
    private float init_RotRandomRange = 10f;
    private float init_FovRandomRange = 10f;

    private float init_PosExpandRatio = 2;
    private float init_RotExpandRatio = 2;
    private float init_FovExpandRatio = 2;

    private float init_distort_k_ExpandRatio = 0.1f;
    private float init_distort_p_ExpandRatio = 0.05f;

    private float init_child_mutation_prob = 0.5f;
    private float init_component_mutation_prob = 0.5f;

    private int num_hyper_update = 0;

    [Header("Fitness function")]
    public float MaxDist = 100;
    public float weight_2D = 1;
    public float weight_3D = 2;
    public float weight_entropy = 1;

    private float init_weight_2D = 1;
    private float init_weight_3D = 2;
    private float init_weight_entropy = 1;

    [Header("Feature Loss")]
    public float FeatureFilterRatio = 0.1f;
    public float alpha = 0.6f;
    public float OverfitThres = 20f;

    private int iter = 0;
    private int episode = 0;
    private int local_min_change_num = 0;

    [Header("Utils")]
    public Canvas canvas;

    private param start_param;

    private Vector3 RT_resolution = new Vector3(1920, 1080, 32);

    private List<CamParameters> CamList = new List<CamParameters>();

    private Mat gt_mat;

    private bool is_find_global = false;
    private bool is_find_optimal = false;
    public static bool is_pause = false;

    private CamParameters local_minimum;

    private Dictionary<int, Vector2> coord = new Dictionary<int, Vector2>(); // not normalized, origin is left-bottom
    private List<Transform> measures = new List<Transform>();

    private float best_fit = float.MinValue;
    private int optimal_period = 0;

    private string info = "";
    //public static string Experiment_path = "Assets/CamOptimizer/Test/Runtime/Resources/Experiments/";

    private void Start()
    {
        //start_pos = cam_tf.position;
        //start_rot = cam_tf.eulerAngles;
    }

    public void iterate() // call iterate function every frame for optimization
    {
        InitPopulation();
        StartCoroutine(optimize());

        if (is_find_optimal && !is_find_global)            // find another optimal
        {

            if (local_minimum != null)
            {
                if (local_minimum.fitness < CamList[CamList.Count - 1].fitness)
                {
                    local_min_change_num++;
                    local_minimum.DestroyObjects();

                    local_minimum = CamList[CamList.Count - 1];
                    local_minimum.cam_tf.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                    local_minimum.ri.gameObject.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                    local_minimum.ri.color = new Color(1, 1, 1, 0);

                    Debug.Log("local minimum changed");
                }
                else
                {
                    CamList[CamList.Count - 1].DestroyObjects();
                }
            }
            else   // first time
            {
                local_minimum = CamList[CamList.Count - 1];
                local_minimum.cam_tf.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                local_minimum.ri.gameObject.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                local_minimum.ri.color = new Color(1, 1, 1, 0);

            }

            CamList.RemoveAt(CamList.Count - 1);

            param rand_param = Utilities.RandParam(new Vector3(PosRandomRange * 2, PosRandomRange * 2, PosRandomRange * 2), new Vector3(RotRandomRange * 2, RotRandomRange * 2, RotRandomRange * 2), FovRandomRange * 2);
            for (int i = 0; i < num_population; i++)
            {
                param tmp_rand_param = Utilities.RandParam(-PosRandomRange, PosRandomRange, -RotRandomRange, RotRandomRange, -FovRandomRange, FovRandomRange);

                if (i == num_population - 1)
                {
                    // name should be modified
                    var tmp_cam_param = new CamParameters(rand_param.pose + tmp_rand_param.pose + local_minimum.param.pose, rand_param.euler_rot + tmp_rand_param.euler_rot + local_minimum.param.euler_rot, rand_param.fov, "_episode" + episode.ToString() + "_new one");

                    CamList.Add(tmp_cam_param);

                    var tmp_ui = Instantiate(gt_image.gameObject, gt_image.transform.parent, true);
                    tmp_ui.name = "image_episode_" + episode.ToString() + "_new one";
                    tmp_cam_param.ri = tmp_ui.GetComponent<RawImage>();
                    tmp_cam_param.ri.texture = tmp_cam_param.rt;
                }
                else
                {
                    CamList[i].SetParams(rand_param.pose + tmp_rand_param.pose + local_minimum.param.pose, rand_param.euler_rot + tmp_rand_param.euler_rot + local_minimum.param.euler_rot, rand_param.fov + tmp_rand_param.fov + local_minimum.param.fov, true);
                }
            }

            is_find_optimal = false;
        }
    }

    // APIs
    public void SetGT_Image(RawImage gt_image_)
    {
        gt_image = gt_image_;
    }
    public bool ToggleIteration()   // return is_pause after toggle
    {
        is_pause = !is_pause;
        return is_pause;
    }
    public string GetCamInfo()
    {
        return info;
    }

    public void AddMeasures(Transform[] measures_)
    {
        measures.Clear();
        for (int i = 0; i < measures_.Length; i++)
        {
            measures.Add(measures_[i]);
        }
    }

    public void AddMeasuresByParent(Transform measurer_parent)
    {
        measures.Clear();
        for (int i = 0; i < measurer_parent.childCount; i++)
        {
            measures.Add(measurer_parent.GetChild(i));
        }
    }

    public void Put_Coordinates(Dictionary<int, Vector2> gt_coordinates)    // (1, x, y,), (2, x, y) ... in image coordinate
    {
        coord = gt_coordinates;
    }

    public void SetInitials(param start_param_, Vector2 RT_resol_, RawImage gt_image_, Transform measurer_parent, Dictionary<int, Vector2> gt_coordinates)
    {

        RT_resolution = new Vector3(RT_resol_.x, RT_resol_.y, 32);
        canvas.GetComponent<CanvasScaler>().referenceResolution = RT_resol_;
        CamParameters.SetRT_resolution(RT_resolution);

        start_param = start_param_;

        SetGT_Image(gt_image_);
        gt_image.rectTransform.sizeDelta = RT_resol_;

        AddMeasuresByParent(measurer_parent);
        Put_Coordinates(gt_coordinates);
    }

    public void InitGAParam(int num_iteration_ = 2000, int num_population_ = 20, int num_exchange_ = 13,
        float PosRandomRange_ = 2, float RotRandomRange_ = 3, float FovRandomRange_ = 2,
        float PosExpandRatio_ = 1, float RotExpandRatio_ = 1, float FovExpandRatio_ = 1,
        float child_mutation_prob_ = 0f, float component_mutation_prob_ = 0f,
        float FeatureFilterRatio_ = 0.1f, float alpha_ = 0.6f, float OverfitThres_ = 20f,
        float distort_k_ExpandRatio_ = 0.1f, float distort_p_ExpandRatio_ = 0, float f_img_w = 1,
        float f_DT_w = 5, float f_entropy_w = 10, HyperScheduleMethod hyper_method_ = HyperScheduleMethod.Exponential)
    {
        num_iteration = num_iteration_;
        num_population = num_population_;
        init_num_exchange = num_exchange_;

        init_PosRandomRange = PosRandomRange_;
        init_RotRandomRange = RotRandomRange_;
        init_FovRandomRange = FovRandomRange_;

        init_PosExpandRatio = PosExpandRatio_;
        init_RotExpandRatio = RotExpandRatio_;
        init_FovExpandRatio = FovExpandRatio_;

        init_distort_k_ExpandRatio = distort_k_ExpandRatio_;
        init_distort_p_ExpandRatio = distort_p_ExpandRatio_;

        init_child_mutation_prob = child_mutation_prob_;
        init_component_mutation_prob = component_mutation_prob_;

        FeatureFilterRatio = FeatureFilterRatio_;
        alpha = alpha_;
        OverfitThres = OverfitThres_;

        init_weight_2D = f_img_w;
        init_weight_3D = f_DT_w;
        init_weight_entropy = f_entropy_w;

        hyper_method = hyper_method_;

        SetGAParam(num_iteration_ = num_iteration, num_population_ = num_population, num_exchange_ = init_num_exchange,
                    PosRandomRange_ = init_PosRandomRange, RotRandomRange_ = init_RotRandomRange, FovRandomRange_ = init_FovRandomRange,
                    PosExpandRatio_ = init_PosExpandRatio, RotExpandRatio_ = init_RotExpandRatio, FovExpandRatio_ = init_FovExpandRatio,
                    child_mutation_prob_ = init_child_mutation_prob, component_mutation_prob_ = init_component_mutation_prob,
                    FeatureFilterRatio_ = FeatureFilterRatio, alpha_ = alpha, OverfitThres_ = OverfitThres,
                    distort_k_ExpandRatio_ = init_distort_k_ExpandRatio, distort_p_ExpandRatio_ = init_distort_p_ExpandRatio, f_img_w = init_weight_2D,
                    f_DT_w = init_weight_3D, f_entropy_w = init_weight_entropy, hyper_method_ = hyper_method);
    }

    public void SetGAParam(int num_iteration_ = 2000, int num_population_ = 20, int num_exchange_ = 13,
        float PosRandomRange_=2, float RotRandomRange_=3, float FovRandomRange_ = 2,
        float PosExpandRatio_=1, float RotExpandRatio_=1, float FovExpandRatio_=1,
        float child_mutation_prob_=0f, float component_mutation_prob_=0f,
        float FeatureFilterRatio_=0.1f, float alpha_=0.6f, float OverfitThres_=20f,
        float distort_k_ExpandRatio_ = 0.1f, float distort_p_ExpandRatio_ = 0, float f_img_w=1,
        float f_DT_w=5, float f_entropy_w=10, HyperScheduleMethod hyper_method_ =HyperScheduleMethod.Exponential)
    {
        num_iteration = num_iteration_;
        num_population = num_population_;
        num_exchange = num_exchange_;

        PosRandomRange = PosRandomRange_;
        RotRandomRange = RotRandomRange_;
        FovRandomRange = FovRandomRange_;

        PosExpandRatio = PosExpandRatio_;
        RotExpandRatio = RotExpandRatio_;
        FovExpandRatio = FovExpandRatio_;

        distort_k_ExpandRatio = distort_k_ExpandRatio_;
        distort_p_ExpandRatio = distort_p_ExpandRatio_;

        child_mutation_prob = child_mutation_prob_;
        component_mutation_prob = component_mutation_prob_;

        FeatureFilterRatio = FeatureFilterRatio_;
        alpha = alpha_;
        OverfitThres = OverfitThres_;

        weight_2D = f_img_w;
        weight_3D = f_DT_w;
        weight_entropy = f_entropy_w;

        hyper_method = hyper_method_;
    }

    public CamParameters GetBestCam()
    {
        return CamList[CamList.Count - 1];
    }

    void ReadFile(string file_path_)
    {
        FileStream data_file = new FileStream(file_path_, FileMode.Open);
        StreamReader reader = new StreamReader(data_file);
        foreach (string line in reader.ReadToEnd().Split(new char[] { '\n' }))
        {
            string[] data = line.Split(" ");
            if (data.Length >= 5)
            {
                int id = int.Parse(data[0]);
                float x = float.Parse(data[1]);
                float y = float.Parse(data[2]);
                float width = float.Parse(data[3]);
                float height = float.Parse(data[4]);
                //List<float> xy = new List<float>();
                //xy.Add(x * width);
                //xy.Add(y * height);
                coord[id] = new Vector2(x * width, height - y * height);
            }
        }
    }

    void InitPopulation()
    {
        CamParameters tmp_cam_param = new CamParameters(start_param, "_1");
        CamList.Add(tmp_cam_param);
        //Debug.Log(gt_image.texture.width);
        var first_ui = Instantiate(gt_image.gameObject, gt_image.transform.parent, true);
        first_ui.name = "image_1";
        tmp_cam_param.ri = first_ui.GetComponent<RawImage>();
        tmp_cam_param.ri.texture = tmp_cam_param.rt;
        for (int i = 1; i < num_population; i++)
        {
            param rand_param = Utilities.RandParam(Vector3.one*PosExpandRatio, Vector3.one * RotExpandRatio, FovExpandRatio,
                distort_k_ExpandRatio, distort_k_ExpandRatio, distort_p_ExpandRatio, distort_p_ExpandRatio, distort_k_ExpandRatio);

            tmp_cam_param = new CamParameters(Utilities.AddParam(start_param,rand_param), "_" + (i + 1).ToString());
            //tmp_cam_param = new CamParameters(start_param.pose + rand_param.pose, start_param.euler_rot + rand_param.euler_rot, start_param.fov + rand_param.fov, "_" + (i + 1).ToString());

            CamList.Add(tmp_cam_param);

            var tmp_ui = Instantiate(gt_image.gameObject, gt_image.transform.parent, true);
            tmp_ui.name = "image_" + (i + 1).ToString();
            tmp_cam_param.ri = tmp_ui.GetComponent<RawImage>();
            tmp_cam_param.ri.texture = tmp_cam_param.rt;
        }

    }

    void SortByFitness()
    {
        // Calculate fitness
        List<float> fitnesses = new List<float>();
        foreach (var cam_param in CamList)
        {
            //cam_param.fitness = fitness_fn(cam_param, FitMethod);
            cam_param.f_img = CoordLoss2D(cam_param);
            cam_param.f_DT = CoordLoss3D(cam_param);
            cam_param.f_entropy = DistanceEntropy(cam_param);
            cam_param.fitness = weight_2D* cam_param.f_img + weight_3D * cam_param.f_DT + weight_entropy * cam_param.f_entropy;
            fitnesses.Add(cam_param.fitness);
        }

        // Sort
        Utilities.SortWithIndices(fitnesses, CamList);

        info = iter.ToString() + "/" + num_iteration.ToString() + " iteration";
        info += "\n\nFitness : " + CamList[CamList.Count - 1].fitness.ToString();
        info += "\nCamera parameters";
        info += "\n-Position : " + CamList[CamList.Count - 1].param.pose.ToString();
        info += "\n-Rotation : " + CamList[CamList.Count - 1].param.euler_rot.ToString();
        info += "\n-FoV : " + CamList[CamList.Count - 1].param.fov.ToString();
        info += "\n-k1,k2,p1,p2,k3 : " + CamList[CamList.Count - 1].param.k1.ToString()+"," + CamList[CamList.Count - 1].param.k2.ToString()
            + "," + CamList[CamList.Count - 1].param.p1.ToString() + "," + CamList[CamList.Count - 1].param.p2.ToString() + "," + CamList[CamList.Count - 1].param.k3.ToString();
        //info += "\nepisode : " + episode.ToString();
        //info += "\nlocal minimum changed num : " + local_min_change_num.ToString();


        CamList[CamList.Count - 1].ri.color = new Color(1, 1, 1, 1);
        for (int i = 0; i < CamList.Count - 1; i++)
        {
            CamList[i].ri.color = new Color(1, 1, 1, 0);
        }
    }

    float fitness_fn(CamParameters camparam, fitness_method method)
    {
        float fitness = 0;
        switch (method)
        {
            case fitness_method.TemplateMatching:
                Mat source = Utilities.RenderToMat(camparam.rt);
                Mat res = new Mat();
                Cv2.MatchTemplate(gt_mat, source, res, TemplateMatchModes.CCoeffNormed);
                fitness = -(float)res.At<float>(0, 0);       // unity에서 float : 32byte
                break;
            case fitness_method.SIFT:
                fitness = FeatureLoss(camparam);
                break;
            case fitness_method.Coord:
                fitness = CoordLoss3D(camparam);
                break;
        }

        return fitness;
    }

    float CoordLoss2D(CamParameters camparam)
    {
        return CoordLoss2D(camparam.param, camparam.cam, measures, coord, RT_resolution/2);
    }

    public static float CoordLoss2D(param camparam, Camera cam, List<Transform> measure_childs,
        Dictionary<int, Vector2> coord2D, Vector2 principlePoint, float fx = 0, float fy =0, float w=0, float h=0)
    {
        int i = 0;
        float TotalLoss = 0;
        foreach (var tran in measure_childs)
        {
            var gt = Utilities.UndistortedPoint(coord2D[i], camparam, principlePoint, fx, fy, w, h);
            Vector3 tmp_pos = cam.WorldToScreenPoint(tran.position);     // tmp_pos: x, y, depth 픽셀좌표 (origin: left-bottom)
            float dist = (gt - new Vector2(tmp_pos.x, tmp_pos.y)).magnitude;
            TotalLoss += dist;
            i++;
        }

        return -TotalLoss / measure_childs.Count;
    }

    float CoordLoss3D(CamParameters camparam)
    {
        return CoordLoss3D(camparam.param, camparam.cam, measures, coord, RT_resolution/2, MaxDist);
    }

    public static float CoordLoss3D(param camparam, Camera cam, List<Transform> measure_childs,
        Dictionary<int, Vector2> coord2D, Vector2 principlePoint, float MaxDist_=100, float fx = 0, float fy = 0,
        float w = 0, float h = 0)
    {
        int i = 0;
        float TotalLoss = 0;
        foreach (var tran in measure_childs)
        {
            Vector2 undistort_pt = Utilities.UndistortedPoint(coord2D[i], camparam, principlePoint, fx, fy, w, h);
            Ray ray = cam.ScreenPointToRay(undistort_pt);

            RaycastHit hit;
            Vector3 tmp_pos = Vector3.zero;
            float dist = MaxDist_;
            if (Physics.Raycast(ray, out hit)) // Raycast에 충돌이 있으면
            {
                tmp_pos = hit.point;
                dist = (tran.position - tmp_pos).magnitude;
            }

            TotalLoss += dist;

            i++;
        }

        return -TotalLoss / measure_childs.Count;
    }

    float DistanceEntropy(CamParameters camparam)
    {
        return DistanceEntropy(camparam.param, camparam.cam, measures, coord, RT_resolution/2, MaxDist);
    }

    public static float DistanceEntropy(param camparam, Camera cam, List<Transform> measure_childs,
        Dictionary<int, Vector2> coord2D, Vector2 principlePoint, float MaxDist_ = 100, float fx = 0, float fy = 0,
        float w = 0, float h = 0)
    {
        int i = 0;
        float[] distances = new float[measure_childs.Count];
        foreach (var tran in measure_childs)
        {
            Vector2 undistort_pt = Utilities.UndistortedPoint(coord2D[i], camparam, principlePoint, fx, fy,w,h);
            Ray ray = cam.ScreenPointToRay(undistort_pt);

            RaycastHit hit;
            Vector3 tmp_pos = Vector3.zero;
            float dist = MaxDist_;
            if (Physics.Raycast(ray, out hit)) // Raycast에 충돌이 있으면
            {
                tmp_pos = hit.point;
                dist = (tran.position - tmp_pos).magnitude;
            }

            //tmp_pos = camparam.cam.WorldToScreenPoint(tran.position);     // tmp_pos: x, y, depth 픽셀좌표 (origin: left-bottom)
            //dist = (new Vector2(tmp_pos.x,tmp_pos.y) - undistort_pt).magnitude;
            distances[i] = dist;

            i++;
        }

        distances = Utilities.Softmax(distances);
        float entropy = 0;
        foreach (var prob in distances)
        {
            //Debug.Log(prob);
            float numerical_stab = 1;
            entropy -= prob * Mathf.Log10(numerical_stab + prob);
        }

        return entropy;
    }

    // add variance of distance as a loss
    float FeatureLoss(CamParameters camparam)
    {
        Mat source = Utilities.RenderToMat(camparam.rt);
        var sift = SIFT.Create();

        KeyPoint[] keypoints1, keypoints2;
        var descriptors1 = new Mat();   //<float>
        var descriptors2 = new Mat();   //<float>
        sift.DetectAndCompute(source, null, out keypoints1, descriptors1);
        sift.DetectAndCompute(gt_mat, null, out keypoints2, descriptors2);

        var Matcher = new BFMatcher(NormTypes.L2, false);

        DMatch[] matches = Matcher.Match(descriptors1, descriptors2);
        camparam.match_num = matches.Length;

        matches = matches.OrderByDescending(x => x.Distance).ToArray();
        if (matches.Length <= 3)
        {
            return float.MinValue;
        }
        //Debug.Log(matches.Length);
        float min_dist = matches[matches.Length - 1].Distance;
        float max_dist = matches[0].Distance;
        float boundary = (max_dist - min_dist) * FeatureFilterRatio + min_dist;
        List<DMatch> correct_matches = new List<DMatch>();
        for (int i = matches.Length - 1; matches[i].Distance < boundary; i--)
        {
            correct_matches.Add(matches[i]);
        }

        List<double> losses = new List<double>();
        foreach (DMatch match in correct_matches)
        {
            var src_pt = keypoints1[match.QueryIdx].Pt;
            var gt_pt = keypoints2[match.TrainIdx].Pt;

            var dist = src_pt.DistanceTo(gt_pt);
            losses.Add(dist);
        }
        camparam.dist_loss = (float)losses.Average();

        if (camparam.dist_loss < 3)
        {
            is_find_global = true;
            is_find_optimal = true;
        }

        return -camparam.dist_loss * (1 - alpha) + camparam.match_num * alpha;
    }

    void HyperParamScheduling(HyperScheduleMethod method)    // method \in {const, linear, cyclic, exponential}
    {
        //float[] final_hyperparams = new float[5] { 0.015f, 0.02f , 0.015f , 0.0005f , 0.0001f}; // if real
        float[] final_hyperparams = new float[5] { 0.015f, 0.02f, 0.015f, 0, 0 };   // if virtual
        float fit = CamList[CamList.Count - 1].fitness;
        if (method == HyperScheduleMethod.Const) return;
        else if (method == HyperScheduleMethod.Linear)
        {
            if (iter - optimal_period > HPschedule_period)
            {
                num_hyper_update++;

                PosExpandRatio = Mathf.Clamp(PosExpandRatio - (init_PosExpandRatio - final_hyperparams[0]) / 20, final_hyperparams[0], init_PosExpandRatio);
                RotExpandRatio = Mathf.Clamp(RotExpandRatio - (init_RotExpandRatio - final_hyperparams[1]) / 20, final_hyperparams[1], init_RotExpandRatio);
                FovExpandRatio = Mathf.Clamp(FovExpandRatio - (init_FovExpandRatio - final_hyperparams[2]) / 20, final_hyperparams[2], init_FovExpandRatio);
                distort_k_ExpandRatio = Mathf.Clamp(distort_k_ExpandRatio - (init_distort_k_ExpandRatio - final_hyperparams[3]) / 20, final_hyperparams[3], init_distort_k_ExpandRatio);
                distort_p_ExpandRatio = Mathf.Clamp(distort_p_ExpandRatio - (init_distort_p_ExpandRatio - final_hyperparams[4]) / 20, final_hyperparams[4], init_distort_p_ExpandRatio);

                optimal_period = iter;
            }
        }
        else if (method == HyperScheduleMethod.Cyclic)
        {
            if (iter - optimal_period > HPschedule_period)
            {
                num_hyper_update++;
                float cyclic_ratio = 0.5f * Mathf.Cos(Mathf.PI / 2.5f * num_hyper_update) + 0.5f;

                PosExpandRatio = init_PosExpandRatio* cyclic_ratio;
                RotExpandRatio = init_RotExpandRatio * cyclic_ratio;
                FovExpandRatio = init_FovExpandRatio * cyclic_ratio;
                distort_k_ExpandRatio = init_distort_k_ExpandRatio * cyclic_ratio;
                distort_p_ExpandRatio = init_distort_p_ExpandRatio * cyclic_ratio;

                optimal_period = iter;
            }
        }
        else if (method == HyperScheduleMethod.Exponential)
        {
            if (iter - optimal_period > HPschedule_period)
            {
                num_hyper_update++;

                //PosRandomRange /= 2;
                //RotRandomRange /= 2;
                //FovRandomRange /= 2;

                PosExpandRatio /= 2;
                RotExpandRatio /= 2;
                FovExpandRatio /= 2;

                //weight_2D = weight_2D;
                //weight_3D = weight_3D;

                distort_k_ExpandRatio /= 2;
                distort_p_ExpandRatio /= 2;

                optimal_period = iter;
            }
        }


        if (best_fit != fit)
        {
            optimal_period = iter;
            best_fit = fit;
        }

    }

    void CrossOver()
    {
        List<param> tmp_params = new List<param>();
        for (int i = 0; i < num_exchange; i++)
        {
            int parent_1 = CamList.RandomIndexByFitness();
            int parent_2 = CamList.RandomIndexByFitness(parent_1);

            // interpolation
            float pos_inter = UnityEngine.Random.Range(0, 1);
            float rot_inter = UnityEngine.Random.Range(0, 1);
            float fov_inter = UnityEngine.Random.Range(0, 1);
            float distortion_inter = UnityEngine.Random.Range(0, 1);
            param rand_param = Utilities.RandParam(Vector3.one*PosExpandRatio, Vector3.one * RotExpandRatio, FovExpandRatio,
                distort_k_ExpandRatio, distort_k_ExpandRatio, distort_p_ExpandRatio, distort_p_ExpandRatio, distort_k_ExpandRatio);

            var tmp_pos = Vector3.Lerp(CamList[parent_1].param.pose, CamList[parent_2].param.pose, pos_inter);
            tmp_pos += rand_param.pose;

            var tmp_quat = Quaternion.LerpUnclamped(CamList[parent_1].param.quat_rot, CamList[parent_2].param.quat_rot, rot_inter);
            var tmp_rot = tmp_quat.eulerAngles;
            tmp_rot += rand_param.euler_rot;

            var tmp_fov = Utilities.Lerp(CamList[parent_1].param.fov, CamList[parent_2].param.fov,fov_inter) + rand_param.fov;
            var tmp_k1 = Utilities.Lerp(CamList[parent_1].param.k1, CamList[parent_2].param.k1, distortion_inter) + rand_param.k1;
            var tmp_k2 = Utilities.Lerp(CamList[parent_1].param.k2, CamList[parent_2].param.k2, distortion_inter) + rand_param.k2;
            var tmp_p1 = Utilities.Lerp(CamList[parent_1].param.p1, CamList[parent_2].param.p1, distortion_inter) + rand_param.p1;
            var tmp_p2 = Utilities.Lerp(CamList[parent_1].param.p2, CamList[parent_2].param.p2, distortion_inter) + rand_param.p2;
            var tmp_k3 = Utilities.Lerp(CamList[parent_1].param.k3, CamList[parent_2].param.k3, distortion_inter) + rand_param.k3;

            var tmp_param = mutation(tmp_pos, tmp_rot, tmp_fov);
            tmp_param.k1 = tmp_k1; tmp_param.k2 = tmp_k2; tmp_param.p1 = tmp_p1; tmp_param.p2 = tmp_p2; tmp_param.k3 = tmp_k3;
            tmp_params.Add(tmp_param);
        }

        for (int i = 0; i < num_exchange; i++)
        {
            CamList[i].SetParams(tmp_params[i], true);
        }
    }

    bool CheckOverfit(float threshold)
    {
        float[] fits = new float[CamList.Count];
        for (int i = 0; i < CamList.Count; i++) fits[i] = CamList[i].fitness;

        float[] vars = new float[fits.Length];
        for (int i = 0; i < fits.Length; i++) vars[i] = Mathf.Pow(fits[i] - fits.Average(), 2);

        if (Mathf.Sqrt(vars.Average()) < threshold) return true;

        return false;
    }

    param mutation(Vector3 pose, Vector3 rotation, float fov)
    {
        param param = new param();
        param.pose = pose;
        param.euler_rot = rotation;
        param.fov = fov;

        var pos_range = PosRandomRange;
        var rot_range = RotRandomRange;
        var fov_range = FovRandomRange;

        float q = UnityEngine.Random.Range(0f, 1f);
        if (q <= child_mutation_prob)
        {
            //if (CamList[CamList.Count - 1].dist_loss < 200)
            //{
            //    pos_range /= 2;
            //    rot_range /= 2;
            //    fov_range /= 2;
            //}
            param rand_param;
            rand_param.pose = new Vector3(0, 0, 0);
            rand_param.euler_rot = new Vector3(0, 0, 0);
            rand_param.fov = 0;
            float p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(new Vector3(pos_range, 0, 0), Vector3.zero, 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(new Vector3(0, pos_range, 0), Vector3.zero, 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(new Vector3(0, 0, pos_range), Vector3.zero, 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, new Vector3(rot_range, 0, 0), 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, new Vector3(0, rot_range, 0), 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, new Vector3(0, 0, rot_range), 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, Vector3.zero, fov_range);
            }
            param.pose += rand_param.pose;
            param.euler_rot += rand_param.euler_rot;
            param.fov += rand_param.fov;
        }
        return param;
    }

    //private void Record(string record, bool create = false)
    //{
    //    string txt_path = Experiment_path + SceneManager.GetActiveScene().name + "/Errors/info.txt";
    //    StreamWriter writer;
    //    if (create)
    //    {
    //        writer = File.CreateText(txt_path);
    //    }
    //    else
    //    {
    //        writer = File.AppendText(txt_path);
    //    }
    //    writer.Write(record);
    //    writer.Close();
    //}
    public Dictionary<string, float> Evaluation(bool is_virtual, param Comp_param=new param())
    {
        Dictionary<string, float> metrics = new Dictionary<string, float>();
        if (iter % 25 == 0 || iter == 1)
        {
            metrics["iteration"] = iter;
            var best_cam = GetBestCam();

            metrics["f_img"] = best_cam.f_img;
            metrics["f_DT"] = best_cam.f_DT;
            metrics["f_entropy"] = best_cam.f_entropy;
            metrics["fit"] = best_cam.fitness;

            metrics["pos_error"] = 0; metrics["angle_error"] = 0; metrics["fov_error"] = 0;
            if (is_virtual)
            {
                metrics["pos_error"] = (best_cam.param.pose - Comp_param.pose).magnitude;
                metrics["angle_error"] = Quaternion.Angle(best_cam.param.quat_rot, Comp_param.quat_rot);  // degree
                metrics["fov_error"] = Mathf.Abs(best_cam.param.fov - Comp_param.fov);
            }
        }
        return metrics;
    }

    IEnumerator optimize()
    {
        episode++;
        while (iter < num_iteration)
        {
            if (!is_pause)
            {
                iter++;
                SortByFitness();
                HyperParamScheduling(hyper_method);
                CrossOver();
            }
            else
            {
                //CoordLoss(CamList[0]);
                //Debug.Log(CamList[0].cam_tf.name);
            }

            if (is_find_optimal)
            {
                break;
            }
            //yield return new WaitForSeconds(0.5f);
            yield return null;
        }
        if (local_minimum != null)
            CamList.Add(local_minimum);

        //if (is_evaluate)
        //{
        //    string info = "finished at " + iter.ToString() + "th iteration, " + episode.ToString() + "th episode with " + local_min_change_num.ToString() + " local minimum change";
        //    CamList.SaveInformation(info, "Assets/CamOptimizer/Test/Runtime/Resources/Experiments/20240315_cam_optim_result_episode_" + episode.ToString() + ".txt");
        //}

        if (local_minimum != null)
            CamList.Remove(local_minimum);

        if (iter > num_iteration) is_find_global = true;
    }

    private void OnDestroy()
    {
        Cv2.DestroyAllWindows();
    }

}