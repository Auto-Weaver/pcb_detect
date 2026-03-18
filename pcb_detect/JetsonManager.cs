using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using Newtonsoft.Json.Linq;





namespace pcb_detect {
    // 管理多台 Jetson 设备
    class JetsonManager {

        private Dictionary<string, JetsonClient> _jetsons = new Dictionary<string, JetsonClient>();
        private Dictionary<string, Thread> _threads = new Dictionary<string, Thread>();
        private Dictionary<string, bool> _runningFlags = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lastStatus = new Dictionary<string, bool>();
        private readonly object _lock = new object();
        // 第一个 string = jetsonId 
        // 第二个 string = cameraId
        public event Action<string, string, Bitmap> OnImageReceived;                  // 图片接收事件
        public event Action<string, string, Bitmap, string> OnDefectImageReceived;    // 缺陷图片事件
        public event Action<string, string> OnResultReceived;                      // 检测结果事件
        public event Action<string, bool> OnConnectionStatusChanged;               // Jetson连接状态变化事件
        public event Action<string, DateTime> OnHeartbeatReceived;


        #region 添加 Jetson
        public void AddJetson(string jetsonId, string ip, int port) {
            lock (_lock) {
                if (_jetsons.ContainsKey(jetsonId)) return;
                var client = new JetsonClient(ip, port);
                // 图片事件
                client.OnImageReceived += (cameraId, bmp) => OnImageReceived?.Invoke(jetsonId, cameraId, bmp);
                client.OnDefectImageReceived += (cameraId, bmp, defectName) => OnDefectImageReceived?.Invoke(jetsonId, cameraId, bmp, defectName);
                // 结果事件
                client.OnResultReceived += (result) => {
                    try {
                        JObject obj = JObject.Parse(result);
                        if (obj["type"]?.ToString() == "heartbeat") {
                            // 如果收到的是heartbeat，触发心跳事件
                            double t = obj["time"]?.Value<double>() ?? 0;
                            DateTime dt = DateTimeOffset.FromUnixTimeSeconds((long)t).DateTime;
                            OnHeartbeatReceived?.Invoke(jetsonId, dt);
                            return;
                        }
                    } catch { }
                    OnResultReceived?.Invoke(jetsonId, result);
                };
                // 连接状态
                client.OnConnectionStatusChanged += (connected) => OnConnectionStatusChanged?.Invoke(jetsonId, connected);
                _jetsons.Add(jetsonId, client);
            }
        }
        #endregion



        #region 自动重连守护线程
        public void StartAllAutoConnect(int retryMs = 2000) {
            //启动和重连所有jetson
            lock (_lock) {
                foreach (var kv in _jetsons) {
                    string id = kv.Key;
                    JetsonClient client = kv.Value;
                    if (_threads.ContainsKey(id)) continue;
                    _runningFlags[id] = true;
                    Thread t = new Thread(() => {
                        while (_runningFlags[id]) {
                            try {
                                if (!client.Connected) {
                                    // 开启接收线程 同步线程
                                    bool ok = client.Connect();
                                    //OnConnectionStatusChanged?.Invoke(id, ok);
                                    UpdateConnectionStatus(id, ok);
                                }
                            } catch (Exception ex) {
                                Console.WriteLine($"Jetson {id} 自动连接异常: {ex.Message}");
                            }
                            Thread.Sleep(retryMs);
                        }
                    }) {
                        IsBackground = true,
                        Name = $"Jetson_AutoConnect_{id}"
                    };
                    _threads[id] = t;
                    t.Start();
                }
            }
        }

        public void StopAll() {
            lock (_lock) {
                foreach (var key in _runningFlags.Keys.ToList())
                    _runningFlags[key] = false;

                foreach (var client in _jetsons.Values)
                    client.Disconnect();
            }
        }
        #endregion

        #region 异步连接 / 断开
        public async Task ConnectAllAsync() {
            List<Task> tasks = new List<Task>();
            lock (_lock) {
                foreach (var kv in _jetsons) {
                    string id = kv.Key;
                    JetsonClient client = kv.Value;

                    tasks.Add(Task.Run(async () => {
                        bool ok = await client.ConnectAsync();
                        if (!ok)
                            Console.WriteLine($"Jetson {id} 连接失败");
                    }));
                }
            }
            await Task.WhenAll(tasks);
        }

        public void DisconnectAll() {
            lock (_lock) {
                foreach (var client in _jetsons.Values)
                    client.Disconnect();
            }
        }

        private void UpdateConnectionStatus(string id, bool connected) {
            lock (_lock) {
                if (_lastStatus.TryGetValue(id, out var last) && last == connected)
                    return; // 状态没变，不触发
                _lastStatus[id] = connected;
            }
            OnConnectionStatusChanged?.Invoke(id, connected);
        }

        #endregion

        #region 命令发送
        public void SendCommand(string jetsonId, object cmd) {
            lock (_lock) {
                if (_jetsons.TryGetValue(jetsonId, out var client))
                    client.SendCommand(cmd);
            }
        }

        public void BroadcastCommand(object cmd) {
            lock (_lock) {
                foreach (var client in _jetsons.Values)
                    client.SendCommand(cmd);
            }
        }
        #endregion

        #region 状态查询
        public bool IsConnected(string jetsonId) {
            lock (_lock) {
                return _jetsons.TryGetValue(jetsonId, out var client) && client.Connected;
            }
        }

        // 只要有一个 JetsonClient Connected == true
        public bool AnyConnected() {
            lock (_lock) {
                return _jetsons.Values.Any(c => c.Connected);
            }
        }

        // 所有 JetsonClient 都 Connected == true
        public bool AllConnected() {
            lock (_lock) {
                return _jetsons.Values.All(c => c.Connected);
            }
        }

        // 统计 Connected == true 的 JetsonClient 数量
        public int ConnectedCount() {
            lock (_lock) {
                return _jetsons.Values.Count(c => c.Connected);
            }
        }

        public List<string> GetConnectedJetsonIds() {
            lock (_lock) {
                return _jetsons.Where(kv => kv.Value.Connected).Select(kv => kv.Key).ToList();
            }
        }
        #endregion

    } 

}
