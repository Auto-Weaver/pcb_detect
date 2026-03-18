using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;



namespace pcb_detect {

    public class JetsonClient {
        private TcpClient _client;
        private NetworkStream _stream;
        private string _ip;
        private int _port;
        private bool _running = false;

        // ----------------- 事件 -----------------
        public event Action<string, Bitmap> OnImageReceived;            // 普通相机图片
        public event Action<string, Bitmap, string> OnDefectImageReceived; // 缺陷图片
        public event Action<string> OnResultReceived;               // 检测结果 JSON
        public event Action<bool> OnConnectionStatusChanged;        // 连接状态变化

        public bool Connected => _client != null && _client.Connected;
        public JetsonClient(string ip, int port) {
            _ip = ip;
            _port = port;
        }

        // ----------------- 连接与断开 -----------------
        public bool Connect() {
            try {
                _client = new TcpClient();
                _client.Connect(_ip, _port);
                _stream = _client.GetStream();

                _running = true;
                Thread t = new Thread(ReceiveLoop) { IsBackground = true };
                t.Start();

                //Task.Run(() => ReceiveLoop());
                //LogManager.Instance.Log($"Jetson连接成功 {_ip}:{_port}");
                OnConnectionStatusChanged?.Invoke(true);
                return true;
            } catch (Exception ex) {
                //LogManager.Instance.Log($"Jetson连接失败:{ex.Message}", LogLevel.ERROR);
                OnConnectionStatusChanged?.Invoke(false);
                return false;
            }
        }

        public async Task<bool> ConnectAsync(int timeoutMs = 3000) {
            try {
                _client = new TcpClient();
                var connectTask = _client.ConnectAsync(_ip, _port);
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask) {
                    LogManager.Instance.Log("Jetson连接超时", LogLevel.ERROR);
                    return false;
                }

                _stream = _client.GetStream();
                _running = true;
                Task.Run(() => ReceiveLoop()); // 接收循环可以在后台线程

                LogManager.Instance.Log($"Jetson连接成功 {_ip}:{_port}");
                OnConnectionStatusChanged?.Invoke(true);
                return true;
            } catch (Exception ex) {
                LogManager.Instance.Log($"Jetson连接失败:{ex.Message}", LogLevel.ERROR);
                OnConnectionStatusChanged?.Invoke(false);
                return false;
            }
        }


        public void Disconnect() {
            _running = false;
            try { _stream?.Close(); _client?.Close(); } catch { }
            OnConnectionStatusChanged?.Invoke(false);
        }

        // ----------------- 发送命令 -----------------
        public void SendCommand(object cmd) {
            if (!Connected) return;
            try {
                string json = JsonConvert.SerializeObject(cmd);
                byte[] data = Encoding.UTF8.GetBytes(json);
                _stream.Write(data, 0, data.Length);
                LogManager.Instance.Log($"发送Jetson:{json}");
            } catch (Exception ex) {
                LogManager.Instance.Log($"发送Jetson失败:{ex.Message}", LogLevel.ERROR);
            }
        }

        // ----------------- 接收循环 -----------------
        private void ReceiveLoop() {
            byte[] buffer = new byte[1024 * 1024]; // 1MB
            StringBuilder sb = new StringBuilder();
            try {
                while (_running) {
                    // 没有数据就会等待
                    int len = _stream.Read(buffer, 0, buffer.Length);
                    if (len == 0) break;

                    string chunk = Encoding.UTF8.GetString(buffer, 0, len);
                    // 把新收到的数据拼接到旧数据后面
                    sb.Append(chunk);

                    string content = sb.ToString();
                    string[] lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    // 保留最后一行（可能不完整）
                    sb.Clear();
                    if (!content.EndsWith("\n") && lines.Length > 0) 
                        sb.Append(lines[lines.Length - 1]);

                    int count = content.EndsWith("\n") ? lines.Length : lines.Length - 1;
                    for (int i = 0; i < count; i++)
                        ParseMessage(lines[i]);



                    //string msg = Encoding.UTF8.GetString(buffer, 0, len);
                    //ParseMessage(msg);
                }
            } catch (Exception ex) {
                LogManager.Instance.Log($"Jetson通信异常:{ex.Message}", LogLevel.ERROR);
            } finally {
                Disconnect();
            }
        }

