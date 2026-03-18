using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace pcb_detect {
    class PLCManager {
        private Socket _server;                                  // 监听连接请求
        private Socket _currentClient;
        private int _port;                                       // 端口号

        private bool _isRunning = false;                         // 控制PLC的连接

        public bool Connected => _currentClient != null && _currentClient.Connected;

        public event Action<byte[]> OnMessageReceived;         // 收到原始PLC消息
        public event Action<bool> OnConnectionStatusChanged;   // 连接/断开
        public event Action OnClientDisconnected;


        public PLCManager( int port = 2001) {
            _port = port;
        }


        public void ClosePlc() {
            _isRunning = false;
            _server.Close();
        }

        public void Init() {
            try {
                _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _server.Bind(new IPEndPoint(IPAddress.Any, _port));
                _server.Listen(3);
            } catch (Exception ex) {
                LogManager.Instance.Log($"PLC服务异常: {ex.Message}", LogLevel.ERROR);
            }
        }

        public void Start() {
            _isRunning = true;
            Task.Run(() => AcceptClientsLoop());
        }

        private void AcceptClientsLoop() {
            while (_isRunning) {
                try {
                    var client = _server.Accept();
                    // 如果已有连接，先关闭
                    if (_currentClient != null) {
                        try {
                            _currentClient.Close();
                        } catch { }
                    }
                    _currentClient = client;
                    LogManager.Instance.Log("PLC已连接", LogLevel.INFO);
                    OnConnectionStatusChanged?.Invoke(true);
                    Task.Run(() => ReceiveLoop(client));
                } catch (Exception ex) {
                    LogManager.Instance.Log($"PLC Accept异常: {ex.Message}", LogLevel.ERROR);
                    OnConnectionStatusChanged?.Invoke(false);
                }
            }
        }

        private void ReceiveLoop(Socket client) {
            byte[] buffer = new byte[1024];
            try {
                while (_isRunning && client.Connected) {
                    int len = client.Receive(buffer);
                    if (len == 0)
                        break;

                    byte[] msg = buffer.Take(len).ToArray();

                    // 调试打印
                    LogManager.Instance.Log(
                        $"收到PLC数据: {BitConverter.ToString(msg)}",
                        LogLevel.INFO
                        );
                    // 事件通知外层解析
                    OnMessageReceived?.Invoke(msg); 
                }
            } catch (Exception ex) {
                // 断开
                LogManager.Instance.Log($"PLC断开连接: {ex.Message}", LogLevel.ERROR);
            } finally {
                OnConnectionStatusChanged?.Invoke(false);
                OnClientDisconnected?.Invoke();
                client.Close();
                _currentClient = null;
            }
        }

        public void Send(byte[] data) {
            try {
                if (_currentClient != null && _currentClient.Connected) {
                    _currentClient.Send(data);

                    LogManager.Instance.Log(
                        $"发送PLC数据: {BitConverter.ToString(data)}",
                        LogLevel.INFO
                    );
                }
            } catch (Exception ex) {
                LogManager.Instance.Log(
                    $"发送PLC失败: {ex.Message}",
                    LogLevel.ERROR
                );
            }
        }

        public void Close() {
            _isRunning = false;
            try {
                _currentClient?.Close();
            } catch { }

            try {
                _server?.Close();
            } catch { }
        }

    }
}
