import queue
import threading
import time
import json

from cameras.camera_manager import CameraManager
from communication.jetson_server import JetsonServer
from communication.command_dispatcher import CommandDispatcher
from processing.image_processor import ImageProcessor
from utils.logger_manager import LoggerManager



def main():
    # -----------------------------
    # 1 读取相机配置
    # -----------------------------
    with open("config/camera_config.json", "r", encoding="utf-8") as f:
        camera_configs = json.load(f)

    LoggerManager.log("Camera configs:", camera_configs)
        
    # -----------------------------
    # 2 初始化队列和停止事件
    # -----------------------------
    frame_queues = {cam_id: queue.Queue(maxsize=20) for cam_id in camera_configs.keys()}
    stop_event = threading.Event()

    # -----------------------------
    # 3 JetsonServer（服务器）
    # -----------------------------
    server = JetsonServer(host="0.0.0.0", port=9000)
    server.start()
    
    # 回调函数：发送数据到PC
    def send_to_pc(msg):
        server.send_result(msg)

    # -----------------------------
    # 4 初始化 ImageProcessor
    # -----------------------------
    processors = {}
    for cam_id in camera_configs.keys():
        processors[cam_id] = ImageProcessor(
            cam_id=cam_id,
            frame_queue=frame_queues[cam_id],
            algoso=None,
            stop_event=stop_event,
            send_callback=send_to_pc
        )
        
    # -----------------------------
    # 5 初始化 CameraManager
    # -----------------------------
    cameras = CameraManager(
        camera_configs=camera_configs,
        frame_queues=frame_queues,
        stop_event=stop_event,
        img_paths=""  # 测试时可以填图片路径
    )    
        
    # -----------------------------
    # 6 初始化 Dispatcher
    # -----------------------------
    dispatcher = CommandDispatcher(
        command_queue=None,
        processors=processors,
        camera_manager=cameras,
        stop_event=stop_event,
        send_callback=send_to_pc
    )

    # -----------------------------
    # 7 启动模块
    # -----------------------------
    cameras.start()
    dispatcher.start()
    LoggerManager.log("Main", "System started, Press Ctrl+C to exit.")

    # -----------------------------
    # 8 主循环
    # -----------------------------
    try:
        while not stop_event.is_set():
            cmd = server.get_next_command(timeout=0.1)
            if cmd:
                dispatcher._handle(cmd)
            time.sleep(0.01)
    except KeyboardInterrupt:
        stop_event.set()

    # -----------------------------
    # 9 停止系统
    # -----------------------------
    cameras.stop()
    for p in processors.values():
        p.stop()
    # dispatcher.stop()
    server.stop()
    LoggerManager.log("Main", "System exited")


if __name__ == "__main__":
    main()     
    
    # {"cmd":"detect"}
    # {"cmd": "detect", "camera_id": "cam_01"}
    # {"cmd": "create_model", "camera_id": "cam_01"}
    
    
    