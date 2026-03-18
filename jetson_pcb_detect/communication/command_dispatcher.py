import time
from utils.logger_manager import LoggerManager
import threading


class CommandDispatcher:
    def __init__(self, command_queue, processors, camera_manager,stop_event, send_callback=None):
        """
        :param command_queue: SocketClient 放入的命令队列
        :param processor: ImageProcessor 对象
        :param camera_manager: CameraManager 对象，用于查询相机状态等
        :param stop_event: 全局停止事件
        :param send_callback: 回消息接口，接受 dict 参数
        """
        self.command_queue = command_queue
        self.processors = processors        # 支持多个 processor，键为 camera_id
        self.camera_manager = camera_manager   # 新增
        self.stop_event = stop_event
        self.send_callback = send_callback  # 回消息接口

    # 启动命令调度线程，持续监听 command_queue, 有命令时，执行对应的处理函数
    def start(self):
        # threading.Thread(target=self._loop, daemon=True).start()
        # 启动自动检测
        threading.Thread(
            target=self._startup_check,
            daemon=True
        ).start()

    # # 命令处理循环
    # def _loop(self):
    #     while not self.stop_event.is_set():
    #         time.sleep(0.01)
    #         # try:
    #         #     cmd = self.command_queue.get(timeout=0.5)
    #         #     self._handle(cmd)
    #         # except queue.Empty:
    #         #     continue
    #         # except Exception as e:
    #         #     LoggerManager.log("Dispatcher", f"Unexpected error in loop: {e}")

    # 处理具体命令的函数，根据 cmd["cmd"] 的值调用 processor 的不同方法
    def _handle(self, cmd: dict):
        cmd_type = cmd.get("cmd")
        LoggerManager.log("Dispatcher", f"Handle cmd: {cmd_type}")
        
        # Jetson本地固定两台相机
        cam_ids = list(self.processors.keys())  # 例如 ["jetson_01_cam_01", "jetson_01_cam_02"]
        # 获取相机状态
        camera_status = self.camera_manager.get_camera_status()  # {"jetson_01_cam_01": "connected", "jetson_01_cam_02": "disconnected"}

        # 根据命令类型调用不同的处理逻辑
        if cmd_type == "camera_status":
            try:
                send_msg = {
                    "type": "camera_status",
                    "status": "ok",
                    "cameras": camera_status
                }
                self._send_response(send_msg)
                LoggerManager.log("Dispatcher", f"Camera status: {camera_status}")
            except Exception as e:
                LoggerManager.log("Dispatcher", f"Camera status error: {e}")
                self._send_response({"status": "camera_status_failed","error": str(e)
                })
                    
        elif cmd_type == "create_model":
            # 创建模型命令，调用 processor 的 handle_create_model 方法
            try:
                # 触发每台相机进入 detect 模式
                for cam_id in cam_ids:
                    if camera_status.get(cam_id) == "connected":
                        self.camera_manager.daheng_camera.trigger(cam_id)
                        processor = self.processors[cam_id]
                        processor.set_mode("create_model")  # 启动 ImageProcessor 的 _consume_loop
                    else:
                        LoggerManager.log("Dispatcher", f"Camera {cam_id} not connected, skip create_model")

                # self.processors[cam_id].set_mode("create_model")
                # self._send_response({"status": "create_model_done"})
            except Exception as e:
                LoggerManager.log("Dispatcher", f"Create_model error: {e}")
                self._send_response({ "type": "create_model","status": "create_model_failed", "error": str(e)})

        elif cmd_type == "detect":
            try:
                # 触发每台相机进入 detect 模式
                for cam_id in cam_ids:
                    if camera_status.get(cam_id) == "connected":
                        self.camera_manager.daheng_camera.trigger(cam_id)
                        processor = self.processors[cam_id]
                        processor.set_mode("detect")  # 启动 ImageProcessor 的 _consume_loop
                    else:
                        LoggerManager.log("Dispatcher", f"Camera {cam_id} not connected, skip detect")
                      
                # 上报命令已接收，并反馈每台相机状态
                self._send_response({
                    "type": "detect",
                    "status": "detect_started",
                    "cameras": cam_ids,
                    "camera_status": camera_status
                    })
           
            except Exception as e:
                LoggerManager.log("Dispatcher", f"Detect error: {e}")
                self._send_response({
                    "type": "detect",
                    "status": "detect_failed",
                    "error": str(e)
                })
           
        elif cmd_type == "stop":
            LoggerManager.log("Dispatcher", "Stop command received, shutting down system...")
            self._send_response({"type": "stop", "status": "system_stopped"})
            self.stop_event.set()  # 全局退出

        else:
            LoggerManager.log("Dispatcher", f"Unknown cmd: {cmd_type}")
            self._send_response({"type": "others","status": "unknown_cmd", "cmd": cmd_type})

    def _send_response(self, msg: dict):
        """通过回调发送消息，如果有定义 send_callback"""
        if self.send_callback:
            try:
                self.send_callback(msg)
            except Exception as e:
                LoggerManager.log("Dispatcher", f"Send callback error: {e}")
                
          
    def _startup_check(self):
        # 等待相机初始化
        time.sleep(2)
        camera_status = self.camera_manager.get_camera_status()
        msg = {
            "type": "camera_status",
            "status": "auto_report",
            "cameras": camera_status
         }
        self._send_response(msg)
        LoggerManager.log(
            "Dispatcher",
            f"Auto report camera status: {camera_status}"
         )
