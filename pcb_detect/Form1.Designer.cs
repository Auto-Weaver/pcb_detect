
namespace pcb_detect {
    partial class Form1 {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent() {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.m_panel_show = new System.Windows.Forms.Panel();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.labelResult = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.button2 = new System.Windows.Forms.Button();
            this.btnSwitchRoute = new System.Windows.Forms.Button();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.m_tb_log = new System.Windows.Forms.RichTextBox();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Location = new System.Drawing.Point(0, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(707, 75);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "相机状态：";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.m_panel_show);
            this.groupBox2.Controls.Add(this.flowLayoutPanel1);
            this.groupBox2.Location = new System.Drawing.Point(0, 93);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(710, 566);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "图片显示：";
            // 
            // m_panel_show
            // 
            this.m_panel_show.BackColor = System.Drawing.SystemColors.WindowText;
            this.m_panel_show.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_panel_show.Location = new System.Drawing.Point(41, 17);
            this.m_panel_show.Name = "m_panel_show";
            this.m_panel_show.Size = new System.Drawing.Size(666, 546);
            this.m_panel_show.TabIndex = 1;
            this.m_panel_show.Paint += new System.Windows.Forms.PaintEventHandler(this.m_panel_show_Paint);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 17);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(38, 546);
            this.flowLayoutPanel1.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.labelResult);
            this.groupBox3.Location = new System.Drawing.Point(716, 12);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(256, 139);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "检测结果：";
            // 
            // labelResult
            // 
            this.labelResult.AutoSize = true;
            this.labelResult.Location = new System.Drawing.Point(30, 17);
            this.labelResult.Name = "labelResult";
            this.labelResult.Size = new System.Drawing.Size(41, 12);
            this.labelResult.TabIndex = 0;
            this.labelResult.Text = "label1";
            this.labelResult.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.button2);
            this.groupBox4.Controls.Add(this.btnSwitchRoute);
            this.groupBox4.Location = new System.Drawing.Point(716, 157);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(256, 82);
            this.groupBox4.TabIndex = 3;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "功能切换：";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(150, 20);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(86, 54);
            this.button2.TabIndex = 1;
            this.button2.Text = "列表";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // btnSwitchRoute
            // 
            this.btnSwitchRoute.Location = new System.Drawing.Point(32, 20);
            this.btnSwitchRoute.Name = "btnSwitchRoute";
            this.btnSwitchRoute.Size = new System.Drawing.Size(86, 54);
            this.btnSwitchRoute.TabIndex = 0;
            this.btnSwitchRoute.Text = "上/下相机切换";
            this.btnSwitchRoute.UseVisualStyleBackColor = true;
            this.btnSwitchRoute.Click += new System.EventHandler(this.btnSwitchRoute_Click);
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.m_tb_log);
            this.groupBox5.Location = new System.Drawing.Point(716, 245);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(271, 414);
            this.groupBox5.TabIndex = 4;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "日志：";
            // 
            // m_tb_log
            // 
            this.m_tb_log.BackColor = System.Drawing.SystemColors.Window;
            this.m_tb_log.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_tb_log.Location = new System.Drawing.Point(3, 17);
            this.m_tb_log.Name = "m_tb_log";
            this.m_tb_log.ReadOnly = true;
            this.m_tb_log.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.m_tb_log.Size = new System.Drawing.Size(265, 394);
            this.m_tb_log.TabIndex = 0;
            this.m_tb_log.Text = "";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 661);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.Text = "PCB外观缺陷检测v1.0";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Shown += new System.EventHandler(this.Form1_Shown);
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Panel m_panel_show;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnSwitchRoute;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label labelResult;
        private System.Windows.Forms.RichTextBox m_tb_log;
    }
}

