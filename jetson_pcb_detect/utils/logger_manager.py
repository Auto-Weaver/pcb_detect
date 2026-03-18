import time
import threading

class LoggerManager:
    _lock = threading.Lock()
    log_file = "log_pcb.txt"

    @classmethod
    def log(cls, module, msg):
        text = f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] [{module}] {msg}"
        with cls._lock:
            print(text)
            with open(cls.log_file, "a", encoding="utf-8") as f:
                f.write(text + "\n")




if __name__ == "__main__":
    LoggerManager.log("MainTest", "测试日志写入")
