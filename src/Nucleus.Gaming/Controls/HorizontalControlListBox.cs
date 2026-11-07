using Nucleus.Coop;
using Nucleus.Gaming.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Nucleus.Gaming
{
    public class HorizontalControlListBox : UserControl
    {
        private int totalWidth;
        private int border = 1;

        public event Action<object, Control> SelectedChanged;
        public Size Offset { get; set; }
        public Control SelectedControl { get; protected set; }

        public int Border
        {
            get => border;
            set => border = value;
        }

        public HorizontalControlListBox()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            HorizontalScroll.Maximum = 0;
            VerticalScroll.Visible = false;
            AutoScroll = true;
            DoubleBuffered = true;
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        public override bool AutoScroll
        {
            get => base.AutoScroll;
            set
            {
                base.AutoScroll = value;
                if (!value)
                {
                    HorizontalScroll.Visible = false;
                    HorizontalScroll.Enabled = false;
                    VerticalScroll.Visible = false;
                }
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateSizes();
        }

        private bool updatingSize;
        public void UpdateSizes()
        {
            if (updatingSize)
            {
                return;
            }

            HorizontalScroll.Value = 0;//avoid weird glitchs if scrolled before maximizing the main window.
            updatingSize = true;

            totalWidth = 0;
            bool isVerticalVisible = HorizontalScroll.Visible;
            int v = isVerticalVisible ? (1 + SystemInformation.HorizontalScrollBarHeight) : 0;


            for (int i = 0; i < Controls.Count; i++)
            {
                Control con = Controls[i];
                con.Height = Height - v;

                con.Location = new Point(totalWidth, 0);
                totalWidth += con.Width + border;

                con.Invalidate();
            }

            updatingSize = false;

            VerticalScroll.Visible = false;
            HorizontalScroll.Visible = totalWidth > Width;
            HorizontalScroll.Value = 0;//avoid weird glitchs if scrolled before maximizing the main window.
            if (HorizontalScroll.Visible != isVerticalVisible)
            {
                UpdateSizes(); // need to update again
                HorizontalScroll.Value = 0;//avoid weird glitchs if scrolled before maximizing the main window.
            }
        }

        private void C_SizeChanged(object sender, EventArgs e)
        {
            Control con = (Control)sender;
            // this has the potential of being incredibly slow
            UpdateSizes();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            if (!DesignMode && e.Control != null)
            {
                Control c = e.Control;

                c.ControlAdded += C_ControlAdded;
                c.Click += c_Click;
                c.SizeChanged += C_SizeChanged;

                if (c is IRadioControl)
                {
                    c.MouseEnter += c_MouseEnter;
                    c.MouseLeave += c_MouseLeave;
                }

                int index = Controls.IndexOf(c);
                Size s = c.Size;

                c.Location = new Point(totalWidth, 0);
                totalWidth += s.Width + border;
            }

            UpdateSizes();
        }

        private void C_ControlAdded(object sender, ControlEventArgs e)
        {
            Control c = e.Control;

            c.Click += c_Click;
            c.MouseEnter += c_MouseEnter;
            c.MouseLeave += c_MouseLeave;
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            UpdateSizes();
        }

        public void Deselect()
        {
            SelectedControl = null;
            c_Click(this, EventArgs.Empty);
        }

        private void c_MouseEnter(object sender, EventArgs e)
        {
            Control parent = (Control)sender;

            if (parent != SelectedControl && parent is IRadioControl)
            {
                IRadioControl high = (IRadioControl)parent;
                high.UserOver();
            }
        }

        private void c_MouseLeave(object sender, EventArgs e)
        {
            Control parent = (Control)sender;

            if (parent != SelectedControl && parent is IRadioControl)
            {
                IRadioControl high = (IRadioControl)parent;
                high.UserLeave();
            }
        }

        private void c_Click(object sender, EventArgs e)
        {
            Control parent = (Control)sender;

            for (int i = 0; i < Controls.Count; i++)
            {
                Control c = Controls[i];

                if (c is GameControl || c.Parent is GameControl)
                {
                    MouseEventArgs arg = e as MouseEventArgs;

                    if (arg.Button == MouseButtons.Right)
                    {
                        return;
                    }
                }

                if (c is IRadioControl)
                {
                    IRadioControl high = (IRadioControl)c;
                    if (parent == c)
                    {
                        // highlight
                        high.RadioSelected();
                    }
                    else
                    {
                        high.RadioUnselected();
                    }
                }
            }

            if (parent != null && parent != SelectedControl)
            {
                if (SelectedControl != null &&
                    SelectedControl.GetType() != typeof(ComboBox) &&
                    SelectedControl.GetType() != typeof(TextBox))
                {
                    SelectedControl.BackColor = Color.Transparent;
                }

                if (SelectedChanged != null)
                {
                    SelectedControl = parent;
                    SelectedChanged(SelectedControl, this);
                }
            }

            SelectedControl = parent;

            if (SelectedControl.GetType() != typeof(ComboBox) &&
                SelectedControl.GetType() != typeof(TextBox) && SelectedControl.GetType() != typeof(Label) && SelectedControl.GetType() != typeof(GameControl) && SelectedControl.GetType() != typeof(BufferedFlowLayoutPanel))
            {
                SelectedControl.BackColor = Theme_Settings.SelectedBackColor;
            }
            
            OnClick(e);
        }
    }
}
