import queue
import os
import cv2
import time
import threading
from ultralytics import YOLO
from utils.logger_manager import LoggerManager
import base64

MAX_DEFECT_SEND = 10   # 最多发送10个缺陷
DEFECT_EXPAND_PIXEL = 80
CONF_SEND_THRESHOLD = 0.5


class ImageProcessor:
    def __init__(self, cam_id, frame_queue, algoso, stop_event, send_callback):
        """
        :param cam_id: 相机 ID
        :param frame_queue: CameraManager 放入的帧队列
        :param algoso: 算法处理对象
        :param stop_event: 全局停止事件
        :param send_callback: 回调函数，接收 dict 参数发送结果给 PC
        """
        self.cam_id = cam_id
        self.frame_queue = frame_queue
        self.algoso = algoso
        self.stop_event = stop_event
        self.send_callback = send_callback
        
        self.mode = None
        self.target_count = 14
        self.current_count = 0
        self.in_cycle = False   # 是否正在执行一轮
        self.board_id = None   # 当前检测的板子 ID
        
        self.model = YOLO("models/epoch470_1024.engine", task="detect")
        
         # 异步线程池，用于处理帧
        from concurrent.futures import ThreadPoolExecutor
        self.pool = ThreadPoolExecutor(max_workers=1)
        threading.Thread(target=self._consume_loop, daemon=True).start()

    # 模式设置函数
    def set_mode(self, mode):
        if self.in_cycle:
            LoggerManager.log("Processor", f"[{self.cam_id}] Already in cycle, ignore new command")
            return
        self.mode = mode
        self.current_count = 0
        self.in_cycle = True
        
        # 新增时间戳作为板子 ID，确保每块板子唯一 如："1710660000123"
        self.board_id = str(int(time.time()*1000))
        LoggerManager.log("Processor", f"[{self.cam_id}] Start {mode} cycle")

    def _consume_loop(self):
        while not self.stop_event.is_set():
             # 等待命令
            if not self.in_cycle:
                time.sleep(0.01)
                continue
            # 阻塞等待图片
            task = self.frame_queue.get()   

            # 设置模式
            task["camera_id"] = self.cam_id
            task["action"] = self.mode
            index = self.current_count
            task["pic_index"] = index
            task["board_id"] = self.board_id
            
            safe_task = {
                "frame": task["frame"],
                "timestamp": task["timestamp"],
                "camera_id": self.cam_id,
                "action": self.mode,
                "pic_index": self.current_count,
                "board_id": self.board_id
            }
            
            # 异步处理算法  异步线程    
            self.submit(task)
            
            # 立即发送图片给PC（压缩） 是否需要发送原图？
            self._send_image_to_pc(task)
            
            # 超过14次后结束本轮
            self.current_count += 1
            if self.current_count >= self.target_count:
                LoggerManager.log("Processor",
                              f"[{self.cam_id}] {self.mode} round finished")
                # 结束本轮
                self.in_cycle = False
                self.mode = None
                self.current_count = 0


    def _send_image_to_pc(self, task):
        # 测试用，直接读取图片
        # img_path = "img/pcb.png"
        # frame_bgr = cv2.imread(img_path)
        # if frame_bgr is None:
        #     LoggerManager.log("Processor", f"无法读取图片: {img_path}")
        #     return

        frame = task["frame"]
        # ---------- preview（压缩图） ----------
        preview_b64 = self.encode_image_to_base64(frame, quality=80)
         
        if self.send_callback:
            try:
                self.send_callback({
                    "type": "image",
                    "camera_id": self.cam_id,
                    "board_id": self.board_id,
                    "pic_index": task["pic_index"],
                    "image_type": "preview",
                    "image": preview_b64
                })
            except Exception as e:
                LoggerManager.log("Processor", f"Send image callback error: {e}")
                
        #  # ---------- raw（原图） ----------
        #  raw_b64 = self.encode_image_to_base64(frame, quality=100)
         
        #  self.send_callback({
        #      "type": "image",
        #     "camera_id": self.cam_id,
        #     "board_id": self.board_id,
        #     "pic_index": task["pic_index"],
        #     "image_type": "raw",
        #     "image": raw_b64
        #  })
         

    def _process(self, task):
        cam_id = task["camera_id"]
        frame = task["frame"]
        ts = task["timestamp"]
        action = task["action"]
        
        result_data = None

        if action == "create_model":
            # ret, status, score = self.algoso.create_template(
            #     frame,
            #     f"./pcb_from_algo_{cam_id}.png",
            #     scale_min=0.3,
            #     scale_max=1.1
            # )
            LoggerManager.log("Processor", f"[{cam_id}] create_model done")
            # result_data = {
            #     "status": status,
            #     "score": score
            # }
            
            # 测试 发送压缩图片 
            img_base64 = encode_image_to_base64(frame, quality=80)
            
            #// preview / raw / result / defect
            self.send_callback({
                "type": "image",
                "camera_id": str(cam_id),
                "board_id": self.board_id,
                "pic_index": task.get("pic_index", 0),
                "image_type": "preview",
                # "image": img_base64
                "image": None
            })
            
        elif action == "detect":
            results = self.model.predict(frame, save=False,conf=0.25,imgsz=1024,device=0,verbose=True)
            boxes = results[0].boxes.xyxy.tolist()
            classes = results[0].boxes.cls.tolist()
            confs = results[0].boxes.conf.tolist()

            # 按置信度排序
            detections = list(zip(boxes, confs, classes))
            detections.sort(key=lambda x: x[1], reverse=True)

            defect_count = 0
            defects = []
            for box, conf, cls in detections:
                # 置信度过滤
                if conf < CONF_SEND_THRESHOLD:
                    continue
                
                # 限制发送数量
                if len(defects) >= MAX_DEFECT_SEND:
                    break
                
                x1, y1, x2, y2 = map(int, box)
                # 裁剪缺陷区域
                crop = self.crop_defect(frame, box, expand=DEFECT_EXPAND_PIXEL)
                # 编码图片
                defect_b64 = self.encode_image(crop,quality=80)

                self.send_callback({
                    "type": "defect",
                    "camera_id": cam_id,
                     "board_id": self.board_id,
                     "pic_index": task["pic_index"],
                     "class": int(cls),
                     "conf": float(conf),
                     "box": [x1, y1, x2, y2],
                     "image": defect_b64
                })
                 # 画框
                cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 0, 255), 2)
                defect_count += 1
                
                # 加入 defects 列表
                defects.append({
                    "class": int(cls),
                    "conf": float(conf),
                    "box": [x1, y1, x2, y2],
                    "image": defect_b64
                })
                 
            # ---------- result 图（带框） ----------
            result_img_b64 = self.encode_image(frame, quality=80)
            self.send_callback({
                "type": "image",
                "camera_id": cam_id,
                "board_id": self.board_id,
                "pic_index": task["pic_index"],
                "image_type": "result",
                "image": result_img_b64
            })
            
             # ---------- 汇总结果 ----------
            self.send_callback({
                "type": "result",
                "camera_id": cam_id,
                "board_id": self.board_id,
                "pic_index": task["pic_index"],
                "defect_count": defect_count,
                "ok": defect_count == 0
            })
            
            # speed = results[0].speed
            # LoggerManager.log("Processor",f"warmup Preprocess: {speed['preprocess']:.2f} ms")
            # LoggerManager.log("Processor",f"warmup Inference:  {speed['inference']:.2f} ms")
            # LoggerManager.log("Processor",f"warmup Postprocess:{speed['postprocess']:.2f} ms")
            # detections = self.algoso.detect(frame)
            # result_data = detections

        elif action == "align":
            # 对齐功能
            aligned_frame = self.algoso.align(frame)
            LoggerManager.log("Processor", f"[{cam_id}] align done")
            result_data = "align_done"

        else:
            LoggerManager.log("Processor", f"[{cam_id}] unknown action: {action}")
            return

        # # 保存图片
        # filename = f"results/images/cam{cam_id}_{int(ts*1000)}_{action}.jpg"
        # cv2.imwrite(filename, frame)


    
    def encode_image_to_base64(self,frame, quality=80):
        """
        将 numpy_image 转为 JPEG base64 字符串
        """
        frame_bgr = cv2.cvtColor(frame, cv2.COLOR_RGB2BGR)
        _, buffer = cv2.imencode(".jpg", frame_bgr, [int(cv2.IMWRITE_JPEG_QUALITY), quality])
        return base64.b64encode(buffer).decode('utf-8')
        
    def submit(self, task):
        if not self.stop_event.is_set():
            self.pool.submit(self._process, task)

    def stop(self):
        self.pool.shutdown(wait=True)
        LoggerManager.log("Processor", f"[{self.cam_id}] stopped")

    def crop_defect(self, frame, box, expand=40):
        h, w = frame.shape[:2]
        x1, y1, x2, y2 = map(int, box)
        x1 = max(0, x1 - expand)
        y1 = max(0, y1 - expand)
        x2 = min(w, x2 + expand)
        y2 = min(h, y2 + expand)
        crop = frame[y1:y2, x1:x2]
        return crop
    
    
    def encode_image(self, img, quality=80):
        ret, buffer = cv2.imencode(".jpg", img, [int(cv2.IMWRITE_JPEG_QUALITY), quality])
        return base64.b64encode(buffer).decode()












# test
import cv2
import threading
from processing.image_processor import ImageProcessor

# 模拟发送给PC的函数
def dummy_send_callback(msg):
    print("Send to PC:", msg)

if __name__ == "__main__":
    # 1. 加载测试图片
    img_path = "img/pcb.png"
    frame = cv2.imread(img_path)
    if frame is None:
        raise FileNotFoundError(f"Cannot load image: {img_path}")
    

    # 2. 创建 AlgosoWrapper
    # algoso = AlgosoWrapper("libpcb_algo.so")
    
    stop_event = threading.Event()
    # 3. 创建 ImageProcessor
    processor = ImageProcessor(
        cam_id="cam_01",
        frame_queue=None,  # 这里不需要队列
        algoso=None,
        stop_event=stop_event,
        send_callback=dummy_send_callback
    )
    
    # 4. 构造任务
    task = {
        "camera_id": "cam_01",
        "frame": frame,
        "timestamp": 0.0,
        "action": "detect"   # 可以改成 create_model / align
    }
    # 5. 调用处理函数
    processor._process(task)
    
    # 6. 等待线程完成
    processor.stop()
    
    print("Test finished.")


