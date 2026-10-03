using System.Windows;

namespace HelloWorld
{
    public partial class HelloWindow : Window
    {
        public HelloWindow(int issueCount, string file)
        {
            InitializeComponent();

            lblInfo.Text = $"The list has {issueCount} issue row(s).\nWrote a line to:\n{file}";
        }
    }
}
