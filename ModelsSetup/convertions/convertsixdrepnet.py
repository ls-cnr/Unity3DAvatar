import torch
import sys
from pathlib import Path
from contextlib import contextmanager

def convertSixDRepnetVariable(device):
    """This function converst the 6DRepNet model from PyTorch
    to ONNX format. The model is converted with variable batch size
    """

    #manager to solve import path submodules problems
    @contextmanager
    def temporary_path(path):
        path_str = str(path)
        sys.path.insert(0, path_str)
        try:
            yield
        finally:
            sys.path.remove(path_str)

    # Usage of manager
    sixdrepnet_dir = Path(__file__).resolve().parent.parent / "models" / "sixdrepnet"
    with temporary_path(sixdrepnet_dir):
        from models.sixdrepnet.model import SixDRepNet

    # import the pytorch model
    model = SixDRepNet(backbone_name='RepVGG-B1g2', backbone_file='', deploy=True, pretrained=False)

    # load the weights of the pretrained network
    saved_state_dict = torch.load(
        "models/sixdrepnet/weights/6DRepNet_300W_LP_AFLW2000.pth", 
        map_location='cpu'
    )
    model.load_state_dict(saved_state_dict)
    model.to(device)
    model.eval()

    # create dummy input for export command
    dummy_input = torch.randn((1, 3, 224, 224))

    # define dynamix axes
    dynamic_axes = {
        'input': {0: 'batch_size'},
        'OUT': {0: 'batch_size'}
    }

    # export the model
    torch.onnx.export(
        model, 
        dummy_input, 
        "models/sixdrepnet/6drepnet_dynamic.onnx",
        input_names=['input'], 
        output_names=['OUT'],
        export_params=True,
        dynamic_axes=dynamic_axes,
        dynamo = False,
        opset_version=11 #this version should ensure better compatibility with legacy "dynamo = false"
    )
    
    print('6drepnet model successfully converted')

if __name__ == "__main__":
    device = torch.device('cpu') #you could use the gpu, but performance does not change much for this task
    convertSixDRepnetVariable(device)