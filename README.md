# Unity3DAvatar: Multi Modal Engagement

> Please also check out our paper: [Stefano Monfalcone, Gabriele Ragusa, Giovanna Runfola, Irene Maria Genovese, Agnese Augello, Silvia Franchini and Luca Sabatucci, **From Perception to Social Behaviour: An Adaptive Engagement Pipeline for 3D Avatars**, *Proceedings of the ReDH - Rethinking the Future of Digital Humanities through Ethics and Artificial Consciousness co-located with the 24th International Conference of the Italian Association for Artificial Intelligence (AIxIA 2026) Perugia, Italy, October 8, 2026.*](https://ceur-ws.org/Vol-4314/ReDH3.pdf)
## Overview

The project's goal is to build a system that allows a 3D Avatar to look at a specific user that is targeted as the "engaged" user. The targeting logic is managed through a Behavior Tree (BT). The following software is needed to replicate the project on your machine:

- Unity: `6.3 LTS (6000.3.9f1)`
- Python: `3.13.9`
- CUDA: `12.9`
- CUDNN: `9.20`
- NuGetForUnity: `4.5.0`

The following instructions will guide you in setting everything up accordingly.
In systems running MacOS the project will fallback to using the CPU to run the neural networks.

## Instructions
### ML Models
To reproduce the demo it is mandatory to prepare the used ML models. The models need to be converted to the ONNX format. Instructions to do that are given below.

**Before continuing, download or clone this repository, creating a directory on your computer.**
>If at any point during the execution of the instructions a given directory does not exist, create it.

Inside the created directory's root, make a Python venv and install the requirements:
- create the venv: `py -m venv .venv` or `python3 -m venv .venv`
- activate the venv:
  - windows powershell: `.venv/Scripts/activate.ps1`
  - windows cmd: `.venv/Scripts/activate.bat`
  - linux: `source .venv/bin/activate`
  - macos: `source .venv/bin/activate`
- move into the `ModelsSetup` folder: `cd ModelsSetup`
- install the requirements: `pip install -r requirements.txt`

Once this is done, you can proceed with the following instructions.
#### 1. Ultraface
This is the easiest model to prepare. Open the [link](https://github.com/onnx/models/tree/main/validated/vision/body_analysis/ultraface/models) and download the `version-RFB-640.onnx` file. Rename it `ultraface.onnx` and put it into the `UnityProject/Assets/StreamingAssets/Models` directory.
#### 2. 6DRepNet
Open this [link](https://github.com/thohemp/6DRepNet/tree/master/sixdrepnet), and download the following files:
- `model.py`
- `utils.py`

Put these two files into `ModelsSetup/models/sixdrepnet`.

Then, open this [link](https://github.com/thohemp/6DRepNet/tree/master/sixdrepnet/backbone) and download the following files:
- `repvgg.py`
- `se_block.py`

Put these two files into `ModelsSetup/models/sixdrepnet/backbone`.

Finally, open this [link](https://github.com/thohemp/6DRepNet/tree/master), scroll till you find the `fine-tuned models` section of the repository's readme, follow the given link and download the file named `6DRepNet_300W_LP_AFLW2000.pth`. Put it into the `ModelsSetup/models/sixdrepnet/weights` directory.

Once all this is done, if you have deactivated it, reactivate the Python venv we created, navigate to the `ModelsSetup` directory, and use the following command to convert the model to the needed ONNX format: `python -m convertions.convertsixdrepnet`

Now move the created `6drepnet_dynamic.onnx` file from `ModelsSetup/models/sixdrepnet` to the `UnityProject/Assets/StreamingAssets/Models` directory.

#### 3. LR-ASD
Open this [link](https://github.com/Junhua-Liao/LR-ASD/tree/main/model) and download the following files:
- `Model.py`
- `Encoder.py`
- `Classifier.py`

Put the `Model.py` file into the `ModelsSetup/models/lrasd` directory. Put the other two into the `ModelsSetup/models/lrasd/model` directory.

Also, open this [link](https://github.com/Junhua-Liao/LR-ASD/tree/main/weight) and download the `pretrain_AVA.model` file. Put the file into the `ModelsSetup/models/lrasd/weights` directory.

Once all this is done, if you have deactivated it, reactivate the Python venv we created, navigate to the `ModelsSetup` directory, and use the following command to convert the model to the needed ONNX format: `python -m convertions.convertlrasd`

Now move the created `lrasd.onnx` file from `ModelsSetup/models/lrasd` to the `UnityProject/Assets/StreamingAssets/Models` directory.

### Unity Project
In order to open the Unity project, open Unity Hub and choose `add project from disk`. Here, select the `UnityProject` directory as the project's directory. Once this is done, select or install the Unity Editor `6000.3.9f1` version and launch the project.

When launching the project for the first time, Unity might prompt you saying there are errors. Click `ignore` and let the project open. Once that's done, install [NuGet For Unity version 4.5.0](https://github.com/GlitchEnzo/NuGetForUnity/releases/tag/v4.5.0). Use the `.unitypackage` file to install the correct version for your OS. IMPORTANT: the installation needs to be done with the project open.

Once NuGet For Unity is installed, navigate to the newly added NuGet menu inside Unity's interface and select `manage nuget packages`. If the menu does not appear, reload the project using the `Reimport All` command inside the `Assets` menu. Install the following packages with the indicated version:
- **MathNet.Numerics**, `version 5.0.0`
- **SkiaSharp**, `version 3.119.2`
- **OpenCvSharp4**, `version 4.11.0.20250507`. Also install your platform specific runtime (**OpenCvSharp4.runtime**) package with the `same version`
- **Microsoft.ML.OnnxRuntime.Gpu**, `version 1.23.2`

With all these packages installed, all the errors should be resolved. If needed, reload the project. If SkiaSharp launches a red error, do not worry, the project will still work perfectly fine.

Now, to finish the setup, use the `Project` sub-interface inside the Unity Editor to navigate to the `Assets/Scenes` folder and double click on the `newScene` file to load the scene. After loading, you will be able to Play the demo. We recommend using scene view and putting side by side the Canvas in the 3D environment with the AvatarCamera through the cameras tool for the best experience.

All the modifiable values for the MonoBehaviour scripts are easily understood through the use of the Unity Inspector on the objects they're attached to. Also, the scripts are extensively commented to make them easy to understand.

## References

In building this project we've referenced the following ones:
- [python_speech_features](https://github.com/jameslyons/python_speech_features), for the utilities needed to use the ASD model;
- [ONNX](https://onnx.ai/), for the Open Neural Network Exchange format to use the models on any platform;
- [NuGet For Unity](https://github.com/GlitchEnzo/NuGetForUnity), to use the needed NuGet packages inside Unity.
- [ONNX Model Zoo](https://github.com/onnx/models/tree/main), for the pre-converted Ultraface model.
- [6DRepNet](https://github.com/thohemp/6DRepNet), for the head pose estimation model.
- [LR-ASD](https://github.com/Junhua-Liao/LR-ASD), for the active speaker detection model.

*clicking on a project's name will take you to the project's own site/repository*
