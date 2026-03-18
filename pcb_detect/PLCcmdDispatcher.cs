using System;
using System.Collections.Generic;
using Newtonsoft.Json;



namespace pcb_detect {
    // PLC命令解析 + 调度模块
    class PLCcmdDispatcher {
        private PLCManager _plc;
        private JetsonManager _jetson;

        public PLCcmdDispatcher(PLCManager plc, JetsonManager jetson) {
            _plc = plc;
            _jetson = jetson;
            _plc.OnMessageReceived += HandlePLCMessage;
            _jetson.OnResultReceived += HandleJetsonResult;
        }

        // 解析PLC消息
        private void HandlePLCMessage(byte[] data) {
            if (data.Length < 4) {
                LogManager.Instance.Log("PLC消息长度错误", LogLevel.WARN);
                return;
            }
            byte sender = data[0];
            byte funcCode = data[1];
            byte pos = data[2];
            byte plc4Code = data[3];
            LogManager.Instance.Log(
                $"PLC命令 设备:{sender} 功能码:{funcCode} 拍照位置:{pos} 状态:{plc4Code}",
                LogLevel.INFO);
            switch (funcCode) {
                case 0x01: HandleStatusQuery(); break;
                case 0x02: HandleCreateModel(pos); break;
                case 0x03: HandleDetect(pos); break;
                case 0x04: HandleCaptureEnd(); break;
                case 0x05: HandlePLCConfirm(); break;
                case 0x09: HandleColse(); break;
                default:
                    LogManager.Instance.Log($"未知功能码:{funcCode}", LogLevel.WARN);
                    break;
            }
        }

        private void HandleStatusQuery() {
            // 至少有一台 Jetson 连接，就返回OK
            byte result = _jetson.AnyConnected() ? (byte)0x01 : (byte)0x02;
            byte[] reply = { 0x02, 0x01, 0x99, result };
            _plc.Send(reply);
            LogManager.Instance.Log("回复PLC状态", LogLevel.INFO);
        }

        // 创建模板
        private void HandleCreateModel(byte pos) {
            byte result = _jetson.AnyConnected() ? (byte)0x01 : (byte)0x02;
            byte[] response = new byte[] { 0x02, 0x02, pos, result };
            _plc.Send(response);
            LogManager.Instance.Log($"发送: 02 02 {pos:X2} {result:X2}", LogLevel.INFO);
            if (pos == 0x80) {
                if (!_jetson.AnyConnected()) {
                    LogManager.Instance.Log("Jetson未连接，无法发送命令", LogLevel.ERROR);
                    return;
                }
                var cmd = new { cmd = "create_model" };
                _jetson.BroadcastCommand(cmd);
                LogManager.Instance.Log("发送Jetson 创建模板命令", LogLevel.INFO);
            }
        }

        // 检测
        private void HandleDetect(byte pos) {
            byte result = _jetson.AnyConnected() ? (byte)0x01 : (byte)0x02;
            byte[] response = new byte[] { 0x02, 0x03, pos, result };
            _plc.Send(response);
            LogManager.Instance.Log($"发送: 02 03 {pos:X2} {result:X2}", LogLevel.INFO);
            if (pos == 0x80) {
                if (!_jetson.AnyConnected()) {
                    LogManager.Instance.Log("Jetson未连接，无法发送命令", LogLevel.ERROR);
                    return;
                }
                var cmd = new { cmd = "detect" };
                _jetson.BroadcastCommand(cmd);
                LogManager.Instance.Log("发送Jetson 检测命令", LogLevel.INFO);
            }
        }

        // 拍照结束
        private void HandleCaptureEnd() {
            byte[] response = HexStringToBytes("02 04 99 01");
            _plc.Send(response);
            LogManager.Instance.Log("发送: 02 04 99 01", LogLevel.INFO);
            if (!_jetson.AnyConnected()) {
                LogManager.Instance.Log("Jetson未连接，无法发送命令", LogLevel.ERROR);
                return;
            }
            var cmd = new { cmd = "capture_end" };
            _jetson.BroadcastCommand(cmd);
        }

        // PLC确认收到结果
        private void HandlePLCConfirm() {
            LogManager.Instance.Log("PLC确认收到检测结果 05", LogLevel.INFO);
        }

        private void HandleColse() {
            byte[] response = HexStringToBytes("02 09 99 01");
            _plc.Send(response);
            LogManager.Instance.Log("发送: 02 09 99 01", LogLevel.INFO);
            if (!_jetson.AnyConnected()) {
                LogManager.Instance.Log("Jetson未连接，无法发送命令", LogLevel.ERROR);
                return;
            }
            var cmd = new { cmd = "stop" };
            _jetson.BroadcastCommand(cmd);
            LogManager.Instance.Log("向 Jetson 发送 stop 命令。", LogLevel.INFO);
        }

        // Jetson返回结果
        private void HandleJetsonResult(string jetsonId, string json) {
            try {
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (result["cmd"].ToString() != "result") 
                    return;
                int camera = Convert.ToInt32(result["camera"]);
                int detectResult = Convert.ToInt32(result["result"]);
                byte plcResult = detectResult == 1 ? (byte)0x01 : (byte)0x00;
                byte[] reply = { 0x02, 0x05, (byte)camera, plcResult };
                _plc.Send(reply);
                LogManager.Instance.Log(
                    $"检测结果返回PLC camera:{camera} result:{plcResult}",
                    LogLevel.INFO);
            } catch (Exception ex) {
                LogManager.Instance.Log($"Jetson结果解析失败:{ex.Message}", LogLevel.ERROR);
            }
        }

        private byte[] HexStringToBytes(string hex) {
            hex = hex.Replace(" ", "");              // 去掉空格
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);  // 每次去2个字节，转换为16进制
            }
            return bytes;
        }

    }
}
