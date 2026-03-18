using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace pcb_detect {
    public partial class Form1 : Form {

        private Dictionary<string, int> _cameraIndexMap = new Dictionary<string, int>();

        private Dictionary<string, Bitmap> _cameraBitmaps = new Dictionary<string, Bitmap>();

        private PLCManager _plcService;
        private PLCcmdDispatcher _plcDispatcher;
        private JetsonManager _jetsonManager;
        private StatusLightManager[] camLights = new StatusLightManager[18];  // 相机状态显示
        private bool showingUp = true;                                 // 初始相机显示上路
        private const int GROUP_COUNT = 14;                           // 14组拍照位置

        //// 类成员：缓存每个相机的图片
        //private Dictionary<int, Bitmap> _cameraBitmaps = new Dictionary<int, Bitmap>();


        public Form1() {
            InitializeComponent();
            InitUI();
            InitLog();
            InitJetsonManager();
        }

        private void Form1_Load(object sender, EventArgs e) {
            // 初始化相机映射
            InitCameraIndexMap();
        }

        private async void Form1_Shown(object sender, EventArgs e) {
            await Task.Delay(100);
            _ = InitAsync();
        }

        private void InitUI() {
            InitResultShow();
            InitGroupIndex();
            InitButton();
            InitCameraStatus();
        }

        // 示例：控制相机状态
        private void SetCameraStatus(int camIndex, Color color, bool blink = false) {
            if (camIndex < 0 || camIndex >= camLights.Length) return;

            camLights[camIndex].LightColor = color;
            camLights[camIndex].Blink = blink;
            camLights[camIndex].Invalidate();
        }

        private void InitCameraIndexMap() {
            int index = 0;
            for (int j = 1; j <= 9; j++) {
                for (int c = 1; c <= 2; c++) {
                    string key = $"jetson_{j:D2}_cam_{c:D2}";
                    _cameraIndexMap[key] = index++;
                }
            }
        }

        private async Task InitAsync() {
            try {
                Log("正在加载设备...");
                InitPLC(2001);
                await Task.Run(() => ConnectPlc());
                Log("JetsonManager 自动连接中...");
                _plcDispatcher = new PLCcmdDispatcher(_plcService, _jetsonManager);
                Log("系统初始化完成");
            } catch (Exception ex) {
                Log("初始化异常：" + ex.Message);
            }
        }

        // 创建 Jetson 管理器 → 绑定事件 → 注册 Jetson 设备 → 启动自动连接线程
        private void InitJetsonManager() {
            _jetsonManager = new JetsonManager();
            _jetsonManager.OnImageReceived += (jetsonId, cameraId, bmp) => {
                // 接收图片并显示
                this.Invoke(new Action(() => UpdateCameraImage(cameraId, bmp)));
            };
            _jetsonManager.OnDefectImageReceived += (jetsonId, cameraId, bmp, defectName) => {

                this.Invoke(new Action(() => SaveDefectImage(cameraId, bmp, defectName)));
            };
            _jetsonManager.OnConnectionStatusChanged += (jetsonId, connected) => {
                this.Invoke(new Action(() => Log($"Jetson {jetsonId} 连接状态: {(connected ? "已连接" : "断开")}")));
            };
            _jetsonManager.OnHeartbeatReceived += (jetsonId, time) => {
                this.Invoke(new Action(() => Log($"收到 {jetsonId} 心跳: {time}")));
            };
            // 添加 Jetson 放入 _jetsons 字典
            //_jetsonManager.AddJetson("jetson_01", "192.168.1.130", 9000);
            _jetsonManager.AddJetson("jetson_01", "192.168.1.135", 9000);
            // 启动后台守护连接
            _jetsonManager.StartAllAutoConnect();
        }

        private void UpdateCameraImage(string cameraId, Bitmap bmp) {
            // 保存最新的 Bitmap
            if (_cameraBitmaps.ContainsKey(cameraId)) {
                // 先释放旧的 Bitmap
                _cameraBitmaps[cameraId]?.Dispose();
                _cameraBitmaps[cameraId] = (Bitmap)bmp.Clone();
            } else {
                _cameraBitmaps[cameraId] = (Bitmap)bmp.Clone();
            }

            // 触发 panel 重绘
            m_panel_show.Invalidate();
        }

        private void SaveDefectImage(string cameraId, Bitmap bmp, string defectName) {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dir = Path.Combine(baseDir, "DefectImages", cameraId);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string fileName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{defectName}.jpg";
                string filePath = Path.Combine(dir, fileName);
                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                Log($"缺陷图片保存: {filePath}");
            } catch (Exception ex) {
                Log("保存缺陷图片失败: " + ex.Message, LogLevel.ERROR);
            }
        }

        private bool InitPLC(int port = 2001) {
            try {
                _plcService = new PLCManager(port);
                _plcService.OnConnectionStatusChanged += connected => {
                    Log("PLC 连接状态: " + (connected ? "已连接" : "断开"));
                };
                //_plcService.OnClearImg += OnClearPanelImg;  // 清理画布
                _plcService.Init();
                Log("PLC服务启动完成");
                return true;
            } catch (Exception ex) {
                Log("PLC服务启动《失败》: " + ex.Message);
                return false;
            }
        }

        // 连通 PLC
        private bool ConnectPlc() {
            try {
                _plcService.Start();
                if (_plcService.Connected) {
                    Log("PLC连接成功");
                    return true;
                } else {
                    Log("PLC 未连接！");
                    return false;
                }
            } catch (Exception ex) {
                Log("PLC连接失败: " + ex.Message);
                return false;
            }
        }

        private void InitLog() {
            LogManager.Instance.OnLog += OnLogReceived;
            Log("软件启动完成");
        }

        private void InitResultShow() {
            labelResult.Text = "OK";
            labelResult.ForeColor = Color.Green;
            labelResult.Font = new Font("Arial", 80, FontStyle.Bold);
            labelResult.TextAlign = ContentAlignment.MiddleCenter;

            //labelResult.Text = "NG";
            //labelResult.ForeColor = Color.Red;
        }

        private void InitCameraStatus() {
            int startX = 10;       // 每排起始X坐标
            int startY = 20;       // 第一排Y坐标
            int spaceX = 78;       // 横向间距
            int spaceY = 30;       // 纵向间距

            for (int i = 0; i < 18; i++) {
                int row = i / 9;    // 上排 0，下排 1
                int col = i % 9;
                int x = startX + col * spaceX;
                int y = startY + row * spaceY;
                // 连续编号 1~18
                int camNumber = col + 1;
                //int camNumber = i + 1;
                Label lbl = new Label();
                // 上下排添加前缀区分
                string prefix = (row == 0) ? "上" : "下";   // 上排 "上", 下排 "下"
                lbl.Text = $"{prefix}Cam{camNumber:D2}";  // 例如 "上路Cam01" / "下路Cam02"
                //lbl.Text = $"Cam{camNumber:D2}";  // D2 格式，显示 01, 02...
                lbl.Location = new Point(x, y);
                lbl.AutoSize = true;

                // StatusLight 控件
                StatusLightManager light = new StatusLightManager();

                // 间距设置为 25px 左右
                int gap = 5; // 文字和圆点之间距离

                // x + Label 宽度 + gap
                light.Location = new Point(x + lbl.PreferredWidth + gap, y - 2);
                //light.LightColor = Color.Gray;
                //light.Blink = false;
                light.LightColor = Color.Green;
                light.Blink = true;

                camLights[i] = light;
                // 添加到 groupBox1
                groupBox1.Controls.Add(lbl);
                groupBox1.Controls.Add(light);
            }
        }

        private void InitGroupIndex() {
            // flowLayoutPanel控件显示拍照组号
            int groupCount = GROUP_COUNT; // 总组数
            flowLayoutPanel1.Controls.Clear();
            if (groupCount <= 0) return;
            // 计算每个Label的高度，让所有Label均匀填充FlowLayoutPanel高度
            int totalHeight = flowLayoutPanel1.Height;
            int labelHeight = totalHeight / groupCount;

            for (int i = groupCount; i >= 1; i--) {
                Label lbl = new Label();
                lbl.Text = $"{i}";
                lbl.Width = flowLayoutPanel1.Width - 4; // 留一点边距
                lbl.Height = labelHeight;
                lbl.TextAlign = ContentAlignment.MiddleCenter;
                lbl.Font = new Font("微软雅黑", 10, FontStyle.Bold);
                lbl.ForeColor = Color.DarkBlue;
                lbl.BorderStyle = BorderStyle.FixedSingle;
                flowLayoutPanel1.Controls.Add(lbl);
            }
        }

        private void InitButton() {
            btnSwitchRoute.BackColor = Color.LightBlue;
            btnSwitchRoute.FlatStyle = FlatStyle.Flat;
            btnSwitchRoute.FlatAppearance.MouseOverBackColor = Color.CornflowerBlue;
            btnSwitchRoute.FlatAppearance.BorderSize = 1;
            btnSwitchRoute.Font = new Font("Arial", 10, FontStyle.Bold);
            btnSwitchRoute.Text = "显示下路";  // 初始状态
        }

        private void Log(string msg, LogLevel level = LogLevel.INFO) {
            LogManager.Instance.Log(msg, level);
        }

        private void OnLogReceived(string msg, LogLevel level) {
            if (m_tb_log.InvokeRequired) {
                m_tb_log.Invoke(new Action(() => AddLogToTextBox(msg, level)));
            } else {
                AddLogToTextBox(msg, level);
            }
        }

        private void AddLogToTextBox(string msg, LogLevel level) {
            Color color = Color.Black; // 默认颜色
            switch (level) {
                case LogLevel.INFO:
                    color = Color.Black;
                    break;
                case LogLevel.WARN:
                    color = Color.Orange;
                    break;
                case LogLevel.ERROR:
                    color = Color.Red;
                    break;
                default:
                    color = Color.Black;
                    break;
            }
            m_tb_log.SelectionStart = m_tb_log.TextLength;
            m_tb_log.SelectionLength = 0;
            m_tb_log.SelectionColor = color;
            m_tb_log.AppendText(msg + Environment.NewLine);
            m_tb_log.ScrollToCaret();
            m_tb_log.SelectionColor = m_tb_log.ForeColor; // 恢复默认颜色
        }

        private void btnSwitchRoute_Click(object sender, EventArgs e) {
            if (showingUp) {
                // 显示下路
                //m_panel_show.BackgroundImage = mergedDown;
                btnSwitchRoute.Text = "显示上路";
                btnSwitchRoute.BackColor = Color.LightCoral; // 红色表示下路
            } else {
                // 显示上路
                //m_panel_show.BackgroundImage = mergedUp;
                btnSwitchRoute.Text = "显示下路";
                btnSwitchRoute.BackColor = Color.LightBlue;  // 蓝色表示上路
            }
            showingUp = !showingUp;

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            _jetsonManager?.StopAll();
        }

        private void m_panel_show_Paint(object sender, PaintEventArgs e) {
            // 先清空背景为你设置的白色
            e.Graphics.Clear(Color.Black); // 背景

            if (_cameraBitmaps.Count == 0)
                return;

            int panelWidth = m_panel_show.Width;
            int panelHeight = m_panel_show.Height;

            int camIndex = 0;
            int camCount = _cameraBitmaps.Count;

            foreach (var kv in _cameraBitmaps) {
                string cam = kv.Key;
                Bitmap bmp = kv.Value;

                // 计算显示区域：均分 Panel
                int bmpWidth = panelWidth / camCount;
                int bmpHeight = panelHeight;

                Rectangle destRect = new Rectangle(camIndex * bmpWidth, 0, bmpWidth, bmpHeight);

                // 等比例缩放绘制图片
                if (bmp != null) {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(bmp, destRect);
                }

                camIndex++;
            }



        }
    }
}
