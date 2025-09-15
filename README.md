# GADT
GADT is a camera calibration tool utilizing digital twin based on genetic algorithm. Below figures show the motivation and calibration results. To install this package, follow requirments setup. Using with your custom digital twin environment is also available. If you need tutorial of the package, follow Demo section.
<p align="center">
  <img src="./uploads/Gongeoptap-BEV-Raw.png" width="33%" />
  <img src="./uploads/Gongeoptap-BEV-Ours-DT.png" width="33%" />
  <img src="./uploads/Gongeoptap-BEV-Ours-overlay.png" width="33%" />
</p>

## Requirements Setup

### Install Unity

### Install packages assets
Install OpenCv plus Unity package from asset store

Install TextMesh Pro package

### Install Plugins
Ookii.Dialogs.dll and System.Windows.Forms.dll for window explorer

opencv_videoio_ffmpeg4100_64.dll for video uploader

## Custom Environments
1. **Duplicate Scene**: Copy `Gongeoptap` scene, rename it, and add it to **Build Settings**.
2. **Add Selection Button**: Change scene to `SceneSelect`, duplicate `Canvas/Gongeoptap` button in the hierarchy, rename and reposition it.
3. **Set OnClick Event**: For the new button from step 2, update **OnClick() → SelectEnv()** function argument to new scene name (from step 1).
4. **Import Assets**: Create a folder under **Environments**, add your digital twin model.
5. **Replace Model**: In the new scene from step 1, remove the old model and add the new digital twin model.
6. **Adjust Settings**: Configure colliders, reposition `RenderCam` object,
7. **Update Control Parameters**: In `User Interface` component from `Canvas` object, adjust control parameters.

## Demo
For demo, we provide the part of a digital twin environment, Gongeoptap DT. And the keypoints for that environment (i.e. dataset $\mathcal{D}$ in the paper) is also provided.
1. Play the game in `SceneSelect` scene
2. Click `run1` and `Next` button
3. Change `Const` strategy to `Exponential` (You can leave the other settings)
4. Click `Optimize` button and start the calibration
5. Click `Export` button to save camera parameters of certain step (saved in Demo directory)

## Cite
