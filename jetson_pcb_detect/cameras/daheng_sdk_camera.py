import threading
import time
import queue
from distro import name
import gxipy as gx
from utils.logger_manager import LoggerManager
# from PIL import Image
import cv2

def daheng_capture_callback(raw_image, cam_id, image_queues):
    print("Frame ID: %d   Height: %d   Width: %d"
          % (raw_image.get_frame_id(), raw_image.get_height(), raw_image.get_width()))

    rgb_image = raw_image.convert("RGB")
    if rgb_image is None:
        print('Failed to convert RawImage to RGBImage')
        return
    
    
    # 模拟测试图片
    img_path = "img/pcb.png"
    numpy_image = cv2.imread(img_path)
    
    
    # numpy_image = rgb_image.get_numpy_array()
    # if numpy_image is None:
    #     print('Failed to get numpy array from RGBImage')
    #     return

    # img = Image.fromarray(numpy_image, 'RGB')

    # 入队
    q = image_queues[cam_id]
    if q.full():
        q.get_nowait()
        

    q.put({
        "camera_id": cam_id,
        "frame": numpy_image,
        "timestamp": time.time()
    })


class DahengCamera:
    def __init__(self, camera_configs, stop_event, max_queue_size=20):
        self.camera_configs  = camera_configs
        self.camera_ids = list(camera_configs.keys())
        self.stop_event = stop_event  
        # 相机状态
        self.camera_status = {
             cam_id: "init"
             for cam_id in self.camera_ids
        }
        
        # 每个相机一个图像队列，供回调函数放入捕获的帧，供 CameraManager / Processor 获取
        self.image_queues = {
            cam_id: queue.Queue(maxsize=max_queue_size)
            for cam_id in self.camera_ids
        }
        # 相机factory
        self.device_manager = gx.DeviceManager()
        # 统一管理线程，方便后续扩展（比如stop、join）
        self.threads = []

    def start(self):
        # 获取相机列表并启动每个相机的采集线程
        dev_num, dev_info_list = self.device_manager.update_device_list()        
        if dev_num == 0:
            LoggerManager.log("DahengCamera", "No cameras detected")
            for cam_id in self.camera_ids:
                self.camera_status[cam_id] = "disconnected"
            return 
        
        self.device_map = {
            dev["sn"]: dev
            for dev in dev_info_list
        }
        
        for cam_id, cfg in self.camera_configs.items():
            sn = cfg["sn"]
            if sn not in self.device_map:
                LoggerManager.log("DahengCamera", f"{cam_id} SN {sn} not found")
                self.camera_status[cam_id] = "disconnected"
                continue
            
            t = threading.Thread(
                target=self._init_and_run_camera,
                args=(cam_id,cfg),
                daemon=True
            )
            t.start()
            self.threads.append(t)
        LoggerManager.log("DahengCamera", "SDK cameras started")


    def _init_and_run_camera(self, cam_id, cfg):
        """
        每个相机一个线程 + SDK 回调
        """
        # TODO: SDK 打开相机 
        sn = cfg["sn"]
        self.camera_status[cam_id] = "opening"
        # print(f"Opening camera {cam_id} with SN {sn}")
        try:
            cam = self.device_manager.open_device_by_sn(sn)
            self.camera_status[cam_id] = "connected"
            # cam.UserSetSelector.set(gx.GxUserSetEntry.USER_SET0)
            # cam.UserSetLoad.send_command()
        except Exception as e:
            self.camera_status[cam_id] = "disconnected"
            LoggerManager.log("DahengCamera", 
                              f"Open camera {cam_id} failed: {e}")
            return  
        
        # 保存相机对象
        if not hasattr(self, "cams"):
            self.cams = {}
        self.cams[cam_id] = cam
        
        # 参数配置
        # 如果没配 "exposure"，就用 10000 兜底
        cam.ExposureTime.set(cfg.get("exposure", 10000))

        # set trigger mode and trigger source
        cam.TriggerMode.set(gx.GxSwitchEntry.ON)
        cam.TriggerSource.set(gx.GxTriggerSourceEntry.SOFTWARE)
        # cam.TriggerSource.set(gx.GxTriggerSourceEntry.LINE0)
        
        # get data stream
        data_stream = cam.data_stream[0]    
        # Register capture callback
        data_stream.register_capture_callback(
            lambda raw_image, cam_id=cam_id, queues=self.image_queues: 
                daheng_capture_callback(raw_image, cam_id, queues)
        )
        
        # start data acquisition
        cam.stream_on()
        LoggerManager.log("DahengCamera", f"Camera {cam_id} ({sn}) started")

        # # 采用硬触发模式时，释放下面代码
        while not self.stop_event.is_set():
            time.sleep(0.1)


        # # 模拟采集图片10张
        # count = 0
        # while not self.stop_event.is_set():
        #     cam.TriggerSoftware.send_command()
        #     time.sleep(1)
        #     if count > 10:
        #         break
        #     count += 1

        # close device
        cam.stream_off()
        data_stream.unregister_capture_callback()
        cam.close_device()
        self.camera_status[cam_id] = "stopped"
        LoggerManager.log("DahengCamera", f"Camera {cam_id} ({sn}) stopped")

    # ===== 给 CameraManager / Processor 用 =====
    def get_frame(self, cam_id, timeout=0.1):
        try:
            return self.image_queues[cam_id].get(timeout=timeout)
        except queue.Empty:
            return None

    def clear_frames(self):
        """
        一块 PCB 拍完后调用
        """
        for cam_id, q in self.image_queues.items():
            with q.mutex:
                q.queue.clear()
        LoggerManager.log("DahengCamera", "All image queues cleared")

    def stop(self):
        self.stop_event.set()
        LoggerManager.log("DahengCamera", "SDK cameras stopped")

    def get_camera_status(self):
        """
        返回相机状态，供 CommandDispatcher 查询
        """
        return self.camera_status.copy()

    def trigger(self, cam_id):
        """
        软触发拍照
        """
        try:
            cam = self.cams.get(cam_id)
            if cam is None:
                LoggerManager.log("DahengCamera", f"{cam_id} camera not found")
                return False
            cam.TriggerSoftware.send_command()
            return True
        except Exception as e:
            LoggerManager.log("DahengCamera", f"Trigger {cam_id} error: {e}")
            return False
          
    def trigger_all(self):
        """
        同时触发所有相机
        """
        for cam_id in self.cams.keys():
            self.trigger(cam_id)
        
    def close_camera(self, cam_id):
        cam = self.cams.get(cam_id)
        if cam is None:
            return
        try:
            cam.stream_off()
            cam.close_device()
            self.camera_status[cam_id] = "closed"
            LoggerManager.log("DahengCamera", f"Camera {cam_id} closed")
        except Exception as e:
            LoggerManager.log("DahengCamera", f"Close {cam_id} error: {e}")

    def stop(self):
        self.stop_event.set()
        if hasattr(self, "cams"):
            for cam_id, cam in self.cams.items():
                try:
                    cam.stream_off()
                    cam.close_device()
                except:
                    pass
        LoggerManager.log("DahengCamera", "All cameras stopped")











