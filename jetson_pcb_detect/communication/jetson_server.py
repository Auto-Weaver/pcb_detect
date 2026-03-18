import socket
import threading
import json
import time
import queue
from utils.logger_manager import LoggerManager



class JetsonServer:
    """
    Jetson TCP 服务器
    作用：
    1. 等待PC端连接
    2. 接收命令(JSON)
    3. 将命令放入 command_queue
    4. 发送结果/图片/缺陷图片给 PC
    """

    def __init__(self, host='0.0.0.0', port=9000):
        self.host = host
        self.port = port
        self.server_sock = None
        self.clients = []  # 存放所有已连接客户端
        self.lock = threading.Lock()
        self.running = False

        # 队列用于内部处理
        self.command_queue = queue.Queue()
        self.result_queue = queue.Queue()

    def start(self):
        self.running = True
        self.server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
         # 允许端口复用
        self.server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

        self.server_sock.bind((self.host, self.port))
        self.server_sock.listen(1)
        LoggerManager.log("JetsonServer", f"服务器启动，监听 {self.host}:{self.port}")

        # 接受客户端连接线程
        threading.Thread(target=self._accept_clients_loop, daemon=True).start()
        # 发送结果线程
        threading.Thread(target=self._send_result_loop, daemon=True).start()

    def stop(self):
        self.running = False
        if self.server_sock:
            self.server_sock.close()
        with self.lock:
            for c, _ in self.clients:
                c.close()
            self.clients.clear()
        LoggerManager.log("JetsonServer", "服务器已停止")

    # ---------------- 连接客户端 ----------------
    def _accept_clients_loop(self):
        while self.running:
            try:
                client_sock, addr = self.server_sock.accept()
                LoggerManager.log("JetsonServer", f"客户端连接 {addr}")
                with self.lock:
                    self.clients.append((client_sock, addr))
                threading.Thread(target=self._recv_loop, args=(client_sock, addr), daemon=True).start()
            except Exception as e:
                LoggerManager.log("JetsonServer", f"Accept异常: {e}")
                time.sleep(1)

    # ---------------- 接收数据 ----------------
    def _recv_loop(self, client_sock, addr):
        while self.running:
            try:
                data = client_sock.recv(4096)
                if not data:
                    LoggerManager.log("JetsonServer", f"客户端断开 {addr}")
                    self._remove_client(client_sock, addr)
                    break

                # 可能一次收到多条 JSON，按换行拆分
                for cmd_str in data.decode().strip().split("\n"):
                    if not cmd_str:
                        continue
                    try:
                        cmd = json.loads(cmd_str)
                        self.command_queue.put(cmd)
                        LoggerManager.log("JetsonServer", f"接收命令: {cmd_str}")
                    except json.JSONDecodeError:
                        LoggerManager.log("JetsonServer", f"无效JSON: {cmd_str}")

            except Exception as e:
                LoggerManager.log("JetsonServer", f"接收异常 {addr}: {e}")
                self._remove_client(client_sock, addr)
                break

    # ---------------- 发送结果 ----------------
    def _send_result_loop(self):
        while self.running:
            try:
                result = self.result_queue.get(timeout=0.5)
                msg = (json.dumps(result) + "\n").encode()
                with self.lock:
                    for client_sock, addr in self.clients:
                        try:
                            client_sock.sendall(msg)
                        except Exception as e:
                            LoggerManager.log("JetsonServer", f"发送失败 {addr}: {e}")
                            self._remove_client(client_sock, addr)
            except queue.Empty:
                continue

    def _remove_client(self, client_sock, addr):
        with self.lock:
            self.clients = [(c, a) for c, a in self.clients if c != client_sock]
        try:
            client_sock.close()
        except:
            pass
        LoggerManager.log("JetsonServer", f"移除客户端 {addr}")

    # ---------------- 外部接口 ----------------
    def send_result(self, result_dict):
        """
        将检测结果 / 图片 / 缺陷图片发送给所有连接的 PC
        :param result_dict: dict 类型，例如：
            {"cmd":"result", "camera":1, "result":1}
            {"cmd":"image", "camera":1, "image":base64_str}
        """
        self.result_queue.put(result_dict)

    def get_next_command(self, timeout=0.1):
        """
        从队列中取下一条命令
        :param timeout: 超时时间
        :return: dict 或 None
        """
        try:
            return self.command_queue.get(timeout=timeout)
        except queue.Empty:
            return None
        
    def _heartbeat_loop(self):
        while self.running:
            msg = {
                "type":"heartbeat",
                "device":"jetson_01",
                "time":time.time()
            }
            self.send_result(msg)
            time.sleep(3)














# ---------------- 示例使用 ----------------
if __name__ == "__main__":
    stop_event = threading.Event()
    server = JetsonServer(host="0.0.0.0", port=9000)
    server.start()

    try:
        while True:
            cmd = server.get_next_command(timeout=0.1)
            if cmd:
                LoggerManager.log("Main", f"处理命令: {cmd}")
                # 模拟处理命令并返回结果
                if cmd.get("cmd") == "detect":
                    camera = cmd.get("camera", 1)
                    server.send_result({"cmd": "result", "camera": camera, "result": 1})
            time.sleep(0.01)
    except KeyboardInterrupt:
        stop_event.set()
        server.stop()