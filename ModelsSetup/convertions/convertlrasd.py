import torch
import torch.nn as nn
import torch.nn.functional as F
from collections import OrderedDict
import sys
from pathlib import Path
from contextlib import contextmanager
import types

def exportLR(device): 
    """Convert LR-ASD from PyTorch to ONNX."""

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
    lrasd_dir = Path(__file__).resolve().parent.parent / "models" / "lrasd"
    with temporary_path(lrasd_dir):
        from models.lrasd.Model import ASD_Model

        class FullLRASD(nn.Module):
            #full class that wraps the model adding the final linear layer 
            def __init__(self): 
                super(FullLRASD, self).__init__() 
                self.backbone = ASD_Model() 
                self.fcAV = nn.Linear(128, 2)  #you only need the fused layer to execute the inference
        
            def forward(self, audioFeature, visualFeature): 
                outsAV, _ = self.backbone(audioFeature, visualFeature) 
                
                #linear layer logic
                xAV = self.fcAV(outsAV) 
                probsAV = F.softmax(xAV, dim=-1)[:, 1] 
                
                return probsAV 
        # import the model
        model = FullLRASD()

    # ============================================================
    # PATCH: Replace MaxPool3d with MaxPool2d in audioEncoder
    # ============================================================
    audio_enc = model.backbone.audioEncoder
    
    # Replace the layer instances
    audio_enc.pool1 = nn.MaxPool2d(kernel_size=(1, 3), stride=(1, 2), padding=(0, 1))
    audio_enc.pool2 = nn.MaxPool2d(kernel_size=(1, 3), stride=(1, 2), padding=(0, 1))
    
    # Monkey-patch the forward method to use the new 2D pools
    def patched_forward(self, x):
        x = self.block1(x)
        x = self.pool1(x)   # Now MaxPool2d
        x = self.block2(x)
        x = self.pool2(x)   # Now MaxPool2d
        x = self.block3(x)
        x = torch.mean(x, dim=2, keepdim=True)
        x = x.squeeze(2).transpose(1, 2)
        return x
    
    audio_enc.forward = types.MethodType(patched_forward, audio_enc)
    # ============================================================

    # load weights 
    u_state_dict = torch.load("models/lrasd/weights/pretrain_AVA.model", map_location=device) 
 
    backbone_state = {} 
    fcAV_state = {} 
 
    for k, v in u_state_dict.items(): 
        # weights to be put into the backbone of our full class 
        if k.startswith('model.'): 
            name = k.replace('model.', '')  # visualEncoder..., GRU..., etc...
            backbone_state[name] = v 
        # linear layer weights. no need to keep the name. will assign directly
        elif k.startswith('lossAV.FC.'): 
            name = k.replace('lossAV.FC.', '')  # weight, bias 
            fcAV_state[name] = v 
 
    # load the weights directly in the part of the model they belong to
    model.backbone.load_state_dict(backbone_state, strict=False) 
    model.fcAV.load_state_dict(fcAV_state, strict=True) 
    model.to(device) 
    model.eval() 
 
    # dummy input creation for export command
    dummy_video = torch.randn(1, 25, 112, 112).to(device)  # [N, T, H, W] 
    dummy_audio = torch.randn(1, 100, 13).to(device)        # [N, T*4, 13] 
    dummy_input = (dummy_audio, dummy_video) 
 
    # export the model 
    torch.onnx.export( 
        model, 
        dummy_input, 
        "models/lrasd/lrasd.onnx", 
        input_names=['audioFeature', 'visualFeature'], 
        output_names=['probsAV'],
        export_params=True, 
        opset_version=11, 
        do_constant_folding=True, 
        verbose=False, 
        dynamo = False 
    ) 
 
    print('LR-ASD model saved')

if __name__ == "__main__":
    device = torch.device('cpu') #you could use the gpu, but performance does not change much for this task
    exportLR(device)