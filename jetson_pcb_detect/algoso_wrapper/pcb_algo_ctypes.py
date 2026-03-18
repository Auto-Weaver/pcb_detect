import ctypes
import numpy as np


# 定义结构体
class CreateModelInput(ctypes.Structure):
    _fields_ = [
        ("image", ctypes.POINTER(ctypes.c_ubyte)),
        ("width", ctypes.c_int),
        ("height", ctypes.c_int),
        ("channels", ctypes.c_int),
        ("scale_min", ctypes.c_double),
        ("scale_max", ctypes.c_double),
        ("save_path", ctypes.c_char_p),
        ("reserved", ctypes.c_int * 8),
    ]
    
class CreateModelOutput(ctypes.Structure):
    _fields_ = [
        ("status", ctypes.c_int),
        ("model_score", ctypes.c_double),
    ]


class PCBAlgoCtypes:
    def __init__(self, so_path):
        self.lib = ctypes.cdll.LoadLibrary(so_path)
        self._bind_funcs()


    def _bind_funcs(self):
        # 这里可以绑定更多函数，例如 match_template
        self.lib.create_template.argtypes = [
            ctypes.POINTER(CreateModelInput),
            ctypes.POINTER(CreateModelOutput),
        ]
        self.lib.create_template.restype = ctypes.c_int


    def create_template(self, img: np.ndarray, save_path: str, scale_min=0.5, scale_max=1.1):
        img = np.ascontiguousarray(img, dtype=np.uint8)
        h, w, c = img.shape
        
        input_data = CreateModelInput()
        input_data.image = img.ctypes.data_as(ctypes.POINTER(ctypes.c_ubyte))
        input_data.width = w
        input_data.height = h
        input_data.channels = c
        input_data.scale_min = scale_min
        input_data.scale_max = scale_max
        input_data.save_path = save_path.encode("utf-8")
        for i in range(8):
            input_data.reserved[i] = 0

        output_data = CreateModelOutput()
        
        ret = self.lib.create_template(ctypes.byref(input_data), ctypes.byref(output_data))
        return ret, output_data.status, output_data.model_score
