using System.Windows.Forms;

public class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    public BufferedFlowLayoutPanel()
    {
        this.DoubleBuffered = true;
        this.ResizeRedraw = true;
    }
}