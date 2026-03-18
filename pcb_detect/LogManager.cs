using System;
using System.IO;

namespace pcb_detect {
    public enum LogLevel {
        INFO,
        WARN,
        ERROR
    }

    class LogManager {
        private static LogManager _instance = new LogManager();
        public static LogManager Instance => _instance;
        public event Action<string, LogLevel> OnLog;
        private readonly object _lock = new object();
        private string _logDir;

        private LogManager() {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
        }

        public void Log(string msg, LogLevel level = LogLevel.INFO) {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{time}] [{level}] {msg}";
            // UI日志
            OnLog?.Invoke(line, level);
            // 写文件
            WriteFile(line);
        }

        private void WriteFile(string line) {
            try {
                lock (_lock) {
                    string file = Path.Combine(_logDir,
                        DateTime.Now.ToString("yyyyMMdd") + ".txt");

                    File.AppendAllText(file, line + Environment.NewLine);
                }
            } catch {
            }
        }
    }
}