# ===== 测试代码 =====
if __name__ == "__main__":
    stop_event = threading.Event()
    camera_configs = {
        "cam_01": {
            "sn": "GCN25090273",
            "exposure": 30000
        },
        "cam_02": {
            "sn": "GCN25070204",
            "exposure": 30000
        }
    }
    daheng_camera = DahengCamera(
        camera_configs=camera_configs,
        stop_event=stop_event
    )
    daheng_camera.start()
    LoggerManager.log("Main", "Waiting for first frame...")
    start_time = time.time()
    got_frame = False
    while time.time() - start_time < 5:   # 最多等 5 秒
        frame_data = daheng_camera.get_frame("cam_01", timeout=0.5)
        if frame_data is not None:
            LoggerManager.log(
                "Main",
                f"Got frame from {frame_data['camera_id']} "
                f"timestamp={frame_data['timestamp']}"
            )
        frame_data = daheng_camera.get_frame("cam_02", timeout=0.5)
        if frame_data is not None:
            LoggerManager.log(
                "Main",
                f"Got frame from {frame_data['camera_id']} "
                f"timestamp={frame_data['timestamp']}"
            )
            got_frame = True
            break        
    # 结束采集
    stop_event.set()
    # 给线程一点退出时间（很重要）
    time.sleep(0.5)
    if got_frame:
        LoggerManager.log("Main", "Camera start & frame capture TEST PASSED")
    else:
        LoggerManager.log("Main", "Camera start TEST FAILED (no frame)")
    LoggerManager.log("Main", "Daheng camera test finished")

    
    
    
