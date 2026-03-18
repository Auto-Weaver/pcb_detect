from utils.logger_manager import LoggerManager
from algoso_wrapper.pcb_algo_ctypes import PCBAlgoCtypes

class AlgosoWrapper:
    def __init__(self, so_path=None):
        self.backend = PCBAlgoCtypes(so_path)
        LoggerManager.log("algoso", f"Loaded: {so_path}")

    def create_template(self, image, save_path, scale_min=0.5, scale_max=1.1):
        LoggerManager.log("algoso", "Create template called")
        ret, status, score = self.backend.create_template(
            image, save_path, scale_min, scale_max
        )
        return {
            "ok": ret == 0 and status == 0,
            "score": score
        }
        

    def match_template(self, image):
        LoggerManager.log("algoso", "Match template called")
        return {
            "matched": True,
            "x": 100,
            "y": 120,
            "angle": 0.05,
            "score": 0.92
        }