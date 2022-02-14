using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace JRunner
{
    public partial class Issues : Form
    {
        public Issues()
        {
            InitializeComponent();
            IssueWizard.Cancelling += WizardCancelled;
            IssueWizard.Finished += WizardFinished;
        }

        private void WizardCancelled(object sender, EventArgs e)
        {
            this.Close();
        }

        private void WizardFinished(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ViewButton_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/X360Tools/J-Runner-Pro/issues");
        }

        private void CreateButton_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/X360Tools/J-Runner-Pro/issues/new/choose");
        }
    }
}
