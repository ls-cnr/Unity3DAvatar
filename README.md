# UnityAvatar3D: Multi Modal Engagement
## Overview
The project's goal is to build a system that allows the 3D Avatar to look at a specific user that is targeted as the "engaged" user. The targeting logic is managed through a Behavior Tree (BT). The project is built in `Unity 6.3 LTS (6000.3.9f1)` and the models are prepared in `Python 3.13.9`.

## Instructions
### ML Models
To reproduce the demo it is mandatory to prepare the used ML models. The models need to be converted to the [ONNX](https://onnx.ai/) format. The links to follow in order to get the models are in the table below:
| Face Detection | Head Pose Estimation | Active Speaker Detection |
|----------------|----------------------|--------------------|
| [Ultraface](https://github.com/onnx/models/tree/main/validated/vision/body_analysis/ultraface) | [6DRepNet](https://github.com/thohemp/6DRepNet) | [LR-ASD](https://github.com/Junhua-Liao/LR-ASD) |

Before continuing, download or clone this repository, creating a directory on your computer.
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
This is the easiest model to prepare. Open the link of the relevant repository given in the table above, navigate to the `models` folder inside the opened repository and download the `version-RFB-640.onnx` file. Rename it `ultraface.onnx` and put it into the `UnityProject/Assets/StreamingAssets/Models` directory.
#### 2. 6DRepNet
To prepare this model you need to get some files from the relevant repository in the table above. Particularly, navigate to the `sixdrepnet` folder and download:
- `model.py`
- `utils.py`

Put these two files into `ModelsSetup/models/sixdrepnet`.

Then, inside the model's repository, navigate to the `backbone` folder and download:
- `repvgg.py`
- `se_block.py`

Put these two files into `ModelsSetup/models/sixdrepnet/backbone`.

Also, follow the instructions given in the relevant repository's own instructions to download the weights (fine-tuned models). The file you need to download is the one named `6DRepNet_300W_LP_AFLW2000.pth`. Put it into the `ModelsSetup/models/sixdrepnet/weights` directory.

Once all this is done, if you have deactivated it, reactivate the Python venv we created, navigate to the `ModelsSetup` directory, and use the following command to convert the model to the needed ONNX format: `python -m convertions.convertsixdrepnet`

Now move the created `6drepnet_dynamic.onnx` file from `ModelsSetup/models/sixdrepnet` to the `UnityProject/Assets/StreamingAssets/Models` directory.

#### 3. LR-ASD
To prepare this model you need to get some files from the relevant repository in the table above. Particularly, navigate to the `model` folder and download the following files:
- `Model.py`
- `Encoder.py`
- `Classifier.py`

Put the `Model.py` file into the `ModelsSetup/models/lrasd` directory. Put the other two into the `ModelsSetup/models/lrasd/model` directory.

Also, go back to the root of the relevant repository, navigate to the `weight` folder and download the `pretrain_AVA.model` file. Put the file into the `ModelsSetup/models/lrasd/weights` directory.

Once all this is done, if you have deactivated it, reactivate the Python venv we created, navigate to the `ModelsSetup` directory, and use the following command to convert the model to the needed ONNX format: `python -m convertions.convertlrasd`

Now move the created `lrasd.onnx` file from `ModelsSetup/models/lrasd` to the `UnityProject/Assets/StreamingAssets/Models` directory.

### Unity Project
In order to open the Unity project, open Unity Hub and choose `add project from disk`. Here, select the `UnityProject` directory as the project's directory. Once this is done, select or install the correct Unity Editor version (`6000.3.9f1`) and launch the project.

When launching the project for the first time, Unity might prompt you saying there are errors. Click `ignore` and let the project open. Once that's done, install [NuGet For Unity](https://github.com/GlitchEnzo/NuGetForUnity/releases/tag/v4.5.0), available at the linked repository in the `Releases` section (the link takes you directly there at the 4.5.0 version release). Use the `.unitypackage` file to install the correct version for your OS. IMPORTANT: the installation needs to be done with the project open.

Once NuGet For Unity is installed, navigate to the newly added NuGet menu inside Unity's interface and select `manage nuget packages`. If the menu does not appear, reload the project using the `Reimport All` command inside the `Assets` menu. Install the following packages with the indicated version:
- MathNet.Numerics, version 5.0.0
- SkiaSharp, version 3.119.2
- OpenCvSharp4, version 4.11.0.20250507. Also install your platform specific runtime (OpenCvSharp4.runtime) package with the same version
- Microsoft.ML.OnnxRuntime.Gpu, version 1.23.2

With all these packages installed, all the errors should be resolved. If needed, reload the project. If SkiaSharp launches a red error, do not worry, the project will still work perfectly fine.

Now, to finish the setup, use the `Project` sub-interface inside the Unity Editor to navigate to the `Assets/Scenes` folder and double click on the `newScene` file to load the scene. After loading, you will be able to Play the demo. We recommend using scene view and putting side by side the Canvas in the 3D environment with the AvatarCamera through the cameras tool for the best experience.

All the modifiable values for the MonoBehaviour scripts are easily understood through the use of the Unity Inspector on the objects they're attached to. Also, the scripts are extensively commented to make them easy to understand.