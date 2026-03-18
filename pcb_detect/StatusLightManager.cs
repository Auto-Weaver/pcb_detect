using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;


namespace pcb_detect {
    public class StatusLightManager : Control {
        public Color LightColor { get; set; } = Color.Gray;
        public bool Blink { get; set; } = false;

        private bool visibleState = true;
        private Timer timer;

        public StatusLightManager() {
            Width = 16;
            Height = 16;

            timer = new Timer();
            timer.Interval = 500;
            timer.Tick += (s, e) => {
                if (Blink) {
                    visibleState = !visibleState;
                    Invalidate();
                }
            };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color drawColor = visibleState ? LightColor : Color.DarkGray;

            using (Brush brush = new SolidBrush(drawColor)) {
                g.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            }
        }




    }
}


