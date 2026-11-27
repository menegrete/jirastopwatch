using Avalonia.Controls;

namespace HelloWorld
{
    internal partial class HelloWindow : Window
    {
        // Needed by the XAML loader and previewers only.
        public HelloWindow()
        {
            InitializeComponent();
        }

        public HelloWindow(int issueCount, string file) : this()
        {
            lblInfo.Text = $"The list has {issueCount} issue row(s).\nWrote a line to:\n{file}";
            btnClose.Click += (s, e) => Close();
        }
    }
}
