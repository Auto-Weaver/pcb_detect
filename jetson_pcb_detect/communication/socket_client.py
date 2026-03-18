import socket
import threading
import json
import time
import queue
from utils.logger_manager import LoggerManager



class SocketClient:
    def __init__(self, server_ip, server_port, result_queue, command_queue,stop_event):
        self.server_ip = server_ip
        self.server_port = server_port
        self.result_queue = result_queue
        self.command_queue = command_queue
        self.stop_event = stop_event
        self.sock = None
        self.lock = threading.Lock()
        self.running = False


    def start(self):
        self.running = True
        # daemon=True 确保主线程退出时子线程也能自动退出
        # 连接和发送线程分开，避免互相阻塞
        threading.Thread(target=self._connect_loop, daemon=True).start()
        threading.Thread(target=self._send_loop, daemon=True).start()
        LoggerManager.log("SocketClient", f"Client start connecting {self.server_ip}:{self.server_port}")

    def _connect_loop(self):
        while not self.stop_event.is_set():
            #具备断开重连功能
            if self.sock is None:
                try:
                    self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    self.sock.connect((self.server_ip, self.server_port))
                    threading.Thread(target=self._recv_loop, daemon=True).start()
                    LoggerManager.log("SocketClient", "Connected to server")
                except Exception as e:
                    LoggerManager.log("SocketClient", f"Connect failed: {e}")
                    self.sock = None
                    time.sleep(2)
            else:
                time.sleep(1)

    # 接收线程，持续监听服务器的命令CMD,并放入 command_queue
    def _recv_loop(self):
        while self.running and not self.stop_event.is_set():
            try:
                # 阻塞直到收到数据，或者连接断开
                data = self.sock.recv(4096)
                # 接收到的数据 data == b'{"cmd": "xxx"}\n'  
                # 或者 data == b'' 表示连接关闭
                if not data:
                    LoggerManager.log("SocketClient", "Server closed connection")
                    self._handle_disconnect()
                    break

                for cmd_str in data.decode().strip().split("\n"):
                    if not cmd_str:
                        continue

                    try:
                        cmd = json.loads(cmd_str)
                        self.command_queue.put(cmd)
                        LoggerManager.log("SocketClient", f"recv JSON: {cmd_str}")
                    except json.JSONDecodeError:
                        LoggerManager.log("SocketClient", f"Invalid JSON: {cmd_str}")
            except Exception as e:
                LoggerManager.log("SocketClient", f"Recv error: {e}")
                self._handle_disconnect()
                break

    # 发送线程，持续监听 result_queue, 有结果时，将结果发送给服务器
    def _send_loop(self):
        while not self.stop_event.is_set():
            try:
                result = self.result_queue.get(timeout=0.5)
                if self.sock:
                    msg = (json.dumps(result) + "\n").encode()
                    with self.lock:
                        self.sock.sendall(msg)
            except queue.Empty:
                continue
    
    # 停止客户端，关闭连接并清理资源
    def stop(self):
        self.running = False
        if self.sock:
            self.sock.close()
        LoggerManager.log("SocketClient", "Client stopped")

    # 处理断开连接的情况，关闭 socket 并清理资源
    def _handle_disconnect(self):
        if self.sock:
            self.sock.close()
        self.sock = None
        LoggerManager.log("SocketClient", "Client disconnected")


