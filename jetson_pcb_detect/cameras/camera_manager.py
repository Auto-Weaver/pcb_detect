import cv2
import threading
import time
from utils.logger_manager import LoggerManager
from .daheng_sdk_camera import DahengCamera




class CameraManager:
    def __init__(self, camera_configs, frame_queues, stop_event, img_paths=None):
        """
        camera_configs: dict, 相机配置字典,键为相机ID,值为配置信息
        frame_queues:   dict, 队列字典,用于存放cam_01, cam_02捕获的帧
        stop_event:     threading.Event,用于停止线程
        img_paths:      list,模拟相机的图片路径
        """
        self.camera_configs = camera_configs 
        self.camera_ids = list(camera_configs.keys())     # "jetson_01_cam_01", "jetson_01_cam_02"
        
        self.frame_queues = frame_queues  # "jetson_01_cam_01", "jetson_01_cam_02"各自的队列
        self.stop_event = stop_event
        self.img_paths = img_paths or []
        self.caps = []  # 原来的结构保留
        self.daheng_camera = DahengCamera(camera_configs, stop_event) if not self.img_paths else None

    def start(self):
        if self.img_paths:
            # 如果有图片路径，则按路径模拟摄像头
            for cam_id in self.camera_ids:
                t = threading.Thread(target=self._simulate_capture_loop, args=(cam_id,), daemon=True)
                t.start()
            LoggerManager.log("Camera", "Simulated cameras started")
        else:
            # 真实相机
            if self.daheng_camera:
                self.daheng_camera.start()
                # 每个相机一个线程，有图时放入frame_queues[cam_id]对应的队列
                for cam_id in self.camera_ids:
                    t = threading.Thread(
                    target=self._capture_loop, 
                    args=(cam_id,),
                    daemon=True
                    )
                    t.start()

    def _simulate_capture_loop(self, cam_id):
        """
        模拟摄像头循环读取图片
        """
        if not self.img_paths:
            LoggerManager.log("Camera", f"No images provided for Camera {cam_id}")
            return
        
        count = 0
        max_images = 3
        while not self.stop_event.is_set():
            for img_path in self.img_paths:
                if self.stop_event.is_set():
                    break
                if count >= max_images:
                    break
                frame = cv2.imread(img_path)
                if frame is None:
                    LoggerManager.log("Camera", f"Failed to read image {img_path} for Camera {cam_id}")
                    continue
                if not self.frame_queues[cam_id].full():
                    self.frame_queues[cam_id].put({
                        "camera_id": cam_id,
                        "frame": frame,
                        "count": count,
                        "timestamp": time.time()
                    })
                    count += 1
                time.sleep(0.1)  # 模拟摄像头帧率约10fps

    def _capture_loop(self, cam_id):
        while not self.stop_event.is_set():
            task = self.daheng_camera.get_frame(cam_id)
            if task is None:
                continue
            # 硬触发获取的图片放入cam_id对应的frame_queue
            self.frame_queues[cam_id].put(task)

    def stop(self):
        for _, cap in self.caps:
            cap.release()
        LoggerManager.log("Camera", "Cameras stopped")
        
    def get_camera_status(self):
        status = {}
        if self.img_paths:
            # 模拟相机
            status = {}
            for cam_id in self.camera_ids:
                status[cam_id] = "simulated"
            return status
        if self.daheng_camera:
            return self.daheng_camera.get_camera_status()
        return {}




# # 测试队列功能
# import queue
# if __name__ == "__main__":
#     q = queue.Queue() #FIFO（先进先出）
#     # q = queue.LifoQueue()  # 后进先出队列
#     q.put(123)                # 整数
#     q.put("hello")             # 字符串
#     q.put([1, 2, 3])           # 列表
#     q.put({"a": 1, "b": 2})    # 字典
    
#     while not q.empty():
#         item = q.get()
#         print(f"Got item: {item} of type {type(item)}")


#测试读取文件保存到队列
import cv2
import queue  
if __name__ == "__main__":

    # 测试图片
    img_paths = [
        "img/cam0_1770025671921.jpg",
        "img/cam0_1770025672111.jpg",
        "img/cam0_1770025672295.jpg",
        # ...
    ]
    
    camera_configs = {
        "cam_01": {"sn": "GCN25090273", "exposure": 30000},
        "cam_02": {"sn": "GCN25070204", "exposure": 30000}
        }
    
    stop_event = threading.Event()
    frame_queue = queue.Queue(maxsize=20)
    camera =  CameraManager(
        camera_configs=camera_configs,   
        frame_queue=frame_queue,
        stop_event=stop_event,
        img_paths=""
        )
    camera.start()
    print("Camera started. Reading frames...")
    
    from PIL import Image           
    try:
        while True:
            try:
                task = frame_queue.get(timeout=0.5)
            except queue.Empty:
                continue

            cam_id = task["camera_id"]
            frame = task["frame"]
            timestamp = task["timestamp"]
            # frame.show()
            # 缩放到 400x800
            display_frame = cv2.resize(frame, (800, 400))  # 宽=800, 高=400

            cv2.imshow(f"{cam_id}", display_frame)
            print(f"Got frame from {cam_id} at {timestamp}, shape: {frame.shape}")

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    finally:
        stop_event.set()
        cv2.destroyAllWindows()
        print("Camera stopped.")