        // ----------------- 解析JSON消息 -----------------
        private void ParseMessage(string json) {
            try {
                // 反序列化 把 JSON 字符串解析成 C# 对象
                JObject obj = JObject.Parse(json);
                string type = obj["type"]?.ToString();

                LogManager.Instance.Log($"ParseMessage解析type : "+ type, LogLevel.INFO);
                // 尝试读取 "camera_id" 字段 如果没有这个字段，默认 "cam_01"
                //string camStr = obj["camera_id"]?.ToString() ?? "cam_01";
                //int cam = camStr.EndsWith("01") ? 1 : 2;
                //string boardId = obj["board_id"]?.ToString() ?? "jetson_01";
                //int cam = obj["camera_id"]?.ToObject<int>() ?? 1;
                //string camStr = obj["camera_id"]?.ToString() ?? "cam_01";
                //int cam = camStr.EndsWith("01") ? 1 : 2;
                LogManager.Instance.Log($"原始字符串: {json}");
                string cameraId = obj["camera_id"]?.ToString();
                switch (type) {
                    case "image":
                        HandleImage(obj, cameraId);
                        break;
                    case "defect":
                        HandleDefect(obj, cameraId);
                        break;
                    case "result":
                        OnResultReceived?.Invoke(json);
                        break;
                    default:
                        LogManager.Instance.Log($"未知Jetson消息:{json}");
                        break;
                }
            } catch (Exception ex) {
                LogManager.Instance.Log($"JSON解析失败:{ex.Message}");
                //LogManager.Instance.Log($"JSON解析失败: {ex.Message}, 原始字符串: {json}");
            }
        }

        // ----------------- 处理普通相机图片 -----------------
        private void HandleImage(JObject obj, string cameraId) {
            try {
                string base64 = obj["image"].ToString();
                byte[] imgBytes = Convert.FromBase64String(base64);
                using (MemoryStream ms = new MemoryStream(imgBytes)) {
                    Bitmap bmp = new Bitmap(ms);
                    OnImageReceived?.Invoke(cameraId, bmp);
                }
            } catch (Exception ex) {
                LogManager.Instance.Log($"普通图片解析失败:{ex.Message}");
            }
        }

        // ----------------- 处理缺陷图片 -----------------
        private void HandleDefect(JObject obj, string cameraId) {
            try {
                string base64 = obj["image"].ToString();
                string defectName = obj["defectName"]?.ToString() ?? $"defect_{DateTime.Now:HHmmssfff}";
                byte[] bytes = Convert.FromBase64String(base64);
                using (MemoryStream ms = new MemoryStream(bytes)) {
                    Bitmap bmp = new Bitmap(ms);
                    OnDefectImageReceived?.Invoke(cameraId, bmp, defectName);

                    // 保存到本地
                    //string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "defects");
                    string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "defects", cameraId);

                    if (!Directory.Exists(saveDir))
                        Directory.CreateDirectory(saveDir);

                    string filePath = Path.Combine(saveDir, defectName + ".jpg");
                    bmp.Save(filePath);
                }
            } catch (Exception ex) {
                LogManager.Instance.Log($"缺陷图片解析失败:{ex.Message}");
            }
        }
        
        // ----------------- 辅助：保存缺陷图片 -----------------
        public void SaveDefectImage(Bitmap bmp, string saveDir, string defectName = null) {
            try {
                if (!Directory.Exists(saveDir))
                    Directory.CreateDirectory(saveDir);

                string name = defectName ?? DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                string filePath = Path.Combine(saveDir, $"{name}.jpg");
                bmp.Save(filePath);
                LogManager.Instance.Log($"缺陷图片保存:{filePath}");
            } catch (Exception ex) {
                LogManager.Instance.Log($"保存缺陷图片失败:{ex.Message}", LogLevel.ERROR);
            }
        }


    }
}