using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;

public class Experiments
{
    private string exp_path;
    private string exp_name;
    StringBuilder csvContent = new StringBuilder();

    public param gt_param;

    public Experiments(string experiment_name)
    {
        exp_name = experiment_name;
        exp_path = Application.persistentDataPath+"/"+ exp_name + ".csv";
        csvContent = new StringBuilder();
        csvContent.AppendLine("iteration,pos error,angle error, fov error,f_img,f_DT,f_entropy");
        File.WriteAllText(exp_path, csvContent.ToString(), Encoding.UTF8);
        gt_param = ControlManager.gt_cam;

    }
    public Experiments(string exp_folder_, string experiment_name)
    {
        exp_name = experiment_name;
        exp_path = exp_folder_ + "/" + exp_name + ".csv";
        csvContent = new StringBuilder();
        csvContent.AppendLine("iteration,pos error,angle error,fov error,f_img,f_DT,f_entropy");
        File.WriteAllText(exp_path, csvContent.ToString(), Encoding.UTF8);
        gt_param = ControlManager.gt_cam;
    }

    public void save_experients(Dictionary<string, float> metrics)
    {
        string line = metrics["iteration"]
            + "," + metrics["pos_error"] + "," + metrics["angle_error"] + "," + metrics["fov_error"]
            + "," + metrics["f_img"] + "," + metrics["f_DT"] + "," + metrics["f_entropy"];
        csvContent.AppendLine(line);
        File.WriteAllText(exp_path, csvContent.ToString(), Encoding.UTF8);
    }

    public void evaluation(GA_optimizer optim, bool is_virtual)
    {
        var metrics = optim.Evaluation(is_virtual, gt_param);
        if (metrics.Count != 0)
        {
            save_experients(metrics);
        }
    }
}